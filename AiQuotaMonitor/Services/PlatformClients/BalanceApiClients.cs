using System.Globalization;
using System.Net;
using System.Text.Json;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>OpenRouter credits + key spend client.</summary>
public sealed class OpenRouterClient : IPlatformClient
{
    public string PlatformId => "openrouter";
    private static readonly HttpClient Http = BalanceHttp.Create();

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        var token = credential.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("未配置 OpenRouter Management Key。");
        var host = BalanceHttp.NormalizeHost(baseUrl, "https://openrouter.ai/api/v1");
        using var credits = await BalanceHttp.GetJsonAsync(Http, $"{host}/credits", token);
        JsonDocument? key = null;
        try { key = await BalanceHttp.GetJsonAsync(Http, $"{host}/key", token); }
        catch (Exception ex) { AppLogger.Swallowed("OpenRouter Key API", ex); }

        var data = credits.RootElement.TryGetProperty("data", out var d) ? d : credits.RootElement;
        var total = BalanceHttp.Num(data, "total_credits", "totalCredits") ?? 0;
        var used = BalanceHttp.Num(data, "total_usage", "totalUsage") ?? 0;
        var balance = Math.Max(0, total - used);
        var daily = key is null ? null : BalanceHttp.DeepNum(key.RootElement, "data", "usage_daily") ?? BalanceHttp.DeepNum(key.RootElement, "data", "day_spend");
        var weekly = key is null ? null : BalanceHttp.DeepNum(key.RootElement, "data", "usage_weekly") ?? BalanceHttp.DeepNum(key.RootElement, "data", "week_spend");
        var monthly = key is null ? null : BalanceHttp.DeepNum(key.RootElement, "data", "usage_monthly") ?? BalanceHttp.DeepNum(key.RootElement, "data", "month_spend");
        key?.Dispose();

        return new UsageResult
        {
            Level = $"余额 ${balance:F2}",
            FiveHour = BalanceHttp.MoneyValue("可用余额", balance, "USD", total > 0 ? Math.Clamp(balance / total * 100, 0, 100) : 100),
            Weekly = BalanceHttp.MoneyQuota("本月消费", monthly ?? used, total, "USD"),
            ModelUsage = BalanceHttp.MoneyModels(("日消费", daily), ("周消费", weekly), ("月消费", monthly), ("总额度", total)),
            TotalDays = 30,
            ActiveDays = 0,
            FetchedAt = DateTimeOffset.Now,
        };
    }
}

/// <summary>DeepSeek balance client.</summary>
public sealed class DeepSeekClient : IPlatformClient
{
    public string PlatformId => "deepseek";
    private static readonly HttpClient Http = BalanceHttp.Create();

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        var token = credential.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("未配置 DeepSeek API Key。");
        var host = BalanceHttp.NormalizeHost(baseUrl, "https://api.deepseek.com");
        using var doc = await BalanceHttp.GetJsonAsync(Http, $"{host}/user/balance", token);
        var root = doc.RootElement;
        List<JsonElement> infos;
        if (root.TryGetProperty("balance_infos", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            infos = arr.EnumerateArray().ToList();
        }
        else
        {
            infos = new List<JsonElement>();
        }
        JsonElement? selected = null;
        foreach (var item in infos)
        {
            if ((BalanceHttp.Str(item, "currency") ?? string.Empty).Equals("CNY", StringComparison.OrdinalIgnoreCase)) { selected = item; break; }
            selected ??= item;
        }
        var e = selected ?? root;
        var currency = BalanceHttp.Str(e, "currency") ?? "CNY";
        var symbol = BalanceHttp.CurrencySymbol(currency);
        var total = BalanceHttp.Num(e, "total_balance", "totalBalance") ?? 0;
        var paid = BalanceHttp.Num(e, "topped_up_balance", "toppedUpBalance") ?? 0;
        var granted = BalanceHttp.Num(e, "granted_balance", "grantedBalance") ?? 0;
        var available = BalanceHttp.Bool(root, "is_available") != false;
        return new UsageResult
        {
            Level = available ? $"可用 {symbol}{total:F2}" : $"余额不可用 · {symbol}{total:F2}",
            FiveHour = BalanceHttp.MoneyValue("可用余额", total, currency, available ? 100 : 0),
            Weekly = BalanceHttp.MoneyValue("充值余额", paid, currency),
            ModelUsage = BalanceHttp.MoneyModels(("可用余额", total), ("充值余额", paid), ("赠金余额", granted)),
            TotalDays = 0,
            ActiveDays = 0,
            FetchedAt = DateTimeOffset.Now,
        };
    }
}

/// <summary>Moonshot / Kimi API balance client.</summary>
public sealed class MoonshotClient : IPlatformClient
{
    public string PlatformId => "moonshot";
    private static readonly HttpClient Http = BalanceHttp.Create();

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        var token = credential.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("未配置 Moonshot API Key。");
        var host = BalanceHttp.NormalizeHost(baseUrl, "https://api.moonshot.cn/v1");
        using var doc = await BalanceHttp.GetJsonAsync(Http, $"{host}/users/me/balance", token);
        var root = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
        var available = BalanceHttp.Num(root, "available_balance", "availableBalance") ?? 0;
        var cash = BalanceHttp.Num(root, "cash_balance", "cashBalance") ?? 0;
        var voucher = BalanceHttp.Num(root, "voucher_balance", "voucherBalance") ?? 0;
        var total = Math.Max(available, cash + voucher);
        return new UsageResult
        {
            Level = $"余额 ¥{available:F2}",
            FiveHour = BalanceHttp.MoneyValue("可用余额", available, "CNY", total > 0 ? Math.Clamp(available / total * 100, 0, 100) : 100),
            Weekly = BalanceHttp.MoneyValue("现金余额", cash, "CNY"),
            ModelUsage = BalanceHttp.MoneyModels(("现金余额", cash), ("代金券", voucher), ("总额度", total)),
            TotalDays = 0,
            ActiveDays = 0,
            FetchedAt = DateTimeOffset.Now,
        };
    }
}

/// <summary>ElevenLabs subscription usage client.</summary>
public sealed class ElevenLabsClient : IPlatformClient
{
    public string PlatformId => "elevenlabs";
    private static readonly HttpClient Http = BalanceHttp.Create();

    public async Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true)
    {
        var token = credential.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("未配置 ElevenLabs API Key。");
        var host = BalanceHttp.NormalizeHost(baseUrl, "https://api.elevenlabs.io/v1");
        using var doc = await BalanceHttp.GetJsonAsync(Http, $"{host}/user/subscription", token, "xi-api-key");
        var root = doc.RootElement;
        var chars = BalanceHttp.Num(root, "character_count") ?? 0;
        var charLimit = BalanceHttp.Num(root, "character_limit") ?? 0;
        var voices = BalanceHttp.Num(root, "voice_slots_used") ?? 0;
        var voiceLimit = BalanceHttp.Num(root, "voice_limit") ?? 0;
        var reset = BalanceHttp.Num(root, "next_character_count_reset_unix");
        return new UsageResult
        {
            Level = BalanceHttp.Str(root, "tier") ?? BalanceHttp.Str(root, "status") ?? "ElevenLabs",
            FiveHour = BalanceHttp.CountQuota("字符额度", chars, charLimit, reset),
            Weekly = BalanceHttp.CountQuota("Voice Slots", voices, voiceLimit, null),
            ModelUsage = new List<ModelUsageSummary> { new() { Model = "Characters", TotalTokens = (long)chars }, new() { Model = "Voices", TotalTokens = (long)voices } },
            TotalDays = 30,
            ActiveDays = 0,
            FetchedAt = DateTimeOffset.Now,
        };
    }
}

internal static class BalanceHttp
{
    public static HttpClient Create() => SharedHttp.Create(TimeSpan.FromSeconds(30));

    public static async Task<JsonDocument> GetJsonAsync(HttpClient http, string url, string token, string authHeader = "Authorization")
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation(authHeader, authHeader.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ? $"Bearer {token}" : token);
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor/1.0");
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        AppLogger.Verbose($"GET {url} -> HTTP {(int)resp.StatusCode}: {BalanceHttp.Truncate(body)}");
        if ((int)resp.StatusCode is 401 or 403) throw new HttpRequestException("凭据无效、已过期或权限不足。");
        if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"请求失败 (HTTP {(int)resp.StatusCode})。");
        return JsonDocument.Parse(body);
    }

    public static string NormalizeHost(string baseUrl, string fallback)
    {
        var b = string.IsNullOrWhiteSpace(baseUrl) ? fallback : baseUrl.Trim();
        if (!b.StartsWith("http", StringComparison.OrdinalIgnoreCase)) b = "https://" + b;
        var ub = new UriBuilder(b);
        var port = ub.Uri.IsDefaultPort ? string.Empty : ":" + ub.Port;
        var path = ub.Uri.AbsolutePath.TrimEnd('/');
        return SharedHttp.EnsureHttps($"{ub.Scheme}://{ub.Host}{port}{path}");
    }

    public static QuotaInfo MoneyQuota(string label, double used, double total, string currency)
    {
        var pct = total > 0 ? used / total * 100 : 0;
        return new QuotaInfo
        {
            Kind = QuotaKind.Weekly,
            DisplayType = label,
            Percentage = Math.Clamp(pct, 0, 100),
            CurrentUsage = used,
            Total = total > 0 ? total : null,
            Remaining = total > 0 ? Math.Max(0, total - used) : null,
        };
    }

    public static QuotaInfo MoneyValue(string label, double value, string currency, double percentage = 0)
    {
        return new QuotaInfo
        {
            Kind = QuotaKind.Other,
            DisplayType = label,
            Percentage = Math.Clamp(percentage, 0, 100),
            CurrentUsage = value,
            Remaining = value,
        };
    }

    public static string CurrencySymbol(string currency) => currency.ToUpperInvariant() switch
    {
        "CNY" => "¥",
        "USD" => "$",
        _ => currency + " ",
    };

    public static QuotaInfo CountQuota(string label, double used, double total, double? resetUnix)
    {
        var pct = total > 0 ? used / total * 100 : 0;
        return new QuotaInfo
        {
            Kind = QuotaKind.FiveHour,
            DisplayType = label,
            Percentage = Math.Clamp(pct, 0, 100),
            CurrentUsage = used,
            Total = total > 0 ? total : null,
            Remaining = total > 0 ? Math.Max(0, total - used) : null,
            NextResetTimeMs = resetUnix is > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)resetUnix.Value).ToUnixTimeMilliseconds() : null,
        };
    }

    public static List<ModelUsageSummary> MoneyModels(params (string Name, double? Value)[] values)
        => values.Where(v => v.Value is > 0).Select(v => new ModelUsageSummary { Model = v.Name, TotalTokens = (long)Math.Round(v.Value!.Value * 100) }).ToList();

    public static string Truncate(string value, int max = 12000)
        => value.Length <= max ? value : value[..max] + "...<truncated>";

    public static double? DeepNum(JsonElement e, string objectName, string propertyName)
        => e.TryGetProperty(objectName, out var o) && o.ValueKind == JsonValueKind.Object ? Num(o, propertyName) : null;

    public static double? Num(JsonElement e, params string[] names)
    {
        foreach (var n in names)
        {
            if (!e.TryGetProperty(n, out var p)) continue;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return d;
            if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
        }
        return null;
    }

    public static string? Str(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String) return p.GetString();
        return null;
    }

    public static bool? Bool(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False) return p.GetBoolean();
        return null;
    }
}
