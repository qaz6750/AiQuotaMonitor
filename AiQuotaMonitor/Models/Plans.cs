namespace AiQuotaMonitor.Models;

/// <summary>
/// GLM Coding Plan 的某一档订阅套餐（Lite / Pro / Max）。
/// 数据源自智谱官方监控接口在 Pro 档返回的基准值：
///   Pro 5h = 59,304,317 tokens / 527 calls；
///   周配额 = 5h × 5；Lite = Pro ÷ 5；Max = Pro × 4。
/// </summary>
public sealed class PlanQuota
{
    public string Level { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>5 小时窗口 token 配额。</summary>
    public long Tokens5h { get; init; }

    /// <summary>5 小时窗口调用次数配额。</summary>
    public int Calls5h { get; init; }

    /// <summary>周窗口 token 配额。</summary>
    public long TokensWeekly { get; init; }

    /// <summary>周窗口调用次数配额。</summary>
    public int CallsWeekly { get; init; }
}

/// <summary>GLM Coding Plan 各档位配额常量表。</summary>
public static class GlmPlans
{
    public static readonly PlanQuota Lite = new()
    {
        Level = "Lite",
        DisplayName = "Lite",
        Tokens5h = 11_860_863,
        Calls5h = 105,
        TokensWeekly = 59_304_317,
        CallsWeekly = 527,
    };

    public static readonly PlanQuota Pro = new()
    {
        Level = "Pro",
        DisplayName = "Pro",
        Tokens5h = 59_304_317,
        Calls5h = 527,
        TokensWeekly = 296_521_585,
        CallsWeekly = 2_635,
    };

    public static readonly PlanQuota Max = new()
    {
        Level = "Max",
        DisplayName = "Max",
        Tokens5h = 237_217_268,
        Calls5h = 2_108,
        TokensWeekly = 1_186_086_340,
        CallsWeekly = 10_540,
    };

    public static readonly IReadOnlyList<PlanQuota> All = new[] { Lite, Pro, Max };

    /// <summary>按档位编码（大小写不敏感）查找套餐。</summary>
    public static PlanQuota? GetByLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level)) return null;
        return All.FirstOrDefault(p => p.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
    }
}
