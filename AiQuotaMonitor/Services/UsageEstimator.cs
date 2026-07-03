using AiQuotaMonitor.Helpers;

namespace AiQuotaMonitor.Services;

/// <summary>各配额窗口的总时长（毫秒）。</summary>
public static class Durations
{
    public const long FiveHourMs = 5L * 60 * 60 * 1000;
    public const long WeeklyMs = 7L * 24 * 60 * 60 * 1000;
    public const long MonthlyMs = 30L * 24 * 60 * 60 * 1000;
}

/// <summary>用量预估结果：基于当前消耗速率推算窗口结束前是否会超额、何时耗尽。</summary>
public sealed record UsageEstimate(
    bool WillExceed,
    double ProjectedPercentage,
    DateTimeOffset? EstimatedExhaustTime,
    string? TimeToExhaust);

/// <summary>
/// 用量预估：移植自参考插件 usageEstimate.ts。
/// 仅在 50% ≤ percentage &lt; 100% 时估算（参考实现的同样阈值）。
/// </summary>
public static class UsageEstimator
{
    public static UsageEstimate? Calculate(double percentage, long? nextResetTimeMs, long totalDurationMs)
    {
        if (nextResetTimeMs is not long reset || percentage <= 0) return null;
        if (percentage < 50 || percentage >= 100) return null;

        var now = Formatters.NowUnixMs();
        var elapsed = totalDurationMs - (reset - now);
        if (elapsed <= 0) return null;

        var projected = percentage / elapsed * totalDurationMs;

        var msToExhaust = projected > 0
            ? totalDurationMs * 100.0 / projected - elapsed
            : 0;

        var willExceed = projected > 95;
        var exhaustAt = msToExhaust > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(now + (long)msToExhaust)
            : (DateTimeOffset?)null;
        var tth = msToExhaust > 0
            ? Formatters.FormatDuration((long)msToExhaust)
            : "已超额";

        return new UsageEstimate(willExceed, projected, exhaustAt, tth);
    }

    public static UsageEstimate? For5Hour(double pct, long? reset)
        => Calculate(pct, reset, Durations.FiveHourMs);

    public static UsageEstimate? ForWeekly(double pct, long? reset)
        => Calculate(pct, reset, Durations.WeeklyMs);

    public static UsageEstimate? ForMonthly(double pct, long? reset)
        => Calculate(pct, reset, Durations.MonthlyMs);
}
