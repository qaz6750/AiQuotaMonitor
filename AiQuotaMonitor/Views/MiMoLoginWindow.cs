using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiQuotaMonitor.Views;

/// <summary>
/// MiMo 登录窗口：内置 WebView2，用户登录后自动提取 cookie。
/// 使用 WindowsAppSDK 内置 WebView2（无需额外 NuGet 包）。
/// </summary>
public sealed class MiMoLoginWindow : Window
{
    public event Action<string>? CookieReady;

    private readonly Grid _root = new();
    private readonly WebView2 _webview = new();
    private readonly TextBlock _status = new();
    private readonly string _loginUrl;
    private bool _captured;
    private bool _closed;

    public MiMoLoginWindow(string loginUrl)
    {
        _loginUrl = loginUrl;
        Title = "登录获取 Cookie";
        SizeAndCenter(560, 720);

        _status.FontSize = 11;
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Padding = new Thickness(16, 8, 16, 4);
        _status.Text = "正在加载登录页…";

        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_status, 0);
        Grid.SetRow(_webview, 1);
        _root.Children.Add(_status);
        _root.Children.Add(_webview);
        Content = _root;

        _webview.NavigationCompleted += OnNavCompleted;
    }

    public async Task StartAsync()
    {
        try
        {
            _status.Text = "正在初始化…";
            await _webview.EnsureCoreWebView2Async();
            _status.Text = "请在下方登录，登录成功后自动获取 cookie。";
            _webview.Source = new Uri(_loginUrl);
        }
        catch (Exception ex)
        {
            _status.Text = "初始化失败：" + ex.Message + "\n请手动复制 cookie。";
        }
    }

    private async void OnNavCompleted(WebView2 sender, object args)
    {
        var ok = args?.GetType().GetProperty("IsSuccess")?.GetValue(args) as bool? ?? false;
        if (!ok) return;
        await TryCaptureAsync();
    }

    private async Task TryCaptureAsync()
    {
        if (_captured || _webview.CoreWebView2 is null) return;
        try
        {
            // 提取目标域的 cookie
            var uri = new Uri(_loginUrl);
            var cookies = await _webview.CoreWebView2.CookieManager.GetCookiesAsync($"{uri.Scheme}://{uri.Host}");
            // 拼接完整 cookie 字符串
            var cookieStr = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
            if (!string.IsNullOrEmpty(cookieStr))
            {
                _captured = true;
                _status.Text = $"✓ 已获取 {cookies.Count} 个 cookie";
                CookieReady?.Invoke(cookieStr);
                CloseAfterCapture();
            }
        }
        catch { /* 下次导航再试 */ }
    }

    private void CloseAfterCapture()
    {
        if (_closed) return;
        _closed = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                _webview.NavigationCompleted -= OnNavCompleted;
                _webview.Close();
            }
            catch { }
            Close();
        });
    }

    private void SizeAndCenter(int w, int h)
    {
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
            appWindow?.Resize(new Windows.Graphics.SizeInt32(w, h));
        }
        catch { }
    }
}
