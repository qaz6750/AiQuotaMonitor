namespace AiQuotaMonitor.Models;

/// <summary>
/// 提供商能力标志——控制 UI 显隐与数据展示逻辑。
/// 每个提供商声明自己支持哪些功能，UI 据此决定显示什么。
/// </summary>
public sealed class ProviderCapabilities
{
    /// <summary>是否有今日用量（按小时趋势图）。</summary>
    public bool HasTodayUsage { get; init; } = true;
    /// <summary>是否有用量趋势（7天/30天）。</summary>
    public bool HasTrend { get; init; } = true;
    /// <summary>是否有重置时间。</summary>
    public bool HasResetTime { get; init; } = true;
    /// <summary>是否有耗尽预估。</summary>
    public bool HasEstimate { get; init; } = true;
    /// <summary>是否有等价花费。</summary>
    public bool HasCost { get; init; } = true;
    /// <summary>是否有 MCP 月度。</summary>
    public bool HasMcp { get; init; } = true;
    /// <summary>是否 Cookie 鉴权（非 API Key）。</summary>
    public bool IsCookieAuth { get; init; } = false;
    /// <summary>是否支持本机自动发现凭据（如 gh CLI token）。</summary>
    public bool SupportsCredentialAutoFetch { get; init; } = false;
    /// <summary>凭据标签（"API Key" 或 "Cookie"）。</summary>
    public string CredentialLabel { get; init; } = "API Key";
    /// <summary>主配额标签（"5 小时额度" 或 "套餐额度"）。</summary>
    public string PrimaryQuotaLabel { get; init; } = "5 小时额度";
    /// <summary>次配额标签（"周额度" 或 "月度额度"）。</summary>
    public string SecondaryQuotaLabel { get; init; } = "周额度";
    /// <summary>进度环中心标签。</summary>
    public string RingCenterLabel { get; init; } = "5H";
}

/// <summary>服务提供商描述符。</summary>
public sealed class ProviderDescriptor
{
    public string Id { get; init; } = "glm";
    public string Name { get; init; } = string.Empty;
    public string Glyph { get; init; } = "G";
    public string BrandColor { get; init; } = "#5B8DEF";
    public string DefaultBaseUrl { get; init; } = "https://open.bigmodel.cn";
    public string? DocsUrl { get; init; }
    public bool IsCustom { get; init; }
    public PlanType SupportedPlan { get; init; } = PlanType.Coding;
    public ProviderCapabilities Capabilities { get; init; } = new();
}

/// <summary>已注册的提供商集合。</summary>
public static class Providers
{
    /// <summary>智谱 GLM（Coding Plan，API Key 鉴权）。</summary>
    public static readonly ProviderDescriptor Glm = new()
    {
        Id = "glm",
        Name = "智谱 GLM",
        Glyph = "G",
        BrandColor = "#0EA5E9",
        DefaultBaseUrl = "https://open.bigmodel.cn",
        DocsUrl = "https://open.bigmodel.cn",
        SupportedPlan = PlanType.Coding,
        Capabilities = new ProviderCapabilities(),
    };

    /// <summary>小米 MiMo（Token Plan，Cookie 鉴权）。</summary>
    public static readonly ProviderDescriptor MiMo = new()
    {
        Id = "mimo",
        Name = "小米 MiMo",
        Glyph = "M",
        BrandColor = "#FF6900",
        DefaultBaseUrl = "https://platform.xiaomimimo.com",
        DocsUrl = "https://platform.xiaomimimo.com/console/plan-manage",
        SupportedPlan = PlanType.Token,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = false,
            HasTrend = false,
            HasResetTime = false,
            HasEstimate = false,
            HasCost = false,
            HasMcp = false,
            IsCookieAuth = true,
            CredentialLabel = "Cookie",
            PrimaryQuotaLabel = "套餐额度",
            SecondaryQuotaLabel = "月度额度",
            RingCenterLabel = "套餐",
        },
    };

    /// <summary>Kimi Code（Coding Plan / 订阅配额，API Key 鉴权）。</summary>
    public static readonly ProviderDescriptor Kimi = new()
    {
        Id = "kimi",
        Name = "Kimi Code",
        Glyph = "K",
        BrandColor = "#7C3AED",
        DefaultBaseUrl = "https://api.kimi.com/coding/v1/usages",
        DocsUrl = "https://www.kimi.com/code/console",
        SupportedPlan = PlanType.Coding,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = false,
            HasTrend = false,
            HasMcp = false,
            HasCost = false,
            CredentialLabel = "API Key / OAuth Token",
            PrimaryQuotaLabel = "5 小时额度",
            SecondaryQuotaLabel = "周额度",
            RingCenterLabel = "5H",
        },
    };

    /// <summary>MiniMax Token Plan（订阅 Key，API Key 鉴权）。</summary>
    public static readonly ProviderDescriptor MiniMax = new()
    {
        Id = "minimax",
        Name = "MiniMax Token Plan",
        Glyph = "X",
        BrandColor = "#10B981",
        DefaultBaseUrl = "https://api.minimaxi.com/v1/token_plan/remains",
        DocsUrl = "https://platform.minimaxi.com",
        SupportedPlan = PlanType.Token,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = false,
            HasTrend = false,
            HasMcp = false,
            HasCost = false,
            CredentialLabel = "Subscription Key",
            PrimaryQuotaLabel = "5 小时额度",
            SecondaryQuotaLabel = "周额度",
            RingCenterLabel = "5H",
        },
    };

    /// <summary>GitHub Copilot（Premium requests / Chat / Completions 配额）。</summary>
    public static readonly ProviderDescriptor Copilot = new()
    {
        Id = "copilot",
        Name = "GitHub Copilot",
        Glyph = "C",
        BrandColor = "#24292F",
        DefaultBaseUrl = "https://api.github.com/copilot_internal/user",
        DocsUrl = "https://github.com/settings/tokens",
        SupportedPlan = PlanType.Coding,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = false,
            HasTrend = false,
            HasCost = false,
            HasEstimate = false,
            SupportsCredentialAutoFetch = true,
            CredentialLabel = "GitHub Token",
            PrimaryQuotaLabel = "Premium Requests",
            SecondaryQuotaLabel = "Chat 配额",
            RingCenterLabel = "PR",
        },
    };

    /// <summary>Factory Droid（Standard / Premium token 用量）。</summary>
    public static readonly ProviderDescriptor Factory = new()
    {
        Id = "factory",
        Name = "Factory Droid",
        Glyph = "F",
        BrandColor = "#F59E0B",
        DefaultBaseUrl = "https://app.factory.ai",
        DocsUrl = "https://app.factory.ai",
        SupportedPlan = PlanType.PayAsYouGo,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = false,
            HasTrend = false,
            HasCost = false,
            HasEstimate = false,
            HasMcp = false,
            CredentialLabel = "Factory API Key / Bearer Token",
            PrimaryQuotaLabel = "Standard Tokens",
            SecondaryQuotaLabel = "Premium Tokens",
            RingCenterLabel = "Droid",
        },
    };

    /// <summary>全部已启用的提供商。</summary>
    public static readonly IReadOnlyList<ProviderDescriptor> All = new[] { Glm, Kimi, Copilot, MiMo, MiniMax, Factory };

    /// <summary>按 id 查找。</summary>
    public static ProviderDescriptor GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Glm;
        return All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? Glm;
    }
}
