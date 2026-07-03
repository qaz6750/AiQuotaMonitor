using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;
using AiQuotaMonitor.Services;
using AiQuotaMonitor.Views;

namespace AiQuotaMonitor.ViewModels;

/// <summary>按模型明细行。</summary>
public sealed class ModelBreakdownItem
{
    public string Model { get; set; } = string.Empty;
    public string Color { get; set; } = "#5985F5";
    public long Tokens { get; set; }
    public string TokensText { get; set; } = "—";
    public double Percent { get; set; }
    public string PercentText => $"{Percent:F1}%";
}

/// <summary>
/// 统计页 ViewModel。对应 cc-bar 的 Statistics 窗口：
/// 时间范围分段 + KPI 行 + 按模型堆叠 + 按模型明细表。
/// </summary>
public partial class StatsViewModel : ViewModelBase
{
    private readonly UsageDataService _data = UsageDataService.Instance;

    [ObservableProperty] private TrendRange _range = TrendRange.SevenDays;
    [ObservableProperty] private TrendSeries? _trend;
    [ObservableProperty] private string _tokensText = "—";
    [ObservableProperty] private string _callsText = "—";
    [ObservableProperty] private string _costText = "—";
    [ObservableProperty] private string _rangeLabel = "近 7 天";
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private DateTimeOffset _rangeStart = DateTimeOffset.Now.AddDays(-6);
    [ObservableProperty] private DateTimeOffset _rangeEnd = DateTimeOffset.Now;
    [ObservableProperty] private string _rangeResult = "—";
    [ObservableProperty] private bool _isCalculating;
    [ObservableProperty] private int _hourStart;
    [ObservableProperty] private int _hourEnd = 23;
    /// <summary>当前范围是否含小时精度（今日/7天是，30天否）。</summary>
    public bool IsHourly => Range != TrendRange.ThirtyDays;
    /// <summary>是否为近 30 天范围（子范围选择器仅在此显示）。</summary>
    public bool IsRange30 => Range == TrendRange.ThirtyDays;

    public ObservableCollection<ModelBreakdownItem> Breakdown { get; } = new();

    public StatsViewModel()
    {
        _data.Updated += Rebuild;
        Rebuild();
        _ = _data.RefreshAsync();
    }

    /// <summary>页面卸载时取消订阅。</summary>
    public void Detach() => _data.Updated -= Rebuild;

    partial void OnRangeChanged(TrendRange value)
    {
        OnPropertyChanged(nameof(IsHourly));
        OnPropertyChanged(nameof(IsRange30));
        Rebuild();
    }

    [RelayCommand]
    private void SetRange(string key)
    {
        if (Enum.TryParse<TrendRange>(key, true, out var r)) Range = r;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await _data.RefreshAsync();

    [RelayCommand]
    private void GoOverview() => App.MainWindowNavigate(typeof(OverviewPage));

    /// <summary>计算自定义范围内当前账号的 token 合计（今日/7天含小时精度，30天用整天）。</summary>
    [RelayCommand]
    private async Task CalcRangeAsync()
    {
        var acc = SettingsService.Instance.ActiveAccount;
        if (acc is null || !acc.HasKey) { RangeResult = "请先配置账号"; return; }
        if (RangeEnd < RangeStart) { RangeResult = "结束需晚于开始"; return; }
        IsCalculating = true;
        try
        {
            if (PlatformClientFactory.Get(acc) is not GlmClient glm)
            { RangeResult = "当前提供商暂不支持自定义范围计算"; return; }
            var s = IsHourly
                ? RangeStart.LocalDateTime.Date.AddHours(Math.Clamp(HourStart, 0, 23))
                : RangeStart.LocalDateTime;
            var e = IsHourly
                ? RangeEnd.LocalDateTime.Date.AddHours(Math.Clamp(HourEnd, 0, 23))
                : RangeEnd.LocalDateTime.AddDays(1).AddSeconds(-1);
            var data = await glm.QueryRangeModelUsageAsync(acc.ApiKey, acc.BaseUrl, s, e, SettingsService.Instance.EnableRetry);
            var total = (long)(data?.TotalUsage?.TotalTokensUsage ?? data?.TokensUsage?.Sum(v => v ?? 0) ?? 0);
            RangeResult = $"{Formatters.FormatTokens(total)} tokens";
        }
        catch (Exception ex) { RangeResult = "计算失败：" + ex.Message; }
        finally { IsCalculating = false; }
    }

    /// <summary>点击统计图柱子 → 设为起始日期（仅当用户已选中起始点时才更新）。</summary>
    public void ApplyBarToRangeStart(int idx)
    {
        // 仅当用户已操作过起始日期（非默认值）时才允许点柱更新，避免误触
        if (RangeStart.Date != DateTimeOffset.Now.AddDays(-6).Date) return;
        if (Trend?.XTime is null || idx < 0 || idx >= Trend.XTime.Count) return;
        if (DateTime.TryParse(Trend.XTime[idx], out var d))
        {
            RangeStart = new DateTimeOffset(d);
            if (IsHourly) HourStart = d.Hour;
        }
    }

    private void Rebuild()
    {
        var r = _data.Current;
        HasData = r is not null;
        Trend = Range switch
        {
            TrendRange.ThirtyDays => r?.Trend30d,
            TrendRange.Today => OverviewExtractToday(r?.Trend7d),
            _ => r?.Trend7d,
        };

        Breakdown.Clear();

        if (Trend is null)
        {
            TokensText = "—";
            CallsText = "—";
            CostText = "—";
            return;
        }

        TokensText = Formatters.FormatTokens((long)Trend.TotalTokens);
        CallsText = ((long)Trend.TotalCalls).ToString("N0");
        RangeLabel = Range switch
        {
            TrendRange.ThirtyDays => "近 30 天",
            TrendRange.Today => "今日",
            _ => "近 7 天",
        };

        var cost = CostCalculator.EstimateFromTrend(Trend);
        CostText = cost is not null ? Formatters.FormatCost(cost.TotalCny) : "—";

        if (Trend.PerModel is not null)
        {
            var total = Trend.PerModel.Sum(p => (double)p.TotalTokens);
            foreach (var p in Trend.PerModel.OrderByDescending(x => x.TotalTokens))
            {
                Breakdown.Add(new ModelBreakdownItem
                {
                    Model = p.Model,
                    Color = p.Color,
                    Tokens = p.TotalTokens,
                    TokensText = Formatters.FormatTokens(p.TotalTokens),
                    Percent = total > 0 ? p.TotalTokens * 100.0 / total : 0,
                });
            }
        }
    }

    /// <summary>从近 7 天小时趋势中截取今天的数据点（与 OverviewViewModel.ExtractToday 等价）。</summary>
    /// <summary>构建今日全天 24 小时用量（0–23 点，未到的时段补 0）。</summary>
    private static TrendSeries? OverviewExtractToday(TrendSeries? t)
    {
        if (t?.XTime is null) return null;
        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        var byHour = new Dictionary<int, int>();
        for (var i = 0; i < t.XTime.Count; i++)
        {
            if (t.XTime[i].StartsWith(todayKey, StringComparison.Ordinal) && TryGetHour(t.XTime[i], out var h))
                byHour[h] = i;
        }
        var xTime = new List<string>();
        var yValue = new List<double?>();
        var modelCall = new List<double?>();
        List<ModelTrendSeries>? per = t.PerModel?.Select(p => new ModelTrendSeries
        {
            Model = p.Model, Color = p.Color,
            YValue = new List<double?>(), CallCount = new List<double?>(), TotalTokens = 0,
        }).ToList();
        for (var hr = 0; hr < 24; hr++)
        {
            xTime.Add($"{todayKey} {hr:00}:00");
            if (byHour.TryGetValue(hr, out var idx))
            {
                yValue.Add(idx < t.YValue.Count ? t.YValue[idx] : 0);
                modelCall.Add(idx < t.ModelCallCount.Count ? t.ModelCallCount[idx] : 0);
                if (per is not null)
                    for (var m = 0; m < per.Count; m++)
                    {
                        var src = t.PerModel![m];
                        var v = idx < src.YValue.Count ? (src.YValue[idx] ?? 0) : 0;
                        per[m].YValue.Add(v);
                        per[m].CallCount.Add(idx < src.CallCount.Count ? (src.CallCount[idx] ?? 0) : 0);
                        per[m].TotalTokens += (long)v;
                    }
            }
            else
            {
                yValue.Add(0); modelCall.Add(0);
                per?.ForEach(p => { p.YValue.Add(0); p.CallCount.Add(0); });
            }
        }
        return new TrendSeries
        {
            XTime = xTime, YValue = yValue, ModelCallCount = modelCall, PerModel = per,
            TotalTokens = yValue.Sum(v => v ?? 0), TotalCalls = modelCall.Sum(v => v ?? 0),
        };
    }

    private static bool TryGetHour(string xtime, out int hour)
    {
        hour = 0;
        return xtime.Length >= 13 && (xtime[10] == ' ' || xtime[10] == 'T') && int.TryParse(xtime.AsSpan(11, 2), out hour);
    }
}
