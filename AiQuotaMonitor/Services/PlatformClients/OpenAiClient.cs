using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>OpenAI organization usage/cost client. Requires an Admin Key.</summary>
public sealed class OpenAiClient : IPlatformClient
{
    public string PlatformId => "gpt";

    private static readonly HttpClient Http = SharedHttp.Create(TimeSpan.FromSeconds(45));

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        var token = credential.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("未配置 OpenAI Admin Key。该接口需要组织级 Admin Key。 ");

        var host = NormalizeHost(baseUrl, "https://api.openai.com");
        var end = DateTimeOffset.UtcNow;
        var start = end.AddDays(-7);
        var startUnix = start.ToUnixTimeSeconds();
        var endUnix = end.ToUnixTimeSeconds();
        var bucketWidth = "1h";

        var usageUrl = $"{host}/v1/organization/usage/completions?start_time={startUnix}&end_time={endUnix}&bucket_width={bucketWidth}&limit=180&group_by[]=model";
        var costsUrl = $"{host}/v1/organization/costs?start_time={startUnix}&end_time={endUnix}&bucket_width=1d&limit=31&group_by[]=line_item";

        using var usageDoc = await GetJsonAsync(usageUrl, token);
        using var costsDoc = await GetJsonAsync(costsUrl, token);
        return Normalize(usageDoc.RootElement, costsDoc.RootElement);
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor-OpenAI/1.0");
        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if ((int)resp.StatusCode is 401 or 403)
            throw new HttpRequestException("OpenAI Admin Key 无效、权限不足或未开启组织用量接口。 ");
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenAI 用量请求失败 (HTTP {(int)resp.StatusCode})。 ");
        return JsonDocument.Parse(body);
    }

    private static UsageResult Normalize(JsonElement usageRoot, JsonElement costsRoot)
    {
        var now = DateTimeOffset.Now;
        var todayKey = now.ToString("yyyy-MM-dd");
        var perModel = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var byHour = new SortedDictionary<string, Dictionary<string, double>>();
        double totalCalls = 0;

        foreach (var bucket in EnumerateArray(usageRoot, "data"))
        {
            var ts = Num(bucket, "start_time") ?? Num(bucket, "startTime");
            var label = ts is null ? Str(bucket, "date") ?? todayKey : DateTimeOffset.FromUnixTimeSeconds((long)ts.Value).LocalDateTime.ToString("yyyy-MM-dd HH:00");
            if (!byHour.TryGetValue(label, out var slot)) byHour[label] = slot = new(StringComparer.OrdinalIgnoreCase);

            foreach (var result in EnumerateArray(bucket, "results"))
            {
                var model = Str(result, "model") ?? "GPT";
                var input = Num(result, "input_tokens", "inputTokens") ?? 0;
                var output = Num(result, "output_tokens", "outputTokens") ?? 0;
                var requests = Num(result, "num_model_requests", "requests") ?? 0;
                var tokens = input + output;
                slot[model] = slot.GetValueOrDefault(model) + tokens;
                perModel[model] = perModel.GetValueOrDefault(model) + tokens;
                totalCalls += requests;
            }
        }

        var trend = BuildTrend(byHour, perModel);
        var todayTokens = trend.PerModel?.Sum(p => p.YValue.Where((_, i) => i < trend.XTime.Count && trend.XTime[i].StartsWith(todayKey, StringComparison.Ordinal)).Sum(v => v ?? 0)) ?? 0;
        var totalTokens = trend.TotalTokens;
        var costUsd = SumCosts(costsRoot);
        var costCny = costUsd * CurrencyRates.UsdToCny;

        return new UsageResult
        {
            Level = "OpenAI API",
            FiveHour = new QuotaInfo { Kind = QuotaKind.FiveHour, DisplayType = "今日 Tokens", Percentage = 0, CurrentUsage = todayTokens, Total = totalTokens },
            Weekly = new QuotaInfo { Kind = QuotaKind.Weekly, DisplayType = "近 7 天费用", Percentage = 0, CurrentUsage = costCny, Total = null },
            ModelUsage = perModel.Select(kv => new ModelUsageSummary { Model = kv.Key, TotalTokens = (long)kv.Value }).OrderByDescending(m => m.TotalTokens).ToList(),
            Trend7d = trend,
            Trend30d = trend,
            ActiveDays = trend.XTime.Select(x => x.Length >= 10 ? x[..10] : x).Distinct().Count(),
            TotalDays = 7,
            FetchedAt = now,
        };
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

    private static double SumCosts(JsonElement root)
    {
        double total = 0;
        foreach (var bucket in EnumerateArray(root, "data"))
            foreach (var result in EnumerateArray(bucket, "results"))
                total += Num(result, "amount", "cost", "value") ?? NestedNum(result, "amount", "value") ?? 0;
        return total;
    }

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array ? p.EnumerateArray() : Array.Empty<JsonElement>();

    private static double? NestedNum(JsonElement e, string objectName, string propertyName)
        => e.TryGetProperty(objectName, out var o) && o.ValueKind == JsonValueKind.Object ? Num(o, propertyName) : null;

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
