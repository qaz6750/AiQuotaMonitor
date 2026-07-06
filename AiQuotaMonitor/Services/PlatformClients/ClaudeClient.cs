using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>Anthropic organization usage/cost report client. Requires an Admin Key.</summary>
public sealed class ClaudeClient : IPlatformClient
{
    public string PlatformId => "claude";

    private static readonly HttpClient Http = SharedHttp.Create(TimeSpan.FromSeconds(45));

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        var token = credential.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("未配置 Anthropic Admin Key。该接口需要组织级 Admin Key。 ");

        var host = baseUrl.Contains("platform.claude.com", StringComparison.OrdinalIgnoreCase)
            ? "https://api.anthropic.com"
            : NormalizeHost(baseUrl, "https://api.anthropic.com");
        var end = DateTimeOffset.UtcNow.Date.AddDays(1);
        var start = end.AddDays(-7);
        var url = $"{host}/v1/organizations/usage_report/messages?starting_at={start:yyyy-MM-ddTHH:mm:ssZ}&ending_at={end:yyyy-MM-ddTHH:mm:ssZ}&bucket_width=1h&group_by[]=model";

        JsonDocument? balanceDoc = null;
        try
        {
            balanceDoc = await TryGetBalanceJsonAsync(host, baseUrl, token);
            using var doc = await GetJsonAsync(url, token);
            return Normalize(doc.RootElement, balanceDoc?.RootElement);
        }
        finally
        {
            balanceDoc?.Dispose();
        }
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("x-api-key", token);
        req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        req.Headers.TryAddWithoutValidation("anthropic-beta", "usage-and-cost-api-2024-10-01");
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor-Claude/1.0");
        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        AppLogger.Verbose($"GET {url} -> HTTP {(int)resp.StatusCode}: {BalanceHttp.Truncate(body)}");
        if ((int)resp.StatusCode is 401 or 403)
            throw new HttpRequestException("Anthropic Admin Key 无效、权限不足或未开启 Usage Report API。 ");
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Claude 用量请求失败 (HTTP {(int)resp.StatusCode})。 ");
        return JsonDocument.Parse(body);
    }

    private static async Task<JsonDocument?> TryGetBalanceJsonAsync(string apiHost, string configuredBaseUrl, string token)
    {
        var platformHost = "https://platform.claude.com";
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl) && configuredBaseUrl.Contains("platform.claude.com", StringComparison.OrdinalIgnoreCase))
        {
            platformHost = NormalizeHost(configuredBaseUrl, platformHost);
        }

        foreach (var url in new[]
        {
            $"{apiHost}/v1/organizations/credits",
            $"{apiHost}/v1/organizations/billing/credits",
            $"{apiHost}/v1/organizations/billing/usage",
            $"{platformHost}/api/organizations/credits",
            $"{platformHost}/api/organizations/billing",
            $"{platformHost}/api/organizations/usage",
        }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var doc = await GetJsonAsync(url, token);
                if (ReadBalance(doc.RootElement).Available is not null) return doc;
                doc.Dispose();
            }
            catch (HttpRequestException ex)
            {
                AppLogger.Verbose($"Claude 余额端点不可用 {url}: {ex.Message}");
            }
        }
        return null;
    }

    private static UsageResult Normalize(JsonElement root, JsonElement? balanceRoot)
    {
        var now = DateTimeOffset.Now;
        var byHour = new SortedDictionary<string, Dictionary<string, double>>();
        var perModel = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double totalCostUsd = 0;

        foreach (var bucket in EnumerateBuckets(root))
        {
            var label = ParseBucketLabel(bucket);
            if (!byHour.TryGetValue(label, out var slot)) byHour[label] = slot = new(StringComparer.OrdinalIgnoreCase);

            foreach (var result in EnumerateResults(bucket))
            {
                var model = Str(result, "model") ?? "Claude";
                var input = Num(result, "input_tokens", "inputTokens") ?? 0;
                var output = Num(result, "output_tokens", "outputTokens") ?? 0;
                var cacheRead = Num(result, "cache_read_input_tokens", "cacheReadInputTokens") ?? 0;
                var cacheCreate = Num(result, "cache_creation_input_tokens", "cacheCreationInputTokens") ?? 0;
                var tokens = input + output + cacheRead + cacheCreate;
                slot[model] = slot.GetValueOrDefault(model) + tokens;
                perModel[model] = perModel.GetValueOrDefault(model) + tokens;
                totalCostUsd += Num(result, "cost_usd", "costUsd", "cost") ?? NestedNum(result, "amount", "value") ?? 0;
            }
        }

        var trend = BuildTrend(byHour, perModel);
        var todayKey = now.ToString("yyyy-MM-dd");
        var todayTokens = trend.XTime.Select((x, i) => x.StartsWith(todayKey, StringComparison.Ordinal) ? trend.YValue[i] ?? 0 : 0).Sum();
        var costCny = totalCostUsd * CurrencyRates.UsdToCny;

        var balance = balanceRoot is null ? default : ReadBalance(balanceRoot.Value);
        return new UsageResult
        {
            Level = balance.Available is double available ? $"Claude API · 余额 ${available:F2}" : "Claude API",
            FiveHour = balance.Available is double availableBalance
                ? new QuotaInfo { Kind = QuotaKind.Other, DisplayType = "可用余额", Percentage = balance.Total is > 0 ? Math.Clamp(availableBalance / balance.Total.Value * 100, 0, 100) : 100, CurrentUsage = availableBalance, Total = balance.Total, Remaining = availableBalance }
                : new QuotaInfo { Kind = QuotaKind.FiveHour, DisplayType = "今日 Tokens", Percentage = 0, CurrentUsage = todayTokens, Total = trend.TotalTokens },
            Weekly = new QuotaInfo { Kind = QuotaKind.Weekly, DisplayType = "近 7 天费用", Percentage = 0, CurrentUsage = costCny, Total = null },
            ModelUsage = perModel.Select(kv => new ModelUsageSummary { Model = kv.Key, TotalTokens = (long)kv.Value }).OrderByDescending(m => m.TotalTokens).ToList(),
            Trend7d = trend,
            Trend30d = trend,
            ActiveDays = trend.XTime.Select(x => x.Length >= 10 ? x[..10] : x).Distinct().Count(),
            TotalDays = 7,
            FetchedAt = now,
        };
    }

    private static IEnumerable<JsonElement> EnumerateBuckets(JsonElement root)
    {
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array) return data.EnumerateArray();
        if (root.TryGetProperty("buckets", out var buckets) && buckets.ValueKind == JsonValueKind.Array) return buckets.EnumerateArray();
        return root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : Array.Empty<JsonElement>();
    }

    private static IEnumerable<JsonElement> EnumerateResults(JsonElement bucket)
    {
        if (bucket.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array) return results.EnumerateArray();
        if (bucket.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array) return models.EnumerateArray();
        return new[] { bucket };
    }

    private static string ParseBucketLabel(JsonElement bucket)
    {
        var raw = Str(bucket, "starting_at", "start_time", "startTime", "date") ?? DateTimeOffset.Now.ToString("yyyy-MM-dd HH:00");
        if (long.TryParse(raw, out var unix)) return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("yyyy-MM-dd HH:00");
        return DateTimeOffset.TryParse(raw, out var dto) ? dto.LocalDateTime.ToString("yyyy-MM-dd HH:00") : raw;
    }

    private static TrendSeries BuildTrend(SortedDictionary<string, Dictionary<string, double>> byHour, Dictionary<string, double> perModel)
    {
        var x = byHour.Keys.ToList();
        var models = perModel.Keys.OrderBy(k => k).ToList();
        var series = models.Select((m, idx) => new ModelTrendSeries
        {
            Model = m,
            Color = GlmPricing.GetModelColor(m, idx),
            YValue = x.Select(t => (double?)byHour[t].GetValueOrDefault(m)).ToList(),
            CallCount = x.Select(_ => (double?)0).ToList(),
            TotalTokens = (long)perModel[m],
        }).ToList();
        var y = x.Select(t => (double?)byHour[t].Values.Sum()).ToList();
        return new TrendSeries { XTime = x, YValue = y, ModelCallCount = x.Select(_ => (double?)0).ToList(), PerModel = series, TotalTokens = y.Sum(v => v ?? 0), TotalCalls = 0 };
    }

    private static double? NestedNum(JsonElement e, string objectName, string propertyName)
        => e.TryGetProperty(objectName, out var o) && o.ValueKind == JsonValueKind.Object ? Num(o, propertyName) : null;

    private static (double? Available, double? Total) ReadBalance(JsonElement root)
    {
        var data = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : root;
        var available = Num(data, "available", "available_balance", "availableBalance", "remaining", "remaining_balance", "remainingBalance", "balance", "credits_remaining");
        var total = Num(data, "total", "total_balance", "totalBalance", "credit_limit", "credits_total", "limit");
        return (available, total);
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

    private static string NormalizeHost(string baseUrl, string fallback)
    {
        var b = string.IsNullOrWhiteSpace(baseUrl) ? fallback : baseUrl.Trim();
        if (!b.StartsWith("http", StringComparison.OrdinalIgnoreCase)) b = "https://" + b;
        var ub = new UriBuilder(b);
        var port = ub.Uri.IsDefaultPort ? string.Empty : ":" + ub.Port;
        return SharedHttp.EnsureHttps($"{ub.Scheme}://{ub.Host}{port}");
    }
}
