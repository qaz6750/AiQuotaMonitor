using AiQuotaMonitor.ViewModels;
using CommunityToolkit.Mvvm.Input;
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
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ViewModel.EditingApiKey) && CredentialBox.Password != ViewModel.EditingApiKey)
            {
                CredentialBox.Password = ViewModel.EditingApiKey;
            }
        };
        // 页面已开启 NavigationCacheMode=Enabled（常驻），ViewModel 与单例服务同生命周期，
        // 不在 Unloaded 取消订阅，避免切回后收不到 AccountsChanged 导致账号列表不刷新。
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.ApplyNavigationParameter(e.Parameter);
        CredentialBox.Password = ViewModel.EditingApiKey;
    }

    private async void SaveAccount_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.EditingApiKey = CredentialBox.Password;
        if (ViewModel.SaveAccountCommand is IAsyncRelayCommand command)
        {
            await command.ExecuteAsync(null);
        }
    }
}
