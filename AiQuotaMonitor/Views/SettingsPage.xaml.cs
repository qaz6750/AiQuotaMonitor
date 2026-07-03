using AiQuotaMonitor.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AiQuotaMonitor.Views;

/// <summary>设置页：多账号管理、Plan 类型、刷新偏好。</summary>
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        this.InitializeComponent();
        ViewModel = new SettingsViewModel();
        DataContext = ViewModel;
        Unloaded += (_, _) => ViewModel.Detach();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.ApplyNavigationParameter(e.Parameter);
    }
}
