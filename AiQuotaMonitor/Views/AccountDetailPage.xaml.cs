using AiQuotaMonitor.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiQuotaMonitor.Views;

/// <summary>账号详情页：从总览点击账号卡片进入，展示该账号的完整用量（5h/周/MCP/今日图）。</summary>
public sealed partial class AccountDetailPage : Page
{
    public OverviewViewModel ViewModel { get; }

    public AccountDetailPage()
    {
        this.InitializeComponent();
        ViewModel = new OverviewViewModel();
        DataContext = ViewModel;
        TodayChart.BarTapped += idx => ViewModel.ApplyBarToHourStart(idx);
        Unloaded += (_, _) => ViewModel.Detach();
    }
}
