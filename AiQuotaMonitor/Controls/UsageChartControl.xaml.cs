using System.Text;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Input;

namespace AiQuotaMonitor.Controls;

/// <summary>
/// 按模型堆叠柱状图：每个时间点一根柱，内部按模型颜色堆叠。
/// 对应 cc-bar Statistics 的「按模型堆叠柱状图」。
/// </summary>
public sealed partial class UsageChartControl : UserControl
{
    public static readonly DependencyProperty TrendProperty =
        DependencyProperty.Register(nameof(Trend), typeof(TrendSeries), typeof(UsageChartControl), new PropertyMetadata(null, OnTrendChanged));

    public TrendSeries? Trend { get => (TrendSeries?)GetValue(TrendProperty); set => SetValue(TrendProperty, value); }

    public static readonly DependencyProperty ShowAxisProperty =
        DependencyProperty.Register(nameof(ShowAxis), typeof(bool), typeof(UsageChartControl), new PropertyMetadata(false, OnTrendChanged));

    /// <summary>是否在图表底部绘制时间轴刻度。</summary>
    public bool ShowAxis { get => (bool)GetValue(ShowAxisProperty); set => SetValue(ShowAxisProperty, value); }

    /// <summary>点击某根柱子时触发，参数为该柱在 XTime 中的索引。</summary>
    public event Action<int>? BarTapped;

    public UsageChartControl()
    {
        this.InitializeComponent();
        Loaded += (_, _) => Draw();
        SizeChanged += OnSized;
        PlotCanvas.Tapped += OnPlotTapped;
    }

    private void OnPlotTapped(object sender, TappedRoutedEventArgs e)
    {
        if (Trend?.XTime is null || Trend.XTime.Count == 0) return;
        var p = e.GetPosition(PlotCanvas);
        var leftPad = ShowAxis ? 42.0 : 6.0;
        var plotW = PlotCanvas.ActualWidth - leftPad - 6;
        if (plotW <= 0) return;
        var slot = plotW / Trend.XTime.Count;
        var idx = (int)Math.Round((p.X - leftPad) / slot);
        if (idx >= 0 && idx < Trend.XTime.Count) BarTapped?.Invoke(idx);
    }

    private DispatcherTimer? _debounce;
    private void OnSized(object sender, object e)
    {
        // 防抖：连续尺寸变化只在停顿后重绘一次，降低 CPU/内存峰值
        _debounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _debounce.Stop();
        _debounce.Tick -= DebounceTick;
        _debounce.Tick += DebounceTick;
        _debounce.Start();
    }

    private void DebounceTick(object? sender, object e)
    {
        if (_debounce is not null) _debounce.Stop();
        Draw();
    }

    private static void OnTrendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((UsageChartControl)d).Draw();

    private string? _lastDrawSig;
    private void Draw()
    {
        // 相同数据+尺寸跳过重绘（性能优化）
        var sig = $"{PlotCanvas.ActualWidth:F0}x{PlotCanvas.ActualHeight:F0}|{Trend?.XTime?.Count ?? 0}|{ShowAxis}";
        if (sig == _lastDrawSig) return;
        _lastDrawSig = sig;

        PlotCanvas.Children.Clear();

        if (Trend?.XTime is null || Trend.PerModel is null || Trend.PerModel.Count == 0 || Trend.XTime.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            return;
        }
        EmptyText.Visibility = Visibility.Collapsed;

        var w = PlotCanvas.ActualWidth;
        var h = PlotCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double RightPad = 6, TopPad = 8;
        var leftPad = ShowAxis ? 42.0 : 6.0;
        var bottomPad = ShowAxis ? 18.0 : 6.0;
        var plotW = w - leftPad - RightPad;
        var plotH = h - TopPad - bottomPad;
        if (plotW <= 0 || plotH <= 0) return;

        // 坐标轴与分度网格（ShowAxis 时作为背景层）
        if (ShowAxis)
        {
            var axisBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x6B, 0x72, 0x80));
            var gridBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(45, 0x9C, 0xA3, 0xAF));
            var yAxis = new Rectangle { Width = 1, Height = plotH, Fill = axisBrush };
            Canvas.SetLeft(yAxis, leftPad);
            Canvas.SetTop(yAxis, TopPad);
            PlotCanvas.Children.Add(yAxis);
            var xAxis = new Rectangle { Width = plotW, Height = 1, Fill = axisBrush };
            Canvas.SetLeft(xAxis, leftPad);
            Canvas.SetTop(xAxis, TopPad + plotH);
            PlotCanvas.Children.Add(xAxis);
            var grid = new Rectangle { Width = plotW, Height = 1, Fill = gridBrush };
            Canvas.SetLeft(grid, leftPad);
            Canvas.SetTop(grid, TopPad + plotH / 2);
            PlotCanvas.Children.Add(grid);
        }

        var n = Trend.XTime.Count;
        var slot = plotW / n;
        var barW = Math.Max(1.5, slot * 0.45);

        // 先算出每列合计与调用次数，用于 tooltip 与纵轴归一
        double maxStack = 0;
        var stackTotals = new double[n];
        var stackCalls = new long[n];
        for (var i = 0; i < n; i++)
        {
            var s = 0.0;
            foreach (var p in Trend.PerModel)
            {
                if (i < p.YValue.Count) s += p.YValue[i] ?? 0;
            }
            stackTotals[i] = s;
            if (s > maxStack) maxStack = s;
            if (i < Trend.ModelCallCount.Count) stackCalls[i] = (long)(Trend.ModelCallCount[i] ?? 0);
        }
        if (maxStack <= 0) return;

        // 堆叠柱体
        for (var i = 0; i < n; i++)
        {
            var x = leftPad + slot * i + (slot - barW) / 2;
            var yBase = TopPad + plotH;
            foreach (var p in Trend.PerModel)
            {
                var v = i < p.YValue.Count ? (p.YValue[i] ?? 0) : 0;
                if (v <= 0) continue;
                var barH = v / maxStack * plotH;
                var rect = new Rectangle
                {
                    Width = barW,
                    Height = Math.Max(0.5, barH),
                    Fill = ToBrush(p.Color),
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, yBase - barH);
                PlotCanvas.Children.Add(rect);
                yBase -= barH;
            }
        }

        // 顶层透明热区：每列一个，承载该时段用量 tooltip
        for (var i = 0; i < n; i++)
        {
            var hit = new Rectangle
            {
                Width = slot,
                Height = TopPad + plotH + bottomPad,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };
            Canvas.SetLeft(hit, leftPad + slot * i);
            Canvas.SetTop(hit, 0);
            ToolTipService.SetToolTip(hit, BuildColumnTip(i, stackTotals[i], stackCalls[i]));
            PlotCanvas.Children.Add(hit);
        }

        // 底部时间轴刻度（24h 图每 4 小时：0/4/8/12/16/20/末；其余约 6 等分；末尾补一个）
        if (ShowAxis && n > 0)
        {
            var step = n >= 24 ? 4 : Math.Max(1, n / 6);
            var indices = new List<int>();
            for (var idx = 0; idx < n; idx += step) indices.Add(idx);
            if ((n - 1) % step != 0) indices.Add(n - 1);
            foreach (var idx in indices)
            {
                var tick = new TextBlock
                {
                    Text = FormatAxisShort(Trend.XTime[idx]),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x4B, 0x55, 0x63)),
                };
                Canvas.SetLeft(tick, leftPad + slot * idx + slot / 2 - 16);
                Canvas.SetTop(tick, TopPad + plotH + 4);
                PlotCanvas.Children.Add(tick);
            }
        }

        // 左侧 Y 轴刻度（用量）
        if (ShowAxis && maxStack > 0)
        {
            double[] ys = { maxStack, maxStack / 2, 0 };
            foreach (var val in ys)
            {
                var y = TopPad + plotH - (val / maxStack * plotH);
                var lbl = new TextBlock
                {
                    Text = Formatters.FormatTokens((long)val),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x4B, 0x55, 0x63)),
                };
                Canvas.SetTop(lbl, y - 7);
                Canvas.SetLeft(lbl, 0);
                PlotCanvas.Children.Add(lbl);
            }
        }
    }

    /// <summary>构造某一时段的悬浮提示文本：时间 + 合计 + 各模型明细。</summary>
    private string BuildColumnTip(int i, double tokens, long calls)
    {
        var sb = new StringBuilder();
        sb.Append(FormatAxis(Trend!.XTime[i]));
        sb.Append("    用量 ").Append(Formatters.FormatTokens((long)tokens));
        sb.Append("  ·  调用 ").Append(calls.ToString("N0")).Append(" 次");
        foreach (var p in Trend.PerModel!)
        {
            var v = i < p.YValue.Count ? (p.YValue[i] ?? 0) : 0;
            if (v <= 0) continue;
            sb.AppendLine();
            sb.Append("  ").Append(p.Model).Append("：").Append(Formatters.FormatTokens((long)v));
        }
        return sb.ToString();
    }

    /// <summary>把轴标签裁剪得更短（如 "2026-07-01 14:00" → "07-01 14:00"）。</summary>
    private static string FormatAxis(string raw)
    {
        if (raw.Length >= 10 && raw.Length >= 5 && raw[4] == '-') return raw[5..];
        return raw;
    }

    /// <summary>时间轴短刻度（如 "2026-07-01 14:00:00" → "14:00"）。</summary>
    private static string FormatAxisShort(string raw)
    {
        // "yyyy-MM-dd HH:..." 或 ISO "yyyy-MM-ddTHH:..."
        if (raw.Length >= 16 && (raw[10] == ' ' || raw[10] == 'T')) return raw.Substring(11, 5);
        return raw.Length > 5 ? raw[^5..] : raw;
    }

    private static SolidColorBrush ToBrush(string hex)
    {
        try { return new SolidColorBrush(ColorHelper.ToColor(hex)); }
        catch (Exception ex) { AppLogger.Swallowed("ToBrush", ex); return new SolidColorBrush(Microsoft.UI.Colors.SteelBlue); }
    }
}
