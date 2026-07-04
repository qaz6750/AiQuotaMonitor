using System.Text.Json.Serialization;

namespace AiQuotaMonitor.Models;

/// <summary>套餐计费类型。Coding = 订阅制（5h/周/MCP 配额），Token = Token 套餐，PayAsYouGo = API 按量付费。</summary>
public enum PlanType
{
    Coding,
    Token,
    PayAsYouGo,
}

/// <summary>PlanType 的展示与解析辅助。</summary>
public static class PlanTypeExtensions
{
    public static string DisplayName(this PlanType t) => t switch
    {
        PlanType.Coding => "Coding",
        PlanType.Token => "Token",
        PlanType.PayAsYouGo => "按量付费",
        _ => t.ToString(),
    };

    public static string Description(this PlanType t) => t switch
    {
        PlanType.Coding => "订阅制配额",
        PlanType.Token => "按量计费",
        PlanType.PayAsYouGo => "API 按量付费",
        _ => string.Empty,
    };

    public static string Glyph(this PlanType t) => t switch
    {
        PlanType.Coding => "\uE734",
        PlanType.Token => "\uE9F2",
        PlanType.PayAsYouGo => "\uE8D4",
        _ => "\uE9F2",
    };

    /// <summary>从持久化字符串还原。</summary>
    public static PlanType Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return PlanType.Coding;
        return s.Trim() switch
        {
            var v when v.Equals("Token", StringComparison.OrdinalIgnoreCase) => PlanType.Token,
            var v when v.Equals("PayAsYouGo", StringComparison.OrdinalIgnoreCase) => PlanType.PayAsYouGo,
            var v when v.Equals("PayG", StringComparison.OrdinalIgnoreCase) => PlanType.PayAsYouGo,
            var v when v.Equals("按量付费", StringComparison.OrdinalIgnoreCase) => PlanType.PayAsYouGo,
            _ => PlanType.Coding,
        };
    }

    /// <summary>持久化编码。</summary>
    public static string Encode(PlanType t) => t switch
    {
        PlanType.Token => "Token",
        PlanType.PayAsYouGo => "PayAsYouGo",
        _ => "Coding",
    };
}

/// <summary>运行时账号模型（API Key 已解密，仅存在于内存中）。</summary>
public sealed class GlmAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string ProviderId { get; set; } = "glm";
    /// <summary>该账号的提供商描述符（根据 ProviderId 解析）。</summary>
    public ProviderDescriptor Provider => Providers.GetById(ProviderId);
    public PlanType PlanType { get; set; } = PlanType.Coding;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://open.bigmodel.cn";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);

    public string DisplayLabel => string.IsNullOrWhiteSpace(Name)
        ? CookieIdentityFormatter.DefaultAccountName(this)
        : Name;

    public string PlanBadge => PlanType.DisplayName();

    /// <summary>用于下拉等处展示的「名称 (Plan)」组合文本。</summary>
    public string DisplayWithPlan => $"{Provider.Name} · {DisplayLabel} · {PlanBadge}";
}

/// <summary>从 Cookie 型凭据中提取可展示的账号标识，避免设置页只显示随机账号名。</summary>
public static class CookieIdentityFormatter
{
    private static readonly string[] PreferredCookieNames =
    {
        "email", "account", "username", "user_name", "nickname", "name", "userId", "user_id", "uid",
    };

    public static string DefaultAccountName(GlmAccount account)
    {
        var provider = account.Provider;
        if (provider.Capabilities.IsCookieAuth || provider.Capabilities.SupportsCredentialAutoFetch)
        {
            if (TryGetCookieIdentity(account.ApiKey) is { Length: > 0 } identity)
            {
                return identity;
            }
            return $"{provider.Name} 网页账号";
        }
        return $"账号 {account.Id[..Math.Min(6, account.Id.Length)]}";
    }

    public static string CredentialHint(GlmAccount account)
    {
        var provider = account.Provider;
        if ((provider.Capabilities.IsCookieAuth || provider.Capabilities.SupportsCredentialAutoFetch) && LooksLikeCookie(account.ApiKey))
        {
            var identity = TryGetCookieIdentity(account.ApiKey);
            return string.IsNullOrWhiteSpace(identity) ? "网页 Cookie · 已保存" : $"网页 Cookie · {identity}";
        }
        return account.HasKey ? "••••" + account.ApiKey[^Math.Min(4, account.ApiKey.Length)..] : "未配置";
    }

    public static string? TryGetCookieIdentity(string? cookieHeader)
    {
        if (string.IsNullOrWhiteSpace(cookieHeader) || !LooksLikeCookie(cookieHeader)) return null;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0 || idx == part.Length - 1) continue;
            map[part[..idx].Trim()] = Uri.UnescapeDataString(part[(idx + 1)..].Trim());
        }
        foreach (var key in PreferredCookieNames)
        {
            if (map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return MaskIfEmail(value);
        }
        return null;
    }

    private static bool LooksLikeCookie(string value) => value.Contains('=') && value.Contains(';');

    private static string MaskIfEmail(string value)
    {
        var at = value.IndexOf('@');
        if (at <= 1) return value;
        var name = value[..at];
        return name.Length <= 2 ? "**" + value[at..] : name[0] + new string('*', Math.Min(6, name.Length - 2)) + name[^1] + value[at..];
    }
}

/// <summary>账号持久化记录（API Key 经 DPAPI 加密）。</summary>
public sealed class AccountRecord
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("providerId")] public string ProviderId { get; set; } = "glm";
    [JsonPropertyName("planType")] public string PlanType { get; set; } = "Coding";
    [JsonPropertyName("apiKeyEnc")] public string? ApiKeyEnc { get; set; }
    [JsonPropertyName("baseUrl")] public string? BaseUrl { get; set; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
}
