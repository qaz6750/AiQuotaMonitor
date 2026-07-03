using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>
/// GitHub Copilot quota client.
/// Reference: AIUsage CopilotProvider, endpoint https://api.github.com/copilot_internal/user.
/// </summary>
public sealed class CopilotClient : IPlatformClient
{
    public string PlatformId => "copilot";

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = DecompressionMethods.All,
    })
    { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        var token = credential.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("未配置 GitHub Token。请填写具有 Copilot 权限的 GitHub token。");

        var endpoint = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.github.com/copilot_internal/user"
            : baseUrl.Trim();
        if (!endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase)) endpoint = "https://" + endpoint;
        if (!endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("为保护凭据安全，仅支持 HTTPS 接口地址。");

        using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
        req.Headers.TryAddWithoutValidation("Authorization", $"token {token}");
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        req.Headers.TryAddWithoutValidation("Editor-Plugin-Version", "GitHubCopilotChat/0.26.7");
        req.Headers.TryAddWithoutValidation("Editor-Version", "vscode/1.96.2");
        req.Headers.TryAddWithoutValidation("User-Agent", "GitHubCopilotChat/0.26.7");
        req.Headers.TryAddWithoutValidation("X-Github-Api-Version", "2025-04-01");

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if ((int)resp.StatusCode is 401 or 403)
            throw new HttpRequestException("GitHub Token 无效或没有 Copilot 访问权限。");
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub Copilot 用量请求失败 (HTTP {(int)resp.StatusCode})。");

        using var doc = JsonDocument.Parse(body);
        return Normalize(doc.RootElement);
    }

    private static UsageResult Normalize(JsonElement root)
    {
        var resetMs = ResetMs(root);
        var snapshots = root.TryGetProperty("quota_snapshots", out var qs) && qs.ValueKind == JsonValueKind.Object ? qs : default;
        var premium = FindSnapshot(snapshots, "premiuminteractions") ?? FallbackPremium(root);
        var chat = FindSnapshot(snapshots, "chat");
        var completions = FindSnapshot(snapshots, "completions");

        return new UsageResult
        {
            Level = PlanName(root),
            FiveHour = premium is null ? null : ToQuota(premium.Value, QuotaKind.FiveHour, "Copilot Premium", resetMs),
            Weekly = chat is null ? null : ToQuota(chat.Value, QuotaKind.Weekly, "Copilot Chat", resetMs),
            Mcp = completions is null ? null : ToQuota(completions.Value, QuotaKind.Mcp, "Copilot Completions", resetMs),
            TotalDays = 30,
            ActiveDays = 0,
            FetchedAt = DateTimeOffset.Now,
        };
    }

    private static JsonElement? FindSnapshot(JsonElement snapshots, string normalizedName)
    {
        if (snapshots.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in snapshots.EnumerateObject())
        {
            var key = new string(p.Name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (key == normalizedName && p.Value.ValueKind == JsonValueKind.Object) return p.Value;
        }
        return null;
    }

    private static JsonElement? FallbackPremium(JsonElement root)
    {
        if (!root.TryGetProperty("monthly_quotas", out var monthly) || !root.TryGetProperty("limited_user_quotas", out var limited)) return null;
        if (!monthly.TryGetProperty("chat", out var mChat) || !limited.TryGetProperty("chat", out var lChat)) return null;
        var entitlement = Num(mChat) ?? 0;
        var remaining = Num(lChat) ?? 0;
        var json = $$"{"entitlement":{{entitlement}},"remaining":{{remaining}},"percent_remaining":{{(entitlement > 0 ? remaining / entitlement * 100 : 0)}}}";
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static QuotaInfo ToQuota(JsonElement snap, QuotaKind kind, string label, long? resetMs)
    {
        var unlimited = Bool(snap, "unlimited");
        var entitlement = Num(snap, "entitlement", "total", "limit") ?? 0;
        var remaining = Num(snap, "remaining", "quota_remaining") ?? 0;
        var used = entitlement > 0 ? Math.Max(0, entitlement - remaining) : 0;
        var pctRemaining = Num(snap, "percent_remaining");
        var usedPct = unlimited ? 0 : pctRemaining is not null ? Math.Clamp(100 - pctRemaining.Value, 0, 100) : entitlement > 0 ? Math.Clamp(used / entitlement * 100, 0, 100) : 0;
        return new QuotaInfo
        {
            Kind = kind,
            DisplayType = label,
            Percentage = usedPct,
            CurrentUsage = used,
            Total = entitlement,
            Remaining = remaining,
            NextResetTimeMs = resetMs,
        };
    }

    private static long? ResetMs(JsonElement root)
    {
        var raw = Str(root, "quota_reset_date_utc", "quota_reset_date");
        if (raw is not null && DateTimeOffset.TryParse(raw, out var dto)) return dto.ToUnixTimeMilliseconds();
        var num = Num(root, "quota_reset_date_utc", "quota_reset_date");
        if (num is null || num <= 0) return null;
        return num > 10_000_000_000 ? (long)num.Value : DateTimeOffset.FromUnixTimeSeconds((long)num.Value).ToUnixTimeMilliseconds();
    }

    private static string PlanName(JsonElement root)
        => Str(root, "copilot_plan", "access_type_sku")?.Replace('_', ' ').Replace('-', ' ') ?? "GitHub Copilot";

    private static bool Bool(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

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

    private static double? Num(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var d)) return d;
        if (e.ValueKind == JsonValueKind.String && double.TryParse(e.GetString(), out d)) return d;
        return null;
    }

    private static string? Str(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String) return p.GetString();
        return null;
    }
}
