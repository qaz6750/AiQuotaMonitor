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
    /// <summary>是否支持通过内置网页登录自动抓取 Cookie。</summary>
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
    public string LogoText { get; init; } = "G";
    public double LogoFontSize { get; init; } = 12;
    public string LogoPath { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = "\uE8D4";
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
        LogoText = "GLM",
        LogoFontSize = 10,
        IconGlyph = "\uE8D4",
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
        Glyph = "米",
        LogoText = "Mi",
        LogoFontSize = 12,
        LogoPath = "ms-appx:///Assets/Logos/xiaomi.svg",
        IconGlyph = "\uE946",
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
        LogoText = "Kimi",
        LogoFontSize = 9,
        IconGlyph = "\uE943",
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
        LogoText = "Mini",
        LogoFontSize = 8.5,
        LogoPath = "ms-appx:///Assets/Logos/minimax.svg",
        IconGlyph = "\uE9F2",
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

    /// <summary>Factory Droid（Standard / Premium token 用量）。</summary>
    public static readonly ProviderDescriptor Factory = new()
    {
        Id = "factory",
        Name = "Factory Droid",
        Glyph = "F",
        LogoText = "F△",
        LogoFontSize = 11,
        IconGlyph = "\uE99A",
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
            SupportsCredentialAutoFetch = true,
            CredentialLabel = "Factory API Key / Bearer Token / Cookie",
            PrimaryQuotaLabel = "Standard Tokens",
            SecondaryQuotaLabel = "Premium Tokens",
            RingCenterLabel = "Droid",
        },
    };

    /// <summary>OpenAI GPT（组织用量 / 费用 API，需要 Admin Key）。</summary>
    public static readonly ProviderDescriptor Gpt = new()
    {
        Id = "gpt",
        Name = "OpenAI GPT",
        Glyph = "AI",
        LogoText = "GPT",
        LogoFontSize = 10,
        LogoPath = "ms-appx:///Assets/Logos/openai.svg",
        IconGlyph = "\uE8F1",
        BrandColor = "#10A37F",
        DefaultBaseUrl = "https://api.openai.com",
        DocsUrl = "https://platform.openai.com/settings/organization/admin-keys",
        SupportedPlan = PlanType.PayAsYouGo,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = true,
            HasTrend = true,
            HasResetTime = false,
            HasEstimate = false,
            HasMcp = false,
            CredentialLabel = "OpenAI Admin Key",
            PrimaryQuotaLabel = "今日 Tokens",
            SecondaryQuotaLabel = "近 7 天费用",
            RingCenterLabel = "GPT",
        },
    };

    /// <summary>Anthropic Claude（组织 Usage/Cost Report，需要 Admin Key）。</summary>
    public static readonly ProviderDescriptor Claude = new()
    {
        Id = "claude",
        Name = "Anthropic Claude",
        Glyph = "C",
        LogoText = "Claude",
        LogoFontSize = 7.5,
        LogoPath = "ms-appx:///Assets/Logos/anthropic.svg",
        IconGlyph = "\uE90A",
        BrandColor = "#D97757",
        DefaultBaseUrl = "https://api.anthropic.com",
        DocsUrl = "https://console.anthropic.com/settings/admin-keys",
        SupportedPlan = PlanType.PayAsYouGo,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = true,
            HasTrend = true,
            HasResetTime = false,
            HasEstimate = false,
            HasMcp = false,
            CredentialLabel = "Anthropic Admin Key",
            PrimaryQuotaLabel = "今日 Tokens",
            SecondaryQuotaLabel = "近 7 天费用",
            RingCenterLabel = "Claude",
        },
    };

    /// <summary>OpenRouter（API Key 余额 + Key spend）。</summary>
    public static readonly ProviderDescriptor OpenRouter = new()
    {
        Id = "openrouter",
        Name = "OpenRouter",
        Glyph = "OR",
        LogoText = "OR",
        LogoFontSize = 11,
        LogoPath = "ms-appx:///Assets/Logos/openrouter.svg",
        IconGlyph = "\uE774",
        BrandColor = "#6D5EF6",
        DefaultBaseUrl = "https://openrouter.ai/api/v1",
        DocsUrl = "https://openrouter.ai/settings/keys",
        SupportedPlan = PlanType.PayAsYouGo,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = false,
            HasTrend = false,
            HasResetTime = false,
            HasEstimate = false,
            HasMcp = false,
            CredentialLabel = "OpenRouter API Key",
            PrimaryQuotaLabel = "Credits Used",
            SecondaryQuotaLabel = "Monthly Spend",
            RingCenterLabel = "OR",
        },
    };

    /// <summary>DeepSeek（API Key 余额接口）。</summary>
    public static readonly ProviderDescriptor DeepSeek = new()
    {
        Id = "deepseek",
        Name = "DeepSeek",
        Glyph = "DS",
        LogoText = "DS",
        LogoFontSize = 11,
        LogoPath = "ms-appx:///Assets/Logos/deepseek.svg",
        IconGlyph = "\uE8D2",
        BrandColor = "#4F8BFF",
        DefaultBaseUrl = "https://api.deepseek.com",
        DocsUrl = "https://platform.deepseek.com/api_keys",
        SupportedPlan = PlanType.PayAsYouGo,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = false,
            HasTrend = false,
            HasResetTime = false,
            HasEstimate = false,
            HasMcp = false,
            CredentialLabel = "DeepSeek API Key",
            PrimaryQuotaLabel = "账户余额",
            SecondaryQuotaLabel = "已充值余额",
            RingCenterLabel = "DS",
        },
    };

    /// <summary>Moonshot / Kimi API（API Key 余额接口）。</summary>
    public static readonly ProviderDescriptor Moonshot = new()
    {
        Id = "moonshot",
        Name = "Moonshot / Kimi API",
        Glyph = "月",
        LogoText = "月",
        LogoFontSize = 14,
        IconGlyph = "\uE708",
        BrandColor = "#111827",
        DefaultBaseUrl = "https://api.moonshot.ai/v1",
        DocsUrl = "https://platform.moonshot.ai/console/api-keys",
        SupportedPlan = PlanType.PayAsYouGo,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = false,
            HasTrend = false,
            HasResetTime = false,
            HasEstimate = false,
            HasMcp = false,
            CredentialLabel = "Moonshot API Key",
            PrimaryQuotaLabel = "可用余额",
            SecondaryQuotaLabel = "现金余额",
            RingCenterLabel = "Kimi",
        },
    };

    /// <summary>ElevenLabs（API Key 订阅字符额度）。</summary>
    public static readonly ProviderDescriptor ElevenLabs = new()
    {
        Id = "elevenlabs",
        Name = "ElevenLabs",
        Glyph = "11",
        LogoText = "11",
        LogoFontSize = 11,
        LogoPath = "ms-appx:///Assets/Logos/elevenlabs.svg",
        IconGlyph = "\uE189",
        BrandColor = "#111111",
        DefaultBaseUrl = "https://api.elevenlabs.io/v1",
        DocsUrl = "https://elevenlabs.io/app/settings/api-keys",
        SupportedPlan = PlanType.PayAsYouGo,
        Capabilities = new ProviderCapabilities
        {
            HasTodayUsage = false,
            HasTrend = false,
            HasEstimate = false,
            HasMcp = false,
            CredentialLabel = "ElevenLabs API Key",
            PrimaryQuotaLabel = "字符额度",
            SecondaryQuotaLabel = "Voice Slots",
            RingCenterLabel = "11",
        },
    };

    /// <summary>全部已启用的提供商。</summary>
    public static readonly IReadOnlyList<ProviderDescriptor> All = new[] { Glm, Kimi, MiMo, MiniMax, Factory, Gpt, Claude, OpenRouter, DeepSeek, Moonshot, ElevenLabs };

    /// <summary>按 id 查找。</summary>
    public static ProviderDescriptor GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Glm;
        return All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? Glm;
    }
}
