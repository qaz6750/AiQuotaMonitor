using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>
/// MiniMax Token Plan 用量客户端。
/// 参考 AIUsage 的 MiniMaxProvider，仅支持可查询订阅额度的 sk-cp-* Token Plan Key。
/// </summary>
public sealed class MiniMaxClient : IPlatformClient
{
    public string PlatformId => "minimax";

    private static readonly HttpClient Http = SharedHttp.Create(TimeSpan.FromSeconds(30));

    private static readonly string[] DefaultEndpoints =
    {
        "https://api.minimaxi.com/v1/token_plan/remains",
        "https://api.minimax.io/v1/token_plan/remains",
    };

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        if (string.IsNullOrWhiteSpace(credential))
            throw new InvalidOperationException("未配置 MiniMax Token Plan Key。该接口需要 sk-cp-* 订阅 Key。 ");

        var endpoints = BuildEndpoints(baseUrl);
        Exception? last = null;
        foreach (var endpoint in endpoints)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {credential.Trim()}");
                req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor-MiniMax/1.0");
                req.Headers.Accept.ParseAdd("application/json");

                using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    last = new HttpRequestException($"MiniMax Token Plan 请求失败 (HTTP {(int)resp.StatusCode})。");
                    continue;
                }

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("base_resp", out var br)
                    && Num(br, "status_code") is { } code && code != 0)
                {
                    last = new HttpRequestException(Str(br, "status_msg") ?? "MiniMax Token Plan 返回业务错误。");
                    continue;
                }

                var result = Normalize(doc.RootElement);
                if (result.FiveHour is not null || result.Weekly is not null) return result;
                last = new InvalidOperationException("MiniMax 响应中没有可用的 model_remains 配额。");
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw last ?? new HttpRequestException("MiniMax Token Plan 请求失败。");
    }

    private static IEnumerable<string> BuildEndpoints(string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var b = baseUrl.Trim();
            if (!b.StartsWith("http", StringComparison.OrdinalIgnoreCase)) b = "https://" + b;
            if (b.Contains("token_plan/remains", StringComparison.OrdinalIgnoreCase)) yield return SharedHttp.EnsureHttps(b);
        }
        foreach (var endpoint in DefaultEndpoints) yield return endpoint;
    }

    private static UsageResult Normalize(JsonElement root)
    {
        if (!root.TryGetProperty("model_remains", out var remains) || remains.ValueKind != JsonValueKind.Array)
            return new UsageResult { Level = "MiniMax Token Plan", FetchedAt = DateTimeOffset.Now };

        JsonElement? selected = null;
        foreach (var item in remains.EnumerateArray())
        {
            if (Str(item, "model_name")?.Equals("general", StringComparison.OrdinalIgnoreCase) == true)
            {
                selected = item;
                break;
            }
            selected ??= item;
        }
        if (selected is not { } entry) return new UsageResult { Level = "MiniMax Token Plan", FetchedAt = DateTimeOffset.Now };

        var fiveHour = ParseWindow(
            entry,
            QuotaKind.FiveHour,
            "MiniMax 5h 窗口",
            totalName: "current_interval_total_count",
            usedName: "current_interval_usage_count",
            remainingPercentName: "current_interval_remaining_percent",
            endTimeName: "end_time");
        var weekly = ParseWindow(
            entry,
            QuotaKind.Weekly,
            "MiniMax 周额度",
            totalName: "current_weekly_total_count",
            usedName: "current_weekly_usage_count",
            remainingPercentName: "current_weekly_remaining_percent",
            endTimeName: "weekly_end_time");

        return new UsageResult
        {
            Level = "MiniMax Token Plan",
            FiveHour = fiveHour,
            Weekly = weekly,
            TotalDays = 7,
            ActiveDays = fiveHour?.CurrentUsage > 0 || weekly?.CurrentUsage > 0 ? 1 : 0,
            FetchedAt = DateTimeOffset.Now,
        };
    }

    private static QuotaInfo? ParseWindow(JsonElement entry, QuotaKind kind, string label, string totalName, string usedName, string remainingPercentName, string endTimeName)
    {
        var total = Num(entry, totalName);
        var used = Num(entry, usedName);
        var remainingPercent = Num(entry, remainingPercentName);
        if (total is null && used is null && remainingPercent is null) return null;

        var pct = 0d;
        if (total is > 0 && used is not null) pct = Math.Clamp(used.Value / total.Value * 100d, 0, 100);
        else if (remainingPercent is not null) pct = 100d - Math.Clamp(remainingPercent.Value, 0, 100);

        return new QuotaInfo
        {
            Kind = kind,
            DisplayType = label,
            Percentage = pct,
            CurrentUsage = used,
            Total = total,
            Remaining = total is not null && used is not null ? Math.Max(0, total.Value - used.Value) : null,
            NextResetTimeMs = ResetMs(Num(entry, endTimeName)),
        };
    }

    private static long? ResetMs(double? raw)
    {
        if (raw is null || raw <= 0) return null;
        return raw > 10_000_000_000 ? (long)raw.Value : DateTimeOffset.FromUnixTimeSeconds((long)raw.Value).ToUnixTimeMilliseconds();
    }

    private static double? Num(JsonElement e, params string[] names)
    {
        foreach (var n in names)
        {
            if (!e.TryGetProperty(n, out var p)) continue;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return d;
            if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), out d)) return d;
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
