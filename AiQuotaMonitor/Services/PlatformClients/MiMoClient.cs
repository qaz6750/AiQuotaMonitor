using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>
/// 小米 MiMo 用量查询客户端（Token 套餐）。
/// 端点：https://platform.xiaomimimo.com/api/v1/tokenPlan/usage
/// 鉴权：Cookie 头（userId / api-platform_slh / api-platform_ph）。
/// 返回 {data: {usage, monthUsage}}，每窗口含 {percent, used, limit}。
/// 参考自 mimo-usage-monitor 项目的 api.ts。
/// </summary>
public sealed class MiMoClient : IPlatformClient
{
    /// <inheritdoc/>
    public string PlatformId => "mimo";

    private static readonly HttpClient Http = SharedHttp.Create(TimeSpan.FromSeconds(30));

    private const int MaxRetry = 3;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>查询 MiMo 用量。credential 为完整 cookie 字符串。</summary>
    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        if (string.IsNullOrWhiteSpace(credential))
            throw new InvalidOperationException("未配置 MiMo cookie。");

        var host = NormalizeHost(baseUrl);
        if (!host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("为保护凭据安全，仅支持 HTTPS 接口地址。");

        var url = $"{host}/api/v1/tokenPlan/usage";
        var cookie = ParseCookies(credential);

        var data = await SafeGet<MiMoResponse>(url, cookie, enableRetry);
        return Normalize(data);
    }

    /// <summary>查询自定义时间范围的 token 用量（MiMo 暂不支持，返回 0）。</summary>
    public Task<long> QueryRangeTokensAsync(string credential, string baseUrl, DateTime start, DateTime end, bool enableRetry = true)
        => Task.FromResult(0L);

    private static string ParseCookies(string raw)
    {
        return string.Join("; ", raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static async Task<T?> SafeGet<T>(string url, string cookie, bool enableRetry) where T : class
    {
        var attempts = enableRetry ? MaxRetry + 1 : 1;
        Exception? last = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("Cookie", cookie);
                req.Headers.Accept.ParseAdd("application/json");
                req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor/1.0");
                req.Headers.TryAddWithoutValidation("x-timezone", TimeZoneInfo.Local.Id);

                using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                var body = await resp.Content.ReadAsStringAsync();
                var code = (int)resp.StatusCode;
                if (code == 429 || code >= 500)
                    throw new RetryableHttpException($"HTTP {code}");
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"HTTP {code}");

                var trimmed = body.TrimStart();
                if (trimmed.Length == 0 || trimmed[0] == '<')
                    throw new HttpRequestException("Cookie 无效或已过期，服务端返回了登录页。请重新获取 cookie。");

                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.Deserialize<T>(JsonOpts);
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < attempts)
            {
                last = ex;
                await Task.Delay(Backoff(attempt));
            }
        }
        throw last ?? new HttpRequestException("请求失败。");
    }

    private static bool IsRetryable(Exception ex)
        => ex is RetryableHttpException or TaskCanceledException or TimeoutException;

    private static int Backoff(int attempt)
    {
        var exp = 1000 * Math.Pow(2, attempt - 1);
        return (int)Math.Round(Math.Min(exp, 8000) * (0.75 + Random.Shared.NextDouble() * 0.5));
    }

    private static string NormalizeHost(string baseUrl)
    {
        var b = baseUrl.Trim();
        if (!b.StartsWith("http", StringComparison.OrdinalIgnoreCase)) b = "https://" + b;
        var ub = new UriBuilder(b);
        var port = ub.Uri.IsDefaultPort ? string.Empty : ":" + ub.Port;
        var path = ub.Uri.AbsolutePath.TrimEnd('/');
        return $"{ub.Scheme}://{ub.Host}{port}{path}";
    }

    /// <summary>把 MiMo 响应归一化为 UsageResult。MiMo 的 percent 是 0-1 范围，需 ×100。</summary>
    private static UsageResult Normalize(MiMoResponse? data)
    {
        QuotaInfo? plan = null, monthly = null;

        if (data?.Data?.Usage is { } u)
        {
            var planTotal = u.Items?.FirstOrDefault(i => i.Name == "plan_total_token");
            // MiMo percent 是 0-1 范围（1=100%），转为 0-100
            var pct = (planTotal?.Percent ?? u.Percent ?? 0) * 100;
            var used = planTotal?.Used ?? 0;
            var limit = planTotal?.Limit ?? 0;
            plan = new QuotaInfo
            {
                Kind = QuotaKind.FiveHour,
                DisplayType = "MiMo 套餐",
                Percentage = pct,
                CurrentUsage = used,
                Total = limit,
                Remaining = Math.Max(0, limit - used),
                NextResetTimeMs = null,
            };
        }

        if (data?.Data?.MonthUsage is { } mu)
        {
            var monthTotal = mu.Items?.FirstOrDefault(i => i.Name == "month_total_token");
            var mPct = (monthTotal?.Percent ?? mu.Percent ?? 0) * 100;
            var mUsed = monthTotal?.Used ?? 0;
            var mLimit = monthTotal?.Limit ?? 0;
            monthly = new QuotaInfo
            {
                Kind = QuotaKind.Weekly,
                DisplayType = "MiMo 月度",
                Percentage = mPct,
                CurrentUsage = mUsed,
                Total = mLimit,
                Remaining = Math.Max(0, mLimit - mUsed),
                NextResetTimeMs = null,
            };
        }

        return new UsageResult
        {
            Level = "MiMo",
            FiveHour = plan,
            Weekly = monthly,
            TotalDays = 0,
            ActiveDays = 0,
        };
    }
}

// ===== MiMo API 响应模型 =====

internal sealed class MiMoResponse
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public MiMoData? Data { get; set; }
}

internal sealed class MiMoData
{
    public MiMoUsage? Usage { get; set; }
    public MiMoUsage? MonthUsage { get; set; }
}

internal sealed class MiMoUsage
{
    public double? Percent { get; set; }
    public long Used { get; set; }
    public long Limit { get; set; }
    public List<MiMoUsageItem>? Items { get; set; }
}

internal sealed class MiMoUsageItem
{
    public string? Name { get; set; }
    public long Used { get; set; }
    public long Limit { get; set; }
    public double Percent { get; set; }
}
