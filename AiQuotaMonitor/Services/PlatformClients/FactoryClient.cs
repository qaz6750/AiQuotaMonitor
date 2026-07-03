using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>
/// Factory Droid usage client.
/// Reference: AIUsage DroidProvider, endpoint /api/organization/subscription/usage.
/// </summary>
public sealed class FactoryClient : IPlatformClient
{
    public string PlatformId => "factory";

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = DecompressionMethods.All,
        UseCookies = false,
    })
    { Timeout = TimeSpan.FromSeconds(30) };

    private static readonly string[] DefaultHosts =
    {
        "https://app.factory.ai",
        "https://api.factory.ai",
    };

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        var auth = credential.Trim();
        if (string.IsNullOrWhiteSpace(auth))
            throw new InvalidOperationException("未配置 Factory API Key / Bearer Token。");
        var isCookie = auth.Contains('=') && auth.Contains(';');
        var bearer = isCookie ? ExtractCookieValue(auth, "access-token") : auth;

        Exception? last = null;
        foreach (var host in BuildHosts(baseUrl))
        {
            try
            {
                var url = $"{host}/api/organization/subscription/usage?useCache=true";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(bearer)) req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearer}");
                if (isCookie) req.Headers.TryAddWithoutValidation("Cookie", auth);
                req.Headers.TryAddWithoutValidation("Accept", "application/json");
                req.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                req.Headers.TryAddWithoutValidation("Origin", "https://app.factory.ai");
                req.Headers.TryAddWithoutValidation("Referer", "https://app.factory.ai/");
                req.Headers.TryAddWithoutValidation("x-factory-client", "web-app");
                req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor/1.0");

                using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                var body = await resp.Content.ReadAsStringAsync();
                if ((int)resp.StatusCode is 401 or 403)
                {
                    last = new HttpRequestException("Factory 凭据无效或已过期。");
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                {
                    last = new HttpRequestException($"Factory 用量请求失败 (HTTP {(int)resp.StatusCode})。");
                    continue;
                }

                using var doc = JsonDocument.Parse(body);
                return Normalize(doc.RootElement);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw last ?? new HttpRequestException("Factory 用量请求失败。");
    }

    private static string? ExtractCookieValue(string cookieHeader, string name)
    {
        foreach (var part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;
            if (part[..idx].Equals(name, StringComparison.OrdinalIgnoreCase)) return part[(idx + 1)..];
        }
        return null;
    }

    private static IEnumerable<string> BuildHosts(string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var b = baseUrl.Trim();
            if (!b.StartsWith("http", StringComparison.OrdinalIgnoreCase)) b = "https://" + b;
            var ub = new UriBuilder(b);
            var port = ub.Uri.IsDefaultPort ? string.Empty : ":" + ub.Port;
            yield return $"{ub.Scheme}://{ub.Host}{port}";
        }
        foreach (var host in DefaultHosts) yield return host;
    }

    private static UsageResult Normalize(JsonElement root)
    {
        var usage = root.TryGetProperty("usage", out var u) ? u : root;
        var standard = usage.TryGetProperty("standard", out var s) ? s : default;
        var premium = usage.TryGetProperty("premium", out var p) ? p : default;
        var resetMs = ParseResetMs(usage);
        var planName = Str(usage, "planName") ?? "Factory Droid";

        return new UsageResult
        {
            Level = planName,
            FiveHour = standard.ValueKind == JsonValueKind.Object ? ToQuota(standard, QuotaKind.FiveHour, "Factory Standard", resetMs) : null,
            Weekly = premium.ValueKind == JsonValueKind.Object ? ToQuota(premium, QuotaKind.Weekly, "Factory Premium", resetMs) : null,
            TotalDays = 30,
            ActiveDays = 0,
            FetchedAt = DateTimeOffset.Now,
        };
    }

    private static QuotaInfo ToQuota(JsonElement e, QuotaKind kind, string label, long? resetMs)
    {
        var used = Num(e, "userTokens", "used", "usage") ?? 0;
        var total = Num(e, "totalAllowance", "limit", "total") ?? 0;
        var usedRatio = Num(e, "usedRatio", "usedPercent", "percentage");
        var pct = usedRatio is not null
            ? (usedRatio <= 1.001 ? usedRatio.Value * 100 : usedRatio.Value)
            : total > 0 ? used / total * 100 : 0;
        pct = Math.Clamp(pct, 0, 100);
        return new QuotaInfo
        {
            Kind = kind,
            DisplayType = label,
            Percentage = pct,
            CurrentUsage = used,
            Total = total,
            Remaining = total > 0 ? Math.Max(0, total - used) : null,
            NextResetTimeMs = resetMs,
        };
    }

    private static long? ParseResetMs(JsonElement e)
    {
        var raw = Num(e, "endDate", "periodEnd", "resetAt");
        if (raw is null && Str(e, "endDate", "periodEnd", "resetAt") is { } s && DateTimeOffset.TryParse(s, out var dto)) return dto.ToUnixTimeMilliseconds();
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
