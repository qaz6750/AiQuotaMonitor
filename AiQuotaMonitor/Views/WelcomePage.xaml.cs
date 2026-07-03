using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiQuotaMonitor.Views;

/// <summary>首次启动欢迎页：直接引导用户去设置页添加账号。</summary>
public sealed partial class WelcomePage : Page
{
    public WelcomePage()
    {
        this.InitializeComponent();
    }

    private void GoSetup_Click(object sender, RoutedEventArgs e)
        => App.MainWindowNavigate(typeof(SettingsPage), "new");
}
