using AiQuotaMonitor.Services;
using AiQuotaMonitor.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiQuotaMonitor;

/// <summary>
/// 应用程序入口。Unpackaged WinUI 3 桌面应用，运行时无需 MSIX 打包。
/// </summary>
public partial class App : Application
{
    private static MainWindow? _mainWindow;

    /// <summary>主窗口实例（全局单例，供主题刷新、对话框宿主等使用）。</summary>
    public static MainWindow MainWindow => _mainWindow!;

    /// <summary>当前应用内容根 Frame，供页面导航使用。</summary>
    public static Frame RootFrame { get; private set; } = null!;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow();
        RootFrame = _mainWindow.ContentFrame;

        _mainWindow.Activate();

        // 应用上次保存的主题
        try
        {
            var t = SettingsService.Instance.AppTheme;
            var em = t switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
            if (_mainWindow.Content is FrameworkElement fe) fe.RequestedTheme = em;
        }
        catch (Exception ex) { AppLogger.Swallowed("主题应用", ex); }

        // 启动时导航：已有账号进入概览页，否则进入欢迎页选择套餐类型
        var startPage = SettingsService.Instance.HasAccounts
            ? typeof(OverviewPage)
            : typeof(WelcomePage);
        _mainWindow.Navigate(startPage);
    }

    /// <summary>便捷导航入口（其他模块通过 App.MainWindowNavigate 跳转）。</summary>
    public static void MainWindowNavigate(Type pageType, object? parameter = null)
    {
        _mainWindow?.Navigate(pageType, parameter);
    }
}
