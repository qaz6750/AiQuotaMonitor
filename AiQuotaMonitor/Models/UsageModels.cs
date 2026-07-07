namespace AiQuotaMonitor.Models;

/// <summary>归一化后的配额种类。</summary>
public enum QuotaKind
{
    FiveHour,
    Weekly,
    Monthly,
    Mcp,
    Other,
}

/// <summary>归一化后的单个配额信息。</summary>
public sealed class QuotaInfo
{
    public QuotaKind Kind { get; init; } = QuotaKind.Other;

    /// <summary>展示用类型名，如 "Token usage(5 Hour)"。</summary>
    public string DisplayType { get; init; } = string.Empty;

    /// <summary>已用百分比 0-100。</summary>
    public double Percentage { get; init; }

    /// <summary>下次重置时间（Unix 毫秒），可空。</summary>
    public long? NextResetTimeMs { get; init; }

    /// <summary>当前用量或余额值。</summary>
    public double? CurrentUsage { get; init; }

    /// <summary>总额度。</summary>
    public double? Total { get; init; }

    /// <summary>剩余额度。</summary>
    public double? Remaining { get; init; }

    /// <summary>月度额度明细（GLM MCP 等平台使用）。</summary>
    public List<McpUsageDetail>? UsageDetails { get; init; }
}

public sealed class McpUsageDetail
{
    public string ModelCode { get; init; } = string.Empty;
    public double Usage { get; init; }
}

public sealed class ModelUsageSummary
{
    public string Model { get; init; } = string.Empty;
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long TotalTokens { get; init; }
    public int RequestCount { get; init; }
}

/// <summary>用量趋势序列（小时或日维度）。</summary>
public sealed class TrendSeries
{
    public List<string> XTime { get; init; } = new();
    public List<double?> YValue { get; init; } = new();
    public List<double?> ModelCallCount { get; init; } = new();
    public List<ModelTrendSeries>? PerModel { get; init; }
    public double TotalTokens { get; init; }
    public double TotalCalls { get; init; }
}

public sealed class ModelTrendSeries
{
    public string Model { get; init; } = string.Empty;
    public List<double?> YValue { get; init; } = new();
    public List<double?> CallCount { get; init; } = new();
    public long TotalTokens { get; set; }
    public string Color { get; init; } = "#5985F5";
}

public sealed class ToolUsageSummary
{
    public string Tool { get; init; } = string.Empty;
    public int CallCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
}

/// <summary>一次完整查询后的归一化结果。</summary>
public sealed class UsageResult
{
    public QuotaInfo? FiveHour { get; init; }
    public QuotaInfo? Weekly { get; init; }
    public QuotaInfo? Monthly { get; init; }
    public QuotaInfo? Mcp { get; init; }
    public string? Level { get; init; }

    public List<ModelUsageSummary> ModelUsage { get; init; } = new();
    public List<ToolUsageSummary> ToolUsage { get; init; } = new();

    /// <summary>近 7 天（按小时）趋势。</summary>
    public TrendSeries? Trend7d { get; init; }

    /// <summary>近 30 天（按小时）趋势。</summary>
    public TrendSeries? Trend30d { get; init; }

    /// <summary>近 30 天内有使用的天数。</summary>
    public int ActiveDays { get; init; }

    /// <summary>窗口总天数。</summary>
    public int TotalDays { get; init; }

    /// <summary>本次抓取时间。</summary>
    public DateTimeOffset FetchedAt { get; init; }
}
