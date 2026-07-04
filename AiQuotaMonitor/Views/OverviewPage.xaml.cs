using AiQuotaMonitor.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiQuotaMonitor.Views;

/// <summary>概览页：今日用量概括 + 配额 + KPI。详细统计见统计页。</summary>
public sealed partial class OverviewPage : Page
{
    public OverviewViewModel ViewModel { get; }

    public OverviewPage()
    {
        this.InitializeComponent();
        ViewModel = new OverviewViewModel();
        DataContext = ViewModel;
        // 页面常驻（NavigationCacheMode=Enabled），订阅随 ViewModel 持久保留，不在 Unloaded 取消订阅。
    }
}
