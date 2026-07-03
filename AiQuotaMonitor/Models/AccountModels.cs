using System.Text.Json.Serialization;

namespace AiQuotaMonitor.Models;

/// <summary>套餐计费类型。Coding = 订阅制（5h/周/MCP 配额），Token = 按量计费。</summary>
public enum PlanType
{
    Coding,
    Token,
}

/// <summary>PlanType 的展示与解析辅助。</summary>
public static class PlanTypeExtensions
{
    public static string DisplayName(this PlanType t) => t switch
    {
        PlanType.Coding => "Coding",
        PlanType.Token => "Token",
        _ => t.ToString(),
    };

    public static string Description(this PlanType t) => t switch
    {
        PlanType.Coding => "订阅制配额",
        PlanType.Token => "按量计费",
        _ => string.Empty,
    };

    public static string Glyph(this PlanType t) => t switch
    {
        PlanType.Coding => "\uE734",
        PlanType.Token => "\uE9F2",
        _ => "\uE9F2",
    };

    /// <summary>从持久化字符串还原。</summary>
    public static PlanType Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return PlanType.Coding;
        return s.Trim().Equals("Token", StringComparison.OrdinalIgnoreCase)
            ? PlanType.Token
            : PlanType.Coding;
    }

    /// <summary>持久化编码。</summary>
    public static string Encode(PlanType t) => t == PlanType.Token ? "Token" : "Coding";
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
        ? $"账号 {Id[..Math.Min(6, Id.Length)]}"
        : Name;

    public string PlanBadge => PlanType == PlanType.Token ? "Token" : "Coding";

    /// <summary>用于下拉等处展示的「名称 (Plan)」组合文本。</summary>
    public string DisplayWithPlan => $"{Provider.Name} · {DisplayLabel} · {PlanBadge}";
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
