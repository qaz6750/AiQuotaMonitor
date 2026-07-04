using AiQuotaMonitor.Helpers;
using Microsoft.UI.Xaml.Media;

namespace AiQuotaMonitor.Models;

/// <summary>总览页单个账号的用量概况。</summary>
public sealed class AccountSummaryItem
{
    public GlmAccount Account { get; init; } = new();

    // 原始数据
    public long TodayTokens { get; set; }
    public double FiveHourPct { get; set; }
    public double WeeklyPct { get; set; }
    public long PrimaryUsed { get; set; }
    public long PrimaryLimit { get; set; }
    public long SecondaryUsed { get; set; }
    public long SecondaryLimit { get; set; }
    public double EstimatedCostCny { get; set; }

    // 是否 Token Plan（显示 token 量而非百分比）
    public bool IsTokenPlan => Account.PlanType == PlanType.Token;
    public bool IsPayAsYouGoPlan => Account.PlanType == PlanType.PayAsYouGo;

    // 展示文本
    public string TodayTokensText => TodayTokens > 0 ? Formatters.FormatTokens(TodayTokens) : "—";
    public string CostHint => EstimatedCostCny > 0 ? $"≈ {Formatters.FormatCost(EstimatedCostCny)}" : "费用待换算";
    public string UsageDensityText => TodayTokens > 0
        ? $"今日 {TodayTokensText} · {CostHint}"
        : HasError ? "刷新失败，点击进入查看" : "今日暂无用量";
    public string FiveHourPctText => $"{FiveHourPct:F0}%";
    public string WeeklyPctText => $"{WeeklyPct:F0}%";

    // 主/次配额显示（Token Plan 显示 token 量，Coding Plan 显示百分比）
    public string PrimaryDisplay => IsTokenPlan && PrimaryLimit > 0
        ? $"{Formatters.FormatTokens(PrimaryUsed)}"
        : IsPayAsYouGoPlan
            ? TodayTokensText
        : FiveHourPctText;
    public string SecondaryDisplay => IsTokenPlan && SecondaryLimit > 0
        ? $"{Formatters.FormatTokens(SecondaryUsed)}"
        : IsPayAsYouGoPlan
            ? "按量"
        : WeeklyPctText;
    public string PrimaryLabel => Account.Provider.Capabilities.PrimaryQuotaLabel.Replace("额度", "");
    public string SecondaryLabel => Account.Provider.Capabilities.SecondaryQuotaLabel.Replace("额度", "");

    public SolidColorBrush BarBrush { get; set; } = new(Windows.UI.Color.FromArgb(255, 0x9C, 0xA3, 0xAF));
    public bool HasError { get; set; }

    public string ProviderName => Account.Provider.Name;
    public string ProviderGlyph => Account.Provider.Glyph;
    public string ProviderIconGlyph => Account.Provider.IconGlyph;
    public string ProviderColor => Account.Provider.BrandColor;
    public string Name => Account.DisplayLabel;
    public string PlanBadge => Account.PlanBadge;
}
