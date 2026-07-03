using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Helpers;

/// <summary>
/// 文本格式化工具（移植自参考插件，本地化为中文，移除 VS Code l10n 依赖）。
/// </summary>
public static class Formatters
{
    private static readonly string[] WeekdayNames = { "日", "一", "二", "三", "四", "五", "六" };
    private static readonly string[] WeekdayShort = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

    /// <summary>把 Unix 毫秒时间戳转为本地 DateTime。</summary>
    public static DateTime FromUnixMs(long ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;

    /// <summary>当前本地时间对应的 Unix 毫秒时间戳。</summary>
    public static long NowUnixMs() =>
        new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();

    /// <summary>把重置时间格式化为「倒计时 (绝对时间)」。</summary>
    public static string FormatResetTime(long? nextResetTimeMs, string? quotaType = null)
    {
        if (nextResetTimeMs is not long ts || ts <= 0) return "N/A";

        var d = FromUnixMs(ts);
        var now = DateTime.Now;
        var diff = ts - NowUnixMs();

        string countdown;
        if (diff <= 0)
        {
            countdown = "重置中";
        }
        else
        {
            var totalMinutes = (int)(diff / 60000);
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            var seconds = (int)((diff % 60000) / 1000);

            if (quotaType == QuotaTypes.FiveHour)
            {
                countdown = $"{hours}h {minutes}m";
            }
            else if (quotaType == QuotaTypes.Weekly)
            {
                var days = hours / 24;
                var remainHours = hours % 24;
                countdown = days > 0 ? $"{days}d {remainHours}h" : $"{remainHours}h {minutes}m";
            }
            else if (quotaType == QuotaTypes.Mcp)
            {
                var days = hours / 24;
                var remainHours = hours % 24;
                countdown = days > 0 ? $"{days}d {remainHours}h" : $"{hours}h {minutes}m";
            }
            else
            {
                var parts = new List<string>();
                if (hours > 0) parts.Add($"{hours}h");
                if (minutes > 0) parts.Add($"{minutes}m");
                parts.Add($"{seconds}s");
                countdown = string.Join(" ", parts);
            }
        }

        var isToday = d.Year == now.Year && d.Month == now.Month && d.Day == now.Day;
        var timeStr = isToday
            ? $"{d.Hour:00}:{d.Minute:00}"
            : $"{d.Month:00}-{d.Day:00} {WeekdayShort[(int)d.DayOfWeek]} {d.Hour:00}:{d.Minute:00}";

        return $"{countdown} ({timeStr})";
    }

    /// <summary>把毫秒时长格式化为「Xd Yh / Xh Ym」。</summary>
    public static string FormatDuration(long ms)
    {
        if (ms <= 0) return "已超额";
        var totalMinutes = ms / 60000;
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        var days = hours / 24;
        var remainHours = hours % 24;

        if (days > 0) return $"{days}d {remainHours}h";
        if (hours > 0) return $"{hours}h {minutes}m";
        return $"{minutes}m";
    }

    /// <summary>紧凑的剩余时间（如 3.2d / 1.5h / 45m）。</summary>
    public static string FormatRemainingCompact(long? nextResetTimeMs)
    {
        if (nextResetTimeMs is not long ts) return "--";
        var diff = ts - NowUnixMs();
        if (diff <= 0) return "0m";

        var totalMinutes = diff / 60000.0;
        var hours = totalMinutes / 60.0;
        var days = hours / 24.0;

        if (days >= 1) return $"{days:F1}d";
        if (hours >= 1) return $"{hours:F1}h";
        return $"{(int)totalMinutes}m";
    }

    /// <summary>token 数量缩写（M / K）。</summary>
    public static string FormatTokens(double tokens)
    {
        if (tokens >= 1_000_000) return $"{tokens / 1_000_000.0:F2}M";
        if (tokens >= 1000) return $"{tokens / 1000.0:F1}K";
        return tokens.ToString("F0");
    }

    /// <summary>金额格式化（人民币）。</summary>
    public static string FormatCost(double cny) => $"¥{cny:F2}";

    /// <summary>百分比格式化。</summary>
    public static string FormatPercent(double pct) => $"{pct:F1}%";

    /// <summary>绝对时间（YYYY-MM-DD HH:mm）。</summary>
    public static string FormatAbsolute(DateTimeOffset dto)
    {
        var d = dto.LocalDateTime;
        return $"{d:yyyy-MM-dd HH:mm}";
    }

    /// <summary>「更新于」相对时间。</summary>
    public static string FormatUpdatedAgo(DateTimeOffset fetchedAt)
    {
        var diff = DateTimeOffset.Now - fetchedAt;
        if (diff.TotalSeconds < 60) return "刚刚";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} 分钟前";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} 小时前";
        return FormatAbsolute(fetchedAt);
    }

    /// <summary>把「YYYY-MM-DD HH:mm」拆分为图表可用的短标签。</summary>
    public static string FormatChartLabel(string t, bool isEnd = false)
    {
        var parts = t.Split(' ');
        if (parts.Length < 2) return t;
        var tp = parts[1].Split(':');
        if (tp.Length < 2 || !int.TryParse(tp[0], out var hour) || !int.TryParse(tp[1], out var minute))
            return t;
        var formatted = $"{hour}:{minute:00}";
        if (isEnd)
        {
            var now = DateTime.Now;
            if (now.Hour == hour) return $"{now.Hour:00}:{now.Minute:00}";
        }
        return formatted;
    }

    /// <summary>把日期标签（YYYY-MM-DD ...）转为「MM-DD」。</summary>
    public static string FormatDayLabel(string t)
    {
        var parts = t.Split(' ');
        if (parts.Length < 1) return t;
        var dp = parts[0].Split('-');
        if (dp.Length >= 3 && int.TryParse(dp[1], out var m) && int.TryParse(dp[2], out var d))
            return $"{m:00}-{d:00}";
        return t;
    }
}
