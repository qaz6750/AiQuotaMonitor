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
        Unloaded += (_, _) => ViewModel.Detach();
    }
}
