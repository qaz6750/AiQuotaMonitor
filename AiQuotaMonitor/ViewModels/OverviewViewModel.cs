using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;
using AiQuotaMonitor.Models.Api;
using AiQuotaMonitor.Services;
using AiQuotaMonitor.Views;
using Microsoft.UI.Xaml.Media;

namespace AiQuotaMonitor.ViewModels;

/// <summary>
/// 概览页 ViewModel。对应 cc-bar 的 Popover 总览：
/// 服务 tile + plan + 重置倒计时 + 56pt 5h 进度环 + weekly 细条 + tokens/spend 双列 + KPI + 趋势。
/// </summary>
public partial class OverviewViewModel : ViewModelBase
{
    private readonly UsageDataService _data = UsageDataService.Instance;
    private readonly SettingsService _settings = SettingsService.Instance;
    private TrendRange _trendRange = TrendRange.Today;
    private GlmAccount? _selectedAccount;
    private bool _syncingAccounts;

    /// <summary>可切换的账号列表（概览页头部收纳下拉）。</summary>
    public ObservableCollection<GlmAccount> Accounts { get; } = new();

    /// <summary>当前账号的套餐类型（用于 Token Plan 提示等）。</summary>
    public PlanType ActivePlanType => _settings.ActivePlanType;
    public bool IsTokenPlan => ActivePlanType == PlanType.Token;
    public bool IsPayAsYouGoPlan => ActivePlanType == PlanType.PayAsYouGo;

    // ===== 多账号概览 =====
    public ObservableCollection<AccountSummaryItem> Summaries { get; } = new();
    [ObservableProperty] private string _totalTodayTokensText = "—";
    [ObservableProperty] private int _accountCount;
    [ObservableProperty] private bool _isLoadingAll;

    /// <summary>当前选中账号；切换时写入 SettingsService 并触发数据刷新。</summary>
    public GlmAccount? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (_syncingAccounts)
            {
                _selectedAccount = value;
                OnPropertyChanged();
                return;
            }
            if (!SetProperty(ref _selectedAccount, value) || value is null) return;
            if (_settings.SetActive(value.Id))
            {
                UsageDataService.Instance.StartAutoRefresh();
                _ = UsageDataService.Instance.RefreshAsync();
            }
        }
    }

    public OverviewViewModel()
    {
        _data.Updated += OnUpdated;
        _data.LoadingChanged += OnLoadingChanged;
        _settings.AccountsChanged += OnAccountsChanged;
        RefreshAccounts();
        OnUpdated();
        OnLoadingChanged();
        _ = LoadSummariesAsync();
    }

    private void OnAccountsChanged() => RefreshAccounts();

    private void RefreshAccounts()
    {
        _syncingAccounts = true;
        try
        {
            Accounts.Clear();
            foreach (var a in _settings.Accounts) Accounts.Add(a);
            var active = _settings.ActiveAccount;
            if (_selectedAccount?.Id != active?.Id)
            {
                _selectedAccount = active;
                OnPropertyChanged(nameof(SelectedAccount));
            }
            OnPropertyChanged(nameof(ActivePlanType));
            OnPropertyChanged(nameof(IsTokenPlan));
            OnPropertyChanged(nameof(IsPayAsYouGoPlan));
            OnPropertyChanged(nameof(PrimaryQuotaLabel));
            OnPropertyChanged(nameof(SecondaryQuotaLabel));
            OnPropertyChanged(nameof(Caps));
            OnPropertyChanged(nameof(HasTodayUsage));
            OnPropertyChanged(nameof(HasResetTime));
            OnPropertyChanged(nameof(HasMcp));
            OnPropertyChanged(nameof(HasCost));
            OnPropertyChanged(nameof(HasTrend));
            OnPropertyChanged(nameof(RingCenterLabel));
            HasNoAccount = !_settings.HasAccounts;
        }
        finally { _syncingAccounts = false; }
    }

    /// <summary>页面卸载时取消事件订阅，避免多实例累积。</summary>
    public void Detach()
    {
        _data.Updated -= OnUpdated;
        _data.LoadingChanged -= OnLoadingChanged;
        _settings.AccountsChanged -= OnAccountsChanged;
    }

    // ===== 状态 =====
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _lastUpdatedText = "尚未刷新";
    [ObservableProperty] private string _statusText = "离线";
    [ObservableProperty] private SolidColorBrush _statusBrush = new(Windows.UI.Color.FromArgb(255, 0xB6, 0xB6, 0xB6));
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _hasData;

    // ===== 服务信息 =====
    [ObservableProperty] private string _levelText = "—";
    [ObservableProperty] private string _serviceName = "AI 服务用量";

    /// <summary>当前活跃账号的能力标志。</summary>
    public ProviderCapabilities Caps => _settings.ActiveAccount?.Provider.Capabilities ?? new();
    public string PrimaryQuotaLabel => Caps.PrimaryQuotaLabel;
    public string SecondaryQuotaLabel => Caps.SecondaryQuotaLabel;
    public string RingCenterLabel => Caps.RingCenterLabel;
    public bool HasTodayUsage => Caps.HasTodayUsage;
    public bool HasResetTime => Caps.HasResetTime;
    public bool HasMcp => _data.Current?.Mcp is not null && Caps.HasMcp;
    public bool HasCost => Caps.HasCost;
    public bool HasTrend => Caps.HasTrend;
    private bool IsCookieProvider => Caps.IsCookieAuth;

    // ===== 5 小时 =====
    [ObservableProperty] private double _fiveHourPct;
    [ObservableProperty] private string _fiveHourPctText = "—";
    [ObservableProperty] private string _fiveHourResetText = "N/A";
    [ObservableProperty] private SolidColorBrush _fiveHourBrush = new(Windows.UI.Color.FromArgb(255, 0x3F, 0xB9, 0x50));
    [ObservableProperty] private string? _fiveHourEstimateText;

    // ===== 周 =====
    [ObservableProperty] private double _weeklyPct;
    [ObservableProperty] private string _weeklyPctText = "—";
    [ObservableProperty] private string _weeklyResetText = "N/A";
    [ObservableProperty] private SolidColorBrush _weeklyBrush = new(Windows.UI.Color.FromArgb(255, 0x3F, 0xB9, 0x50));
    [ObservableProperty] private string _weeklyTokensText = "—";
    [ObservableProperty] private string _weeklyCapText = "—";
    [ObservableProperty] private string? _weeklyEstimateText;

    // ===== MCP 月度 =====
    [ObservableProperty] private double _mcpPct;
    [ObservableProperty] private string _mcpPctText = "—";
    [ObservableProperty] private string _mcpUsageText = "—";
    [ObservableProperty] private string _mcpTotalText = "—";
    [ObservableProperty] private string _mcpRemainingText = "—";
    [ObservableProperty] private string _mcpResetText = "N/A";
    [ObservableProperty] private SolidColorBrush _mcpBrush = new(Windows.UI.Color.FromArgb(255, 0x9C, 0xA3, 0xAF));

    // ===== 等价花费 =====
    [ObservableProperty] private string _costText = "—";
    [ObservableProperty] private string _costWindowLabel = "";
    [ObservableProperty] private bool _costHasFallback;
    [ObservableProperty] private string _costBreakdownText = "等待用量数据";
    [ObservableProperty] private string _costFormulaText = "按模型 token × 单价估算，OpenAI/Claude 使用官方费用优先。";

    // ===== KPI =====
    [ObservableProperty] private string _todayTokensText = "—";
    [ObservableProperty] private string _todayCallsText = "—";
    [ObservableProperty] private string _activeDaysText = "—";

    // ===== 趋势 =====
    [ObservableProperty] private TrendSeries? _trendToday;
    [ObservableProperty] private TrendSeries? _trend7d;
    [ObservableProperty] private TrendSeries? _trend30d;

    // ===== 自定义时间范围计算 =====
    [ObservableProperty] private DateTimeOffset _rangeStart = DateTimeOffset.Now.AddDays(-6);
    [ObservableProperty] private DateTimeOffset _rangeEnd = DateTimeOffset.Now;
    [ObservableProperty] private string _customRangeResult = "—";
    [ObservableProperty] private bool _isCalculating;
    [ObservableProperty] private TrendSeries? _customRangeTrend;
    [ObservableProperty] private bool _hasNoAccount;
    [ObservableProperty] private TrendSeries? _combinedTrend;

    // ===== 小时范围计算（今日用量图：起止小时） =====
    [ObservableProperty] private int _hourStart;
    [ObservableProperty] private int _hourEnd = 23;
    [ObservableProperty] private string _hourRangeResult = "—";

    public TrendRange TrendRange
    {
        get => _trendRange;
        set
        {
            if (SetProperty(ref _trendRange, value))
            {
                OnPropertyChanged(nameof(CurrentTrend));
                OnPropertyChanged(nameof(CurrentTrendLabel));
            }
        }
    }

    public TrendSeries? CurrentTrend =>
        _trendRange switch
        {
            TrendRange.SevenDays => Trend7d,
            TrendRange.ThirtyDays => Trend30d,
            _ => TrendToday,
        };

    public string CurrentTrendLabel =>
        _trendRange switch
        {
            TrendRange.SevenDays => "近 7 天",
            TrendRange.ThirtyDays => "近 30 天",
            _ => "今日",
        };

    // ===== 命令 =====
    [RelayCommand]
    private async Task LoadAsync()
    {
        _data.StartAutoRefresh();
        await _data.RefreshAsync();
    }

    /// <summary>并发拉取所有账号的今日用量概况，用于多账号概览。</summary>
    private async Task LoadSummariesAsync()
    {
        var accounts = _settings.Accounts.ToList();
        AccountCount = accounts.Count;
        if (accounts.Count == 0)
        {
            Summaries.Clear();
            TotalTodayTokensText = "—";
            return;
        }

        IsLoadingAll = true;
        Summaries.Clear();
        foreach (var a in accounts) Summaries.Add(new AccountSummaryItem { Account = a });

        using var gate = new SemaphoreSlim(3);
        var results = await Task.WhenAll(accounts.Select(async a =>
        {
            if (!a.HasKey) return (a, (UsageResult?)null);
            await gate.WaitAsync();
            try
            {
                // 活跃账号复用 UsageDataService 缓存，避免重复查询
                if (a.Id == _settings.ActiveAccount?.Id && _data.Current is { } cached) return (a, cached);
                return (a, await PlatformClientFactory.Get(a).QueryUsageAsync(a.ApiKey, a.BaseUrl, _settings.EnableRetry));
            }
            catch (Exception ex) { AppLogger.Swallowed("LoadSummaries", ex); return (a, (UsageResult?)null); }
            finally { gate.Release(); }
        }));

        long total = 0;
        foreach (var (a, r) in results)
        {
            var item = Summaries.FirstOrDefault(s => s.Account.Id == a.Id);
            if (item is null) continue;
            if (r is not null)
            {
                var today = ExtractToday(r.Trend7d);
                item.TodayTokens = (long)(today?.TotalTokens ?? 0);
                item.FiveHourPct = r.FiveHour?.Percentage ?? 0;
                item.PrimaryUsed = (long)(r.FiveHour?.CurrentUsage ?? 0);
                item.PrimaryLimit = (long)(r.FiveHour?.Total ?? 0);
                item.WeeklyPct = r.Weekly?.Percentage ?? 0;
                item.SecondaryUsed = (long)(r.Weekly?.CurrentUsage ?? 0);
                item.SecondaryLimit = (long)(r.Weekly?.Total ?? 0);
                item.EstimatedCostCny = (a.PlanType == PlanType.PayAsYouGo && r.Weekly?.CurrentUsage is double c && c > 0)
                    ? c
                    : CostCalculator.EstimateFromTrend(r.Trend7d)?.TotalCny ?? 0;
                item.BarBrush = ColorHelper.ToBrush(ColorHelper.GetQuotaColor(Math.Max(item.FiveHourPct, item.WeeklyPct)));
                total += item.TodayTokens;
            }
            else
            {
                item.HasError = true;
            }
        }
        TotalTodayTokensText = Formatters.FormatTokens(total);

        // AccountSummaryItem 非 Observable，重建集合触发 UI 刷新
        var snapshot = Summaries.ToList();
        Summaries.Clear();
        foreach (var s in snapshot) Summaries.Add(s);
        CombinedTrend = BuildCombinedTrend(results);
        IsLoadingAll = false;
    }

    /// <summary>把所有账号近 3 天的小时用量合并为按账号堆叠的趋势（不同账号不同颜色）。</summary>
    private static TrendSeries? BuildCombinedTrend(IEnumerable<(GlmAccount Account, UsageResult? Result)> results)
    {
        const int Hours = 3 * 24;
        var series = new List<ModelTrendSeries>();
        List<string>? xTime = null;
        foreach (var (a, r) in results)
        {
            if (r?.Trend7d is not { } t || t.XTime is null || t.XTime.Count == 0) continue;
            var n = t.XTime.Count;
            var take = Math.Min(Hours, n);
            var start = n - take;
            var yv = (t.YValue ?? new List<double?>()).Skip(start).Take(take).ToList();
            while (yv.Count < take) yv.Add(0);
            var mc = (t.ModelCallCount ?? new List<double?>()).Skip(start).Take(take).ToList();
            while (mc.Count < take) mc.Add(0);
            xTime ??= t.XTime.Skip(start).Take(take).ToList();
            // 安全：确保当前账号数据点数与 xTime 对齐
            if (yv.Count > xTime.Count) yv = yv.Take(xTime.Count).ToList();
            if (mc.Count > xTime.Count) mc = mc.Take(xTime.Count).ToList();
            series.Add(new ModelTrendSeries
            {
                Model = a.DisplayLabel,
                Color = a.Provider.BrandColor,
                YValue = yv,
                CallCount = mc,
                TotalTokens = (long)yv.Sum(v => v ?? 0),
            });
        }
        if (xTime is null || series.Count == 0) return null;
        var N = xTime.Count;
        var combinedY = new double?[N];
        foreach (var s in series)
            for (var i = 0; i < N && i < s.YValue.Count; i++)
                combinedY[i] = (combinedY[i] ?? 0) + (s.YValue[i] ?? 0);
        return new TrendSeries
        {
            XTime = xTime,
            YValue = combinedY.ToList(),
            ModelCallCount = combinedY.Select(_ => (double?)0).ToList(),
            PerModel = series,
            TotalTokens = series.Sum(s => s.TotalTokens),
            TotalCalls = 0,
        };
    }

    /// <summary>计算今日 [HourStart, HourEnd] 小时区间的 token 合计。</summary>
    [RelayCommand]
    private void CalcHourRange()
    {
        if (TrendToday?.YValue is null || TrendToday.YValue.Count == 0)
        {
            HourRangeResult = "今日暂无数据";
            return;
        }
        var yv = TrendToday.YValue;
        var s = Math.Clamp(HourStart, 0, yv.Count - 1);
        var e = Math.Clamp(HourEnd, s, yv.Count - 1);
        var sum = yv.Skip(s).Take(e - s + 1).Sum(v => v ?? 0);
        HourRangeResult = $"{Formatters.FormatTokens((long)sum)} tokens · {s:00}:00–{e:00}:00";
    }

    /// <summary>点击今日图柱子 → 设为起始小时（仅当用户已选中起始点时才更新）。</summary>
    public void ApplyBarToHourStart(int idx)
    {
        // 仅当用户已操作过起始小时（非默认 0）时才允许点柱更新，避免误触
        if (HourStart != 0 && idx >= 0 && idx <= 23) HourStart = idx;
    }

    /// <summary>点击某账号卡片：切换为活跃账号并进入详情页。</summary>
    [RelayCommand]
    private async Task OpenAccountAsync(AccountSummaryItem? item)
    {
        if (item?.Account is null) return;
        if (_settings.SetActive(item.Account.Id))
        {
            UsageDataService.Instance.StartAutoRefresh();
            await UsageDataService.Instance.RefreshAsync();
        }
        App.MainWindowNavigate(typeof(AccountDetailPage));
    }

    /// <summary>从详情页返回总览。</summary>
    [RelayCommand]
    private void GoBack() => App.MainWindowNavigate(typeof(OverviewPage));

    /// <summary>计算自定义时间范围的总 token 用量（基于当前活跃账号），并生成按小时趋势。</summary>
    [RelayCommand]
    private async Task CalculateRangeAsync()
    {
        var acc = _settings.ActiveAccount;
        if (acc is null || !acc.HasKey) { CustomRangeResult = "请先在设置中配置账号"; return; }
        if (RangeEnd < RangeStart) { CustomRangeResult = "结束时间需晚于开始时间"; return; }
        IsCalculating = true;
        try
        {
            if (PlatformClientFactory.Get(acc) is not GlmClient glm)
            { CustomRangeResult = "当前提供商暂不支持自定义范围计算"; return; }
            var s = RangeStart.LocalDateTime;
            var e = RangeEnd.LocalDateTime.AddDays(1).AddSeconds(-1);
            var data = await glm.QueryRangeModelUsageAsync(acc.ApiKey, acc.BaseUrl, s, e, _settings.EnableRetry);
            var total = (long)(data?.TotalUsage?.TotalTokensUsage ?? data?.TokensUsage?.Sum(v => v ?? 0) ?? 0);
            CustomRangeResult = $"{Formatters.FormatTokens(total)} tokens";
            CustomRangeTrend = BuildRangeTrend(data);
        }
        catch (Exception ex) { CustomRangeResult = "计算失败：" + ex.Message; CustomRangeTrend = null; }
        finally { IsCalculating = false; }
    }

    /// <summary>把自定义范围的 model-usage 数据构建为按小时趋势（单序列合计）。</summary>
    private static TrendSeries? BuildRangeTrend(ModelUsageData? d)
    {
        if (d?.XTime is null || d.XTime.Count == 0) return null;
        var n = d.XTime.Count;
        var yv = (d.TokensUsage ?? Enumerable.Repeat<double?>(0, n)).ToList();
        var mc = (d.ModelCallCount ?? Enumerable.Repeat<double?>(0, n)).ToList();
        return new TrendSeries
        {
            XTime = d.XTime,
            YValue = yv,
            ModelCallCount = mc,
            PerModel = new List<ModelTrendSeries>
            {
                new() { Model = "用量", Color = "#0EA5E9", YValue = yv, CallCount = mc, TotalTokens = (long)yv.Sum(v => v ?? 0) }
            },
            TotalTokens = yv.Sum(v => v ?? 0),
            TotalCalls = mc.Sum(v => v ?? 0),
        };
    }

    /// <summary>手动刷新多账号概览。</summary>
    [RelayCommand]
    private async Task RefreshSummariesAsync() => await LoadSummariesAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _data.RefreshAsync();
        _data.StartAutoRefresh();
    }

    [RelayCommand] private void GoStats() => App.MainWindowNavigate(typeof(StatsPage));
    [RelayCommand] private void GoSettings() => App.MainWindowNavigate(typeof(SettingsPage));
    [RelayCommand] private void SetTrendRange(string key)
    {
        if (Enum.TryParse<TrendRange>(key, true, out var r)) TrendRange = r;
    }

    private void OnLoadingChanged()
    {
        IsLoading = _data.IsLoading;
    }

    private void OnUpdated()
    {
        var r = _data.Current;
        HasData = r is not null;
        IsLoading = _data.IsLoading;
        ErrorMessage = _data.LastError;
        HasError = !string.IsNullOrEmpty(_data.LastError);

        // 状态点三态：实时 / 过期 / 离线
        if (HasError && r is null)
        {
            StatusText = "离线";
            StatusBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xF8, 0x51, 0x49));
        }
        else if (_data.LastUpdated is DateTimeOffset lu && (DateTimeOffset.Now - lu).TotalMinutes < 5)
        {
            StatusText = "实时";
            StatusBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x3F, 0xB9, 0x50));
        }
        else
        {
            StatusText = "过期";
            StatusBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xD2, 0x99, 0x22));
        }

        LastUpdatedText = _data.LastUpdated is null
            ? "尚未刷新"
            : $"更新于 {Formatters.FormatUpdatedAgo(_data.LastUpdated.Value)}";

        if (r is null)
        {
            return;
        }

        // 服务名根据提供商动态显示
        var acc = _settings.ActiveAccount;
        ServiceName = acc?.Provider.Name ?? "AI 服务用量";
        LevelText = (r.Level ?? acc?.PlanBadge ?? "—").ToUpperInvariant();

        // 5h（Token Plan 用作"套餐进度"，无重置时间）
        if (r.FiveHour is { } q5)
        {
            FiveHourPct = q5.Percentage;
            FiveHourPctText = Formatters.FormatPercent(q5.Percentage);
            FiveHourResetText = q5.NextResetTimeMs is null ? "—" : Formatters.FormatResetTime(q5.NextResetTimeMs, q5.DisplayType);
            FiveHourBrush = ColorHelper.ToBrush(ColorHelper.GetQuotaColor(q5.Percentage));
            FiveHourEstimateText = q5.NextResetTimeMs is null ? null
                : (UsageEstimator.For5Hour(q5.Percentage, q5.NextResetTimeMs) is { } est
                   ? $"窗口末预计 {Formatters.FormatPercent(est.ProjectedPercentage)} · 约 {est.TimeToExhaust}耗尽"
                   : null);
        }

        // weekly
        if (r.Weekly is { } qw)
        {
            WeeklyPct = qw.Percentage;
            WeeklyPctText = Formatters.FormatPercent(qw.Percentage);
            WeeklyResetText = qw.NextResetTimeMs is null ? "—" : Formatters.FormatResetTime(qw.NextResetTimeMs, qw.DisplayType);
            WeeklyBrush = ColorHelper.ToBrush(ColorHelper.GetQuotaColor(qw.Percentage));

            // Token Plan（MiMo）直接用 QuotaInfo 的 used/limit；Coding Plan 用 GlmPlans 套餐表
            if (IsCookieProvider && qw.Total > 0)
            {
                WeeklyTokensText = Formatters.FormatTokens((long)(qw.CurrentUsage ?? 0));
                WeeklyCapText = Formatters.FormatTokens((long)qw.Total);
            }
            else
            {
                var plan = GlmPlans.GetByLevel(r.Level);
                if (plan is not null)
                {
                    var used = plan.TokensWeekly * qw.Percentage / 100.0;
                    WeeklyTokensText = Formatters.FormatTokens((long)used);
                    WeeklyCapText = Formatters.FormatTokens(plan.TokensWeekly);
                }
            }

            WeeklyEstimateText = qw.NextResetTimeMs is null ? null
                : (UsageEstimator.ForWeekly(qw.Percentage, qw.NextResetTimeMs) is { } west
                   ? $"窗口末预计 {Formatters.FormatPercent(west.ProjectedPercentage)} · 约 {west.TimeToExhaust}耗尽"
                   : null);
        }

        // mcp
        if (r.Mcp is { } qm)
        {
            McpPct = qm.Percentage;
            McpPctText = Formatters.FormatPercent(qm.Percentage);
            McpResetText = Formatters.FormatResetTime(qm.NextResetTimeMs, qm.DisplayType);
            McpBrush = ColorHelper.ToBrush(ColorHelper.GetQuotaColor(qm.Percentage));
            if (qm.Total is double total)
            {
                McpTotalText = total.ToString("F0");
                McpUsageText = (qm.CurrentUsage ?? 0).ToString("F0");
                McpRemainingText = (qm.Remaining ?? (total - (qm.CurrentUsage ?? 0))).ToString("F0");
            }
        }

        // 等价花费（优先近 7 天趋势，其次今日模型汇总）
        if (IsPayAsYouGoPlan && r.Weekly?.CurrentUsage is double officialCost && officialCost > 0)
        {
            CostText = Formatters.FormatCost(officialCost);
            CostWindowLabel = "近 7 天官方费用";
            CostHasFallback = false;
            CostBreakdownText = $"官方费用约 ¥{officialCost:F2}；美元账单按 1 USD ≈ ¥7.25 换算。";
            CostFormulaText = "费用 = 官方 cost_usd × 7.25；token 图按时段展示。";
        }
        else if ((CostCalculator.EstimateFromTrend(r.Trend7d) ?? CostCalculator.EstimateFromModels(r.ModelUsage)) is { } cost)
        {
            CostText = Formatters.FormatCost(cost.TotalCny);
            CostWindowLabel = cost.HasFallback ? "（含未知模型回退价）" : "近 7 天等价计费";
            CostHasFallback = cost.HasFallback;
            CostBreakdownText = string.Join(" · ", cost.PerModel.Take(3).Select(x => $"{x.Model}≈{Formatters.FormatCost(x.CostCny)}"));
            if (string.IsNullOrWhiteSpace(CostBreakdownText)) CostBreakdownText = "无可换算模型明细";
            CostFormulaText = "换算 = tokens / 1,000,000 × (输入价 + 输出价) / 2。";
        }

        // 今日 KPI
        var today = ExtractToday(r.Trend7d);
        TrendToday = today;
        if (today is not null)
        {
            TodayTokensText = Formatters.FormatTokens((long)today.TotalTokens);
            TodayCallsText = ((long)today.TotalCalls).ToString("N0");
        }
        else
        {
            TodayTokensText = "—";
            TodayCallsText = "—";
        }

        Trend7d = r.Trend7d;
        Trend30d = r.Trend30d;
        ActiveDaysText = r.TotalDays > 0 ? $"{r.ActiveDays}/{r.TotalDays} 天" : "—";

        OnPropertyChanged(nameof(CurrentTrend));
        OnPropertyChanged(nameof(CurrentTrendLabel));
        OnPropertyChanged(nameof(ActivePlanType));
        OnPropertyChanged(nameof(IsTokenPlan));
        OnPropertyChanged(nameof(IsPayAsYouGoPlan));
    }

    /// <summary>构建今日全天 24 小时用量（0–23 点，未到的时段补 0）。</summary>
    private static TrendSeries? ExtractToday(TrendSeries? t)
    {
        if (t?.XTime is null) return null;
        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");

        // 小时 → 源索引
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
            Model = p.Model,
            Color = p.Color,
            YValue = new List<double?>(),
            CallCount = new List<double?>(),
            TotalTokens = 0,
        }).ToList();

        for (var hr = 0; hr < 24; hr++)
        {
            xTime.Add($"{todayKey} {hr:00}:00");
            if (byHour.TryGetValue(hr, out var idx))
            {
                yValue.Add(idx < t.YValue.Count ? t.YValue[idx] : 0);
                modelCall.Add(idx < t.ModelCallCount.Count ? t.ModelCallCount[idx] : 0);
                if (per is not null)
                {
                    for (var m = 0; m < per.Count; m++)
                    {
                        var src = t.PerModel![m];
                        var v = idx < src.YValue.Count ? (src.YValue[idx] ?? 0) : 0;
                        per[m].YValue.Add(v);
                        per[m].CallCount.Add(idx < src.CallCount.Count ? (src.CallCount[idx] ?? 0) : 0);
                        per[m].TotalTokens += (long)v;
                    }
                }
            }
            else
            {
                yValue.Add(0);
                modelCall.Add(0);
                per?.ForEach(p => { p.YValue.Add(0); p.CallCount.Add(0); });
            }
        }

        return new TrendSeries
        {
            XTime = xTime,
            YValue = yValue,
            ModelCallCount = modelCall,
            PerModel = per,
            TotalTokens = yValue.Sum(v => v ?? 0),
            TotalCalls = modelCall.Sum(v => v ?? 0),
        };
    }

    private static bool TryGetHour(string xtime, out int hour)
    {
        hour = 0;
        if (xtime.Length >= 13 && (xtime[10] == ' ' || xtime[10] == 'T'))
            return int.TryParse(xtime.AsSpan(11, 2), out hour);
        return false;
    }
}
