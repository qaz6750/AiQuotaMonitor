using AiQuotaMonitor.Helpers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT.Interop;

namespace AiQuotaMonitor.Views;

/// <summary>
/// 主窗口：自定义标题栏 + NavigationView（概览/统计/设置）+ 桌面悬浮窗入口。
/// </summary>
public sealed partial class MainWindow : Window
{
    public Frame ContentFrame => NavFrame;

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "AI Quota Monitor";
        ThemeHelper.ApplyMicaBackdrop(this);
        ThemeHelper.ExtendTitleBar(this);
        try { this.SetTitleBar(AppTitleBar); } catch { }
        SizeAndCenter(1600, 1000);
    }

    private void SizeAndCenter(int w, int h)
    {
        var appWindow = ThemeHelper.GetAppWindow(this);
        if (appWindow is null) return;
        try
        {
            appWindow.Resize(new SizeInt32(w, h));
            var hWnd = WindowNative.GetWindowHandle(this);
            var wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var display = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Nearest);
            if (display is not null)
            {
                var wa = display.WorkArea;
                appWindow.Move(new PointInt32(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2));
            }
        }
        catch { /* 忽略 */ }
    }

    /// <summary>导航到指定页面并同步左侧选中项。</summary>
    public void Navigate(Type page, object? parameter = null)
    {
        if (NavFrame.CurrentSourcePageType != page || parameter is not null)
        {
            NavFrame.Navigate(page, parameter);
        }

        var tag = page == typeof(OverviewPage) ? "overview"
                : page == typeof(StatsPage) ? "stats"
                : page == typeof(SettingsPage) ? "settings"
                : null;
        if (tag is null) return;

        foreach (var item in AllNavItems().OfType<NavigationViewItem>())
        {
            if (item.Tag is string t && t == tag)
            {
                if (!ReferenceEquals(NavView.SelectedItem, item))
                {
                    NavView.SelectedItem = item;
                }
                break;
            }
        }
    }

    private System.Collections.Generic.IEnumerable<object> AllNavItems()
    {
        foreach (var i in NavView.MenuItems) yield return i;
        foreach (var i in NavView.FooterMenuItems) yield return i;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            var page = tag switch
            {
                "overview" => typeof(OverviewPage),
                "stats" => typeof(StatsPage),
                "settings" => typeof(SettingsPage),
                _ => (Type?)null,
            };
            if (page is not null && NavFrame.CurrentSourcePageType != page)
            {
                NavFrame.Navigate(page);
            }
        }
    }

}
