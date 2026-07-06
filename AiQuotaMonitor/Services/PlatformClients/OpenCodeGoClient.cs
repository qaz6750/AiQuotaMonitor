using System.Globalization;
using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>
/// OpenCode Go client.
/// Public docs expose the Go model directory and fixed subscription limits. Console usage is backed by
/// workspace login state, so this client also accepts a custom JSON endpoint if one is provided later.
/// </summary>
public sealed class OpenCodeGoClient : IPlatformClient
{
    public string PlatformId => "opencode-go";

    private const double RollingLimitUsd = 12;
    private const double WeeklyLimitUsd = 30;
    private const double MonthlyLimitUsd = 60;
    private static readonly HttpClient Http = SharedHttp.Create(TimeSpan.FromSeconds(30));

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        var token = credential.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("未配置 OpenCode Go API Key。");

        var host = NormalizeHost(baseUrl, "https://opencode.ai/zen/go/v1");
        using var doc = await GetJsonAsync($"{host}/models", token);
        var models = EnumerateModels(doc.RootElement).ToList();
        var usage = TryReadUsage(doc.RootElement);

        var rollingUsed = usage.Rolling ?? 0;
        var weeklyUsed = usage.Weekly ?? 0;
        var monthlyUsed = usage.Monthly ?? 0;
        var level = usage.HasUsage
            ? $"Go · 5h ${rollingUsed:F2} / ${RollingLimitUsd:F0}"
            : $"Go · {models.Count} 个模型 · 用量需控制台授权";

        return new UsageResult
        {
            Level = level,
            FiveHour = MoneyQuota("5 小时额度", rollingUsed, RollingLimitUsd, usage.RollingResetMs),
            Weekly = MoneyQuota("周额度", weeklyUsed, WeeklyLimitUsd, usage.WeeklyResetMs),
            Mcp = MoneyQuota("月额度", monthlyUsed, MonthlyLimitUsd, usage.MonthlyResetMs, QuotaKind.Mcp),
            ModelUsage = BuildModelSummary(models, monthlyUsed),
            TotalDays = 30,
            ActiveDays = usage.HasUsage ? 1 : 0,
            FetchedAt = DateTimeOffset.Now,
        };
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor/1.0");
        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if ((int)resp.StatusCode is 401 or 403) throw new HttpRequestException("OpenCode Go API Key 无效或权限不足。");
        if (resp.StatusCode == HttpStatusCode.NotFound) throw new HttpRequestException("OpenCode Go 模型目录不存在，请确认 Base URL 为 https://opencode.ai/zen/go/v1。");
        if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"OpenCode Go 请求失败 (HTTP {(int)resp.StatusCode})。");
        return JsonDocument.Parse(body);
    }

    private static List<ModelUsageSummary> BuildModelSummary(List<JsonElement> models, double monthlyUsed)
    {
        var list = new List<ModelUsageSummary>
        {
            new() { Model = "Monthly used cents", TotalTokens = (long)Math.Round(monthlyUsed * 100) },
            new() { Model = "Models available", TotalTokens = models.Count },
        };
        foreach (var model in models.Take(8))
        {
            var name = Str(model, "id", "model", "name") ?? "model";
            list.Add(new ModelUsageSummary { Model = name, TotalTokens = 0 });
        }
        return list;
    }

    private static IEnumerable<JsonElement> EnumerateModels(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root.EnumerateArray();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array) return data.EnumerateArray();
        if (root.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array) return models.EnumerateArray();
        return Array.Empty<JsonElement>();
    }

    private static (double? Rolling, double? Weekly, double? Monthly, long? RollingResetMs, long? WeeklyResetMs, long? MonthlyResetMs, bool HasUsage) TryReadUsage(JsonElement root)
    {
        var usageRoot = root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object ? usage : root;
        var rolling = ReadMoney(usageRoot, "rollingUsage", "rolling", "fiveHourUsage", "five_hour_usage");
        var weekly = ReadMoney(usageRoot, "weeklyUsage", "weekly", "weekUsage", "weekly_usage");
        var monthly = ReadMoney(usageRoot, "monthlyUsage", "monthly", "monthUsage", "monthly_usage");
        return (
            rolling,
            weekly,
            monthly,
            ReadResetMs(usageRoot, "rollingResetMs", "rolling_reset_ms", "fiveHourResetMs"),
            ReadResetMs(usageRoot, "weeklyResetMs", "weekly_reset_ms"),
            ReadResetMs(usageRoot, "monthlyResetMs", "monthly_reset_ms"),
            rolling is not null || weekly is not null || monthly is not null
        );
    }

    private static QuotaInfo MoneyQuota(string label, double used, double total, long? resetMs, QuotaKind kind = QuotaKind.FiveHour)
    {
        return new QuotaInfo
        {
            Kind = kind,
            DisplayType = label,
            Percentage = total > 0 ? Math.Clamp(used / total * 100, 0, 100) : 0,
            CurrentUsage = used,
            Total = total,
            Remaining = Math.Max(0, total - used),
            NextResetTimeMs = resetMs,
        };
    }

    private static double? ReadMoney(JsonElement e, params string[] names)
    {
        var raw = Num(e, names);
        if (raw is null) return null;
        // OpenCode internally stores usage as micro-cents; plain JSON integrations may already return dollars.
        return raw > 100_000 ? raw.Value / 100_000_000d : raw.Value;
    }

    private static long? ReadResetMs(JsonElement e, params string[] names)
    {
        var raw = Num(e, names);
        if (raw is null || raw <= 0) return null;
        return raw > 10_000_000_000 ? (long)raw.Value : DateTimeOffset.FromUnixTimeSeconds((long)raw.Value).ToUnixTimeMilliseconds();
    }

    private static string NormalizeHost(string baseUrl, string fallback)
    {
        var b = string.IsNullOrWhiteSpace(baseUrl) ? fallback : baseUrl.Trim();
        if (!b.StartsWith("http", StringComparison.OrdinalIgnoreCase)) b = "https://" + b;
        var ub = new UriBuilder(b);
        var port = ub.Uri.IsDefaultPort ? string.Empty : ":" + ub.Port;
        var path = ub.Uri.AbsolutePath.TrimEnd('/');
        return SharedHttp.EnsureHttps($"{ub.Scheme}://{ub.Host}{port}{path}");
    }

    private static double? Num(JsonElement e, params string[] names)
    {
        foreach (var n in names)
        {
            if (!e.TryGetProperty(n, out var p)) continue;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return d;
            if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
            if (p.ValueKind == JsonValueKind.Object && (Num(p, "usage", "used", "value") is { } nested)) return nested;
        }
        return null;
    }

    private static string? Str(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String) return p.GetString();
        return null;
    }
}
