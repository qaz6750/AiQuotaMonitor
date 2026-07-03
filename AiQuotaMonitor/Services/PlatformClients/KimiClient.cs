using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>
/// Kimi Code 订阅用量客户端。
/// 参考 AIUsage 的 KimiProvider：优先请求国内区，再回退全球区。
/// </summary>
public sealed class KimiClient : IPlatformClient
{
    public string PlatformId => "kimi";

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = DecompressionMethods.All,
    })
    { Timeout = TimeSpan.FromSeconds(30) };

    private static readonly string[] DefaultEndpoints =
    {
        "https://api.kimi.com/coding/v1/usages",
        "https://api.moonshot.ai/v1/usages",
    };

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        if (string.IsNullOrWhiteSpace(credential))
            throw new InvalidOperationException("未配置 Kimi Code API Key。可在 kimi.com/code/console 创建。 ");

        var endpoints = BuildEndpoints(baseUrl);
        Exception? last = null;
        foreach (var endpoint in endpoints)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {credential.Trim()}");
                req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor-Kimi/1.0");
                req.Headers.Accept.ParseAdd("application/json");

                using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                var body = await resp.Content.ReadAsStringAsync();
                if ((int)resp.StatusCode is 401 or 403)
                {
                    last = new HttpRequestException("Kimi Code API Key 无效或无权限。");
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                {
                    last = new HttpRequestException($"Kimi Code 用量请求失败 (HTTP {(int)resp.StatusCode})。");
                    continue;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement.TryGetProperty("data", out var data) ? data : doc.RootElement;
                var result = Normalize(root);
                if (result.FiveHour is not null || result.Weekly is not null) return result;
                last = new InvalidOperationException("Kimi Code 响应中没有可用的配额窗口。");
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        throw last ?? new HttpRequestException("Kimi Code 用量请求失败。");
    }

    private static IEnumerable<string> BuildEndpoints(string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var b = baseUrl.Trim();
            if (!b.StartsWith("http", StringComparison.OrdinalIgnoreCase)) b = "https://" + b;
            if (b.Contains("/usages", StringComparison.OrdinalIgnoreCase)) yield return b;
        }
        foreach (var endpoint in DefaultEndpoints) yield return endpoint;
    }

    private static UsageResult Normalize(JsonElement root)
    {
        var now = DateTimeOffset.Now;
        var windows = ParseLimits(root).OrderBy(w => w.DurationSeconds ?? double.MaxValue).ToList();
        var weekly = ParseWindow(root.TryGetProperty("usage", out var usage) ? usage : default, "Kimi 周额度", 7 * 86400);

        QuotaInfo? primary = null;
        QuotaInfo? secondary = null;
        if (windows.Count > 0)
        {
            primary = ToQuota(windows[0].Window, QuotaKind.FiveHour, "Kimi 频控窗口");
            secondary = weekly is not null ? ToQuota(weekly.Window, QuotaKind.Weekly, "Kimi 周额度") : null;
        }
        else if (weekly is not null)
        {
            primary = ToQuota(weekly.Window, QuotaKind.FiveHour, "Kimi 周额度");
        }

        return new UsageResult
        {
            Level = "Kimi Code",
            FiveHour = primary,
            Weekly = secondary,
            TotalDays = 7,
            ActiveDays = primary?.CurrentUsage > 0 ? 1 : 0,
            FetchedAt = now,
        };
    }

    private static List<ParsedWindow> ParseLimits(JsonElement root)
    {
        var list = new List<ParsedWindow>();
        if (!root.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Array) return list;
        foreach (var item in limits.EnumerateArray())
        {
            var detail = item.TryGetProperty("detail", out var d) ? d : item;
            var duration = item.TryGetProperty("window", out var meta) ? DurationSeconds(meta) : null;
            if (ParseWindow(detail, "Kimi 频控窗口", duration) is { } parsed) list.Add(parsed);
        }
        return list;
    }

    private static ParsedWindow? ParseWindow(JsonElement detail, string label, double? durationSeconds)
    {
        if (detail.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        var limit = Num(detail, "limit", "total", "quota");
        var used = Num(detail, "used", "consumed");
        var remaining = Num(detail, "remaining", "left");
        if (used is null && limit is not null && remaining is not null) used = Math.Max(0, limit.Value - remaining.Value);
        if (limit is null && used is null && remaining is null) return null;

        var pct = 0d;
        if (limit is > 0 && used is not null) pct = Math.Clamp(used.Value / limit.Value * 100d, 0, 100);
        else if (limit is > 0 && remaining is not null) pct = Math.Clamp((limit.Value - remaining.Value) / limit.Value * 100d, 0, 100);

        return new ParsedWindow(durationSeconds, new QuotaInfo
        {
            Kind = QuotaKind.Other,
            DisplayType = label,
            Percentage = pct,
            CurrentUsage = used,
            Total = limit,
            Remaining = remaining ?? (limit is not null && used is not null ? Math.Max(0, limit.Value - used.Value) : null),
            NextResetTimeMs = ResetMs(detail),
        });
    }

    private static QuotaInfo ToQuota(QuotaInfo src, QuotaKind kind, string label) => new()
    {
        Kind = kind,
        DisplayType = label,
        Percentage = src.Percentage,
        CurrentUsage = src.CurrentUsage,
        Total = src.Total,
        Remaining = src.Remaining,
        NextResetTimeMs = src.NextResetTimeMs,
    };

    private static double? DurationSeconds(JsonElement meta)
    {
        var duration = Num(meta, "duration", "value");
        if (duration is null) return null;
        var unit = Str(meta, "timeUnit", "unit")?.ToLowerInvariant();
        return unit switch
        {
            "hour" or "hours" or "h" => duration * 3600,
            "day" or "days" or "d" => duration * 86400,
            "minute" or "minutes" or "m" => duration * 60,
            _ => duration,
        };
    }

    private static long? ResetMs(JsonElement e)
    {
        var raw = Num(e, "reset_at", "resetAt", "reset_time", "resetTime");
        if (raw is null && Str(e, "reset_at", "resetAt") is { } s && DateTimeOffset.TryParse(s, out var dto)) return dto.ToUnixTimeMilliseconds();
        if (raw is null) return null;
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

    private sealed record ParsedWindow(double? DurationSeconds, QuotaInfo Window);
}
