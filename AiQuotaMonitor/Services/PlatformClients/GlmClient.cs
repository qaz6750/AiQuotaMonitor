using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Models;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models.Api;

namespace AiQuotaMonitor.Services;

/// <summary>标记可重试的 HTTP 异常（429 / 5xx）。</summary>
internal sealed class RetryableHttpException : Exception
{
    public RetryableHttpException(string message) : base(message) { }
}

/// <summary>
/// 智谱 GLM Coding Plan 监控接口客户端。
/// 端点（相对基础域名 https://open.bigmodel.cn）：
///   GET /api/monitor/usage/model-usage?startTime=..&endTime=..
///   GET /api/monitor/usage/tool-usage?startTime=..&endTime=..
///   GET /api/monitor/usage/quota/limit
/// 认证：Authorization 头直接携带 API Key（与官方一致）。
/// </summary>
public sealed class GlmClient : IPlatformClient
{
    /// <inheritdoc/>
    public string PlatformId => "glm";

    private static readonly HttpClient Http = SharedHttp.Create(TimeSpan.FromSeconds(60));

    private const int MaxRetry = 1;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>并发拉取全部端点并归一化为 <see cref="UsageResult"/>。任一端点失败不丢弃其余。</summary>
    public async Task<UsageResult> QueryUsageAsync(string apiKey, string baseUrl, bool enableRetry = true)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("未配置 API Key。");

        var host = NormalizeHost(baseUrl);
        // 安全：凭据以明文放入 Authorization 头，仅允许 HTTPS，避免被窃听
        if (!host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("为保护凭据安全，仅支持 HTTPS 接口地址。");

        var (s7, e7) = Window(7);
        var (s30, e30) = Window(30);
        var q7 = $"?startTime={Uri.EscapeDataString(s7)}&endTime={Uri.EscapeDataString(e7)}";
        var q30 = $"?startTime={Uri.EscapeDataString(s30)}&endTime={Uri.EscapeDataString(e30)}";

        // 串行查询并短暂错峰，避免一次刷新产生突发并发请求。
        var quota = await SafeGet<Models.Api.QuotaLimitData>($"{host}/api/monitor/usage/quota/limit", apiKey, string.Empty, enableRetry);
        await Task.Delay(250);
        var model7 = await SafeGet<Models.Api.ModelUsageData>($"{host}/api/monitor/usage/model-usage", apiKey, q7, enableRetry);
        await Task.Delay(250);
        var tools = await SafeGet<List<Models.Api.ToolUsageItem>>($"{host}/api/monitor/usage/tool-usage", apiKey, q7, enableRetry);
        await Task.Delay(250);
        var model30 = await SafeGet<Models.Api.ModelUsageData>($"{host}/api/monitor/usage/model-usage", apiKey, q30, enableRetry);

        return Normalize(quota, model7, tools, model30);
    }

    /// <summary>查询自定义时间范围（起止）的按小时模型用量明细，用于按小时统计图。</summary>
    public async Task<ModelUsageData?> QueryRangeModelUsageAsync(string apiKey, string baseUrl, DateTime start, DateTime end, bool enableRetry = true)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("未配置 API Key。");

        var host = NormalizeHost(baseUrl);
        if (!host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("为保护凭据安全，仅支持 HTTPS 接口地址。");

        var q = $"?startTime={Uri.EscapeDataString(Fmt(start))}&endTime={Uri.EscapeDataString(Fmt(end))}";
        return await SafeGet<ModelUsageData>($"{host}/api/monitor/usage/model-usage", apiKey, q, enableRetry);
    }

    /// <summary>剥离可能附带的 /api/anthropic 等路径，仅保留 scheme://host[:port]。</summary>
    private static string NormalizeHost(string baseUrl)
    {
        var b = baseUrl.Trim();
        if (!b.StartsWith("http", StringComparison.OrdinalIgnoreCase)) b = "https://" + b;
        var ub = new UriBuilder(b);
        var port = ub.Uri.IsDefaultPort ? string.Empty : ":" + ub.Port;
        return $"{ub.Scheme}://{ub.Host}{port}";
    }

    private static async Task<T?> SafeGet<T>(string url, string apiKey, string query, bool enableRetry) where T : class
    {
        try
        {
            return await GetAsync<T>(url, apiKey, query, enableRetry);
        }
        catch (Exception ex)
        {
            // 最关键的 quota/limit 失败时向上抛，让 UI 能提示鉴权/网络错误
            if (url.Contains("quota/limit", StringComparison.OrdinalIgnoreCase)) throw;
            System.Diagnostics.Debug.WriteLine($"[GlmClient] {url} failed: {ex.Message}");
            return null;
        }
    }

    private static async Task<T?> GetAsync<T>(string url, string apiKey, string query, bool enableRetry) where T : class
    {
        var attempts = enableRetry ? MaxRetry + 1 : 1;
        Exception? last = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url + query);
                req.Headers.TryAddWithoutValidation("Authorization", apiKey);
                req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor/1.0");
                req.Headers.AcceptLanguage.ParseAdd("en-US,en");
                req.Headers.Accept.ParseAdd("application/json");

                using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                var body = await resp.Content.ReadAsStringAsync();

                var code = (int)resp.StatusCode;
                if (code == 429 || code >= 500)
                    throw new RetryableHttpException(MapHttpError(resp.StatusCode));

                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException(MapHttpError(resp.StatusCode));

                using var doc = JsonDocument.Parse(body);
                var dataEl = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
                return dataEl.Deserialize<T>(JsonOpts);
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < attempts)
            {
                last = ex;
                await Task.Delay(Backoff(attempt));
                continue;
            }
        }

        throw last ?? new HttpRequestException("请求失败。");
    }

    private static bool IsRetryable(Exception ex)
        => ex is RetryableHttpException or TaskCanceledException or TimeoutException;

    /// <summary>指数退避 + 抖动（1s ~ 8s）。</summary>
    private static int Backoff(int attempt)
    {
        var exp = 1000 * Math.Pow(2, attempt - 1);
        var capped = Math.Min(exp, 8000);
        var jitter = capped * (0.75 + Random.Shared.NextDouble() * 0.5);
        return (int)Math.Round(jitter);
    }

    private static string MapHttpError(HttpStatusCode code)
    {
        var c = (int)code;
        return c switch
        {
            401 => "认证失败 (HTTP 401)，请检查 API Key。",
            403 => "访问被拒 (HTTP 403)，请检查 API Key 权限。",
            429 => "请求过于频繁 (HTTP 429)，请稍后再试。",
            _ when c >= 500 => $"服务器错误 (HTTP {c})，请稍后再试。",
            _ => $"请求失败 (HTTP {c})。",
        };
    }

    // ===== 时间窗口 =====

    private static (string start, string end) Window(int days)
    {
        var now = DateTime.Now;
        var start = now.Date.AddDays(-(days - 1));
        var end = now.Date.Add(new TimeSpan(23, 59, 59));
        return (Fmt(start), Fmt(end));
    }

    private static string Fmt(DateTime d) => $"{d:yyyy-MM-dd HH:mm:ss}";

    // ===== 归一化 =====

    private static UsageResult Normalize(
        Models.Api.QuotaLimitData? quota,
        Models.Api.ModelUsageData? m7,
        List<Models.Api.ToolUsageItem>? tools,
        Models.Api.ModelUsageData? m30)
    {
        Models.QuotaInfo? fiveHour = null, weekly = null, mcp = null;
        var level = quota?.Level;
        var tokensLimitCount = 0;

        foreach (var item in quota?.Limits ?? new List<Models.Api.LimitItem>())
        {
            var next = item.NextResetTime;
            if (item.Type == "TOKENS_LIMIT")
            {
                var isFirst = tokensLimitCount == 0;
                tokensLimitCount++;
                var info = new Models.QuotaInfo
                {
                    Kind = isFirst ? Models.QuotaKind.FiveHour : Models.QuotaKind.Weekly,
                    DisplayType = isFirst ? Models.QuotaTypes.FiveHour : Models.QuotaTypes.Weekly,
                    Percentage = item.Percentage,
                    NextResetTimeMs = next,
                };
                if (isFirst) fiveHour = info; else weekly = info;
            }
            else if (item.Type == "TIME_LIMIT")
            {
                mcp = new Models.QuotaInfo
                {
                    Kind = Models.QuotaKind.Mcp,
                    DisplayType = Models.QuotaTypes.Mcp,
                    Percentage = item.Percentage,
                    NextResetTimeMs = next,
                    CurrentUsage = item.CurrentValue,
                    Total = item.Usage,
                    Remaining = item.Remaining,
                    UsageDetails = (item.UsageDetails ?? new())
                        .Select(u => new Models.McpUsageDetail { ModelCode = u.ModelCode ?? string.Empty, Usage = u.Usage })
                        .ToList(),
                };
            }
        }

        var modelUsage = ToModelUsage(m7);
        var toolUsage = (tools ?? new())
            .Select(t => new Models.ToolUsageSummary
            {
                Tool = t.Tool ?? string.Empty,
                CallCount = t.CallCount,
                SuccessCount = t.SuccessCount,
                FailureCount = t.FailureCount,
            })
            .ToList();
        var trend7 = ToTrend(m7);
        var trend30 = ToTrend(m30);
        var (activeDays, totalDays) = CalcActiveDays(m30 ?? m7);

        return new Models.UsageResult
        {
            FiveHour = fiveHour,
            Weekly = weekly,
            Mcp = mcp,
            Level = level,
            ModelUsage = modelUsage,
            ToolUsage = toolUsage,
            Trend7d = trend7,
            Trend30d = trend30,
            ActiveDays = activeDays,
            TotalDays = totalDays,
            FetchedAt = DateTimeOffset.Now,
        };
    }

    private static List<Models.ModelUsageSummary> ToModelUsage(Models.Api.ModelUsageData? m)
    {
        var result = new List<Models.ModelUsageSummary>();
        if (m?.ModelUsage is { Count: > 0 } list)
        {
            foreach (var i in list)
            {
                result.Add(new Models.ModelUsageSummary
                {
                    Model = i.Model ?? string.Empty,
                    InputTokens = i.InputTokens,
                    OutputTokens = i.OutputTokens,
                    TotalTokens = i.TotalTokens,
                    RequestCount = i.RequestCount,
                });
            }
            return result;
        }

        if (m?.ModelDataList is { Count: > 0 } md)
        {
            for (var i = 0; i < md.Count; i++)
            {
                var name = md[i].ModelName ?? $"Model{i + 1}";
                var tokens = (long)(md[i].TokensUsage ?? new()).Sum(v => v ?? 0);
                var calls = (int)(md[i].ModelCallCount ?? md[i].CallCount ?? new()).Sum(v => (int)(v ?? 0));
                result.Add(new Models.ModelUsageSummary { Model = name, TotalTokens = tokens, RequestCount = calls });
            }
        }
        return result;
    }

    private static Models.TrendSeries? ToTrend(Models.Api.ModelUsageData? m)
    {
        if (m?.XTime is null || m.TokensUsage is null || m.XTime.Count == 0) return null;

        var per = new List<Models.ModelTrendSeries>();
        if (m.ModelDataList is { Count: > 0 } md)
        {
            for (var i = 0; i < md.Count; i++)
            {
                var name = md[i].ModelName ?? $"Model{i + 1}";
                per.Add(new Models.ModelTrendSeries
                {
                    Model = name,
                    YValue = (md[i].TokensUsage ?? new()).ToList(),
                    CallCount = (md[i].ModelCallCount ?? md[i].CallCount ?? new()).ToList(),
                    TotalTokens = (long)(md[i].TokensUsage ?? new()).Sum(v => v ?? 0),
                    Color = Models.GlmPricing.GetModelColor(name, i),
                });
            }
        }

        return new Models.TrendSeries
        {
            XTime = m.XTime,
            YValue = m.TokensUsage,
            ModelCallCount = (m.ModelCallCount ?? new()).ToList(),
            PerModel = per.Count > 0 ? per : null,
            TotalTokens = m.TotalUsage?.TotalTokensUsage ?? m.TokensUsage.Sum(v => v ?? 0),
            TotalCalls = m.TotalUsage?.TotalModelCallCount ?? (m.ModelCallCount?.Sum(v => v ?? 0) ?? 0),
        };
    }

    private static (int active, int total) CalcActiveDays(Models.Api.ModelUsageData? m)
    {
        if (m?.XTime is null || m.TokensUsage is null || m.XTime.Count == 0) return (0, 0);
        var exists = new HashSet<string>();
        var active = new HashSet<string>();
        for (var i = 0; i < m.XTime.Count; i++)
        {
            var key = m.XTime[i].Split(' ')[0];
            exists.Add(key);
            var v = i < m.TokensUsage.Count ? m.TokensUsage[i] : null;
            if (v is double d && d > 0) active.Add(key);
        }
        return (active.Count, exists.Count);
    }
}
