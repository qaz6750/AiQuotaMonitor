using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT.Interop;

namespace AiQuotaMonitor.Helpers;

/// <summary>
/// 主题与窗口外观助手：启用 Mica/亚克力背景、跟随系统暗色模式、
/// 让标题栏配色随主题变化。WinUI 3 默认即跟随系统主题（ElementTheme.Default），
/// 这里主要负责更现代化的视觉效果。
/// </summary>
public static class ThemeHelper
{
    /// <summary>为窗口启用 Mica 背景（Win11 22000+）。失败时静默回退。</summary>
    public static void ApplyMicaBackdrop(Window window)
    {
        try
        {
            // WinAppSDK 1.4+ 的简化 API
            window.SystemBackdrop = new MicaBackdrop
            {
                Kind = MicaKind.BaseAlt,
            };
        }
        catch
        {
            // 旧版本或不支持时回退到亚克力
            try
            {
                window.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            catch
            {
                // 完全不支持时保持默认背景
            }
        }
    }

    /// <summary>从 Window 获取其底层 AppWindow（通过 HWND 互操作）。</summary>
    public static Microsoft.UI.Windowing.AppWindow? GetAppWindow(Window window)
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(window);
            var wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>把标题栏按钮背景设为透明，融入 Mica。</summary>
    public static void ExtendTitleBar(Window window)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow is null) return;

        try
        {
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            ApplyTitleBarButtonColors(window);
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>主题切换后重新计算标题栏按钮颜色（暗色白字、亮色深字）。</summary>
    public static void UpdateTitleBarTheme(Window window) => ApplyTitleBarButtonColors(window);

    private static void ApplyTitleBarButtonColors(Window window)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow is null) return;
        try
        {
            var tb = appWindow.TitleBar;
            var fe = window.Content as FrameworkElement;
            var dark = fe is not null && IsDark(fe);

            // 背景透明融入 Mica；hover/pressed 用半透明覆盖
            tb.ButtonBackgroundColor = Colors.Transparent;
            tb.ButtonInactiveBackgroundColor = Colors.Transparent;
            tb.ButtonHoverBackgroundColor = dark
                ? Windows.UI.Color.FromArgb(28, 255, 255, 255)
                : Windows.UI.Color.FromArgb(22, 0, 0, 0);
            tb.ButtonPressedBackgroundColor = dark
                ? Windows.UI.Color.FromArgb(44, 255, 255, 255)
                : Windows.UI.Color.FromArgb(44, 0, 0, 0);

            // 前景色随主题：暗色白字、亮色深字，否则按钮在透明背景上不可见
            var fg = dark ? Colors.White : Windows.UI.Color.FromArgb(255, 0x20, 0x20, 0x20);
            tb.ButtonForegroundColor = fg;
            tb.ButtonHoverForegroundColor = fg;
            tb.ButtonPressedForegroundColor = fg;
            tb.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 0x9C, 0xA3, 0xAF);
        }
        catch (Exception ex) { AppLogger.Swallowed("TitleBar", ex); }
    }

    /// <summary>获取当前应用实际生效的主题（解析 Default 后的真实值）。</summary>
    public static ElementTheme ActualTheme(FrameworkElement element)
    {
        if (element.RequestedTheme == ElementTheme.Default)
        {
            return Application.Current.RequestedTheme == ApplicationTheme.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }
        return element.RequestedTheme;
    }

    /// <summary>当前是否为暗色主题。</summary>
    public static bool IsDark(FrameworkElement element) => ActualTheme(element) == ElementTheme.Dark;
}
