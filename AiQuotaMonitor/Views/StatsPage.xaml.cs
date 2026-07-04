using AiQuotaMonitor.Models;
using AiQuotaMonitor.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiQuotaMonitor.Views;

/// <summary>统计页：时间范围 + KPI + 趋势 + 按模型明细。</summary>
public sealed partial class StatsPage : Page
{
    public StatsViewModel ViewModel { get; }

    public StatsPage()
    {
        this.InitializeComponent();
        ViewModel = new StatsViewModel();
        DataContext = ViewModel;
        SetRangeRadio(ViewModel.Range);
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.Range))
            {
                SetRangeRadio(ViewModel.Range);
            }
        };
        StatsChart.BarTapped += idx => ViewModel.ApplyBarToRangeStart(idx);
        // 页面常驻（NavigationCacheMode=Enabled），订阅随 ViewModel 持久保留，不在 Unloaded 取消订阅。
    }

    private void SetRangeRadio(TrendRange r)
    {
        var target = r switch
        {
            TrendRange.SevenDays => Range7d,
            TrendRange.ThirtyDays => Range30d,
            _ => RangeToday,
        };
        if (!target.IsChecked.GetValueOrDefault()) target.IsChecked = true;
    }

    private void Range_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not RadioButton rb || rb.Tag is not string tag) return;
        var range = tag switch
        {
            "0" => TrendRange.Today,
            "1" => TrendRange.SevenDays,
            "2" => TrendRange.ThirtyDays,
            _ => ViewModel.Range,
        };
        if (ViewModel.Range != range) ViewModel.Range = range;
    }
}
