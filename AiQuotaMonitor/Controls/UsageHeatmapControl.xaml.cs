using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace AiQuotaMonitor.Controls;

/// <summary>
/// AIUsage 风格的「小方块」热力图：按天分行、每天 24 小时一格，
/// 颜色深浅按用量占比分级；横向填满，鼠标悬浮可查看时段、token、调用次数与各账号明细。
/// </summary>
public sealed partial class UsageHeatmapControl : UserControl
{
    public static readonly DependencyProperty TrendProperty =
        DependencyProperty.Register(nameof(Trend), typeof(TrendSeries), typeof(UsageHeatmapControl),
            new PropertyMetadata(null, OnTrendChanged));

    /// <summary>绑定趋势数据（CombinedTrend 等）。</summary>
    public TrendSeries? Trend
    {
        get => (TrendSeries?)GetValue(TrendProperty);
        set => SetValue(TrendProperty, value);
    }

    private static readonly GridLength Star = new(1, GridUnitType.Star);

    public UsageHeatmapControl()
    {
        this.InitializeComponent();
    }

    private static void OnTrendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((UsageHeatmapControl)d).Rebuild();

    private void Rebuild()
    {
        HeatmapRoot.Children.Clear();
        HeatmapRoot.RowDefinitions.Clear();
        HeatmapRoot.ColumnDefinitions.Clear();

        var t = Trend;
        if (t is null || t.XTime is null || t.XTime.Count == 0)
        {
            return;
        }

        var n = t.XTime.Count;
        var yv = t.YValue ?? new List<double?>();
        var mc = t.ModelCallCount ?? new List<double?>();
        var perModel = t.PerModel;

        // 最大值用于颜色分级（忽略 0）
        double max = 0;
        for (var i = 0; i < n; i++)
        {
            if (i < yv.Count && yv[i] is { } v && v > max) max = v;
        }
        if (max <= 0) max = 1;

        // 解析每个时间点的「天」与「小时」
        var parsed = new List<(string Day, int Hour, double Val, long Calls, string Breakdown)>(n);
        bool hasHour = false;
        for (var i = 0; i < n; i++)
        {
            var raw = t.XTime[i];
            var (day, hour) = SplitTime(raw);
            if (hour >= 0) hasHour = true;
            var val = i < yv.Count ? (yv[i] ?? 0) : 0;
            var calls = i < mc.Count ? (long)(mc[i] ?? 0) : 0;
            parsed.Add((day, hour, val, calls, BuildBreakdown(perModel, i)));
        }

        if (hasHour)
        {
            BuildHourly(parsed, max);
        }
        else
        {
            BuildDaily(parsed, max);
        }

        // 淡入过渡
        PlayFadeIn();
    }

    /// <summary>按天分组、每天 24 小时一行的网格 + 小时轴。</summary>
    private void BuildHourly(List<(string Day, int Hour, double Val, long Calls, string Breakdown)> parsed, double max)
    {
        // 列：[Auto 天标签] + [Star 24 小时区]
        HeatmapRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        HeatmapRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = Star });

        // 按天分组（保持出现顺序）
        var dayOrder = new List<string>();
        var byDay = new Dictionary<string, List<(int Hour, double Val, long Calls, string Breakdown, string FullTime)>>();
        for (var i = 0; i < parsed.Count; i++)
        {
            var p = parsed[i];
            var full = Trend!.XTime[i];
            if (!byDay.ContainsKey(p.Day)) { byDay[p.Day] = new(); dayOrder.Add(p.Day); }
            byDay[p.Day].Add((p.Hour < 0 ? 0 : p.Hour, p.Val, p.Calls, p.Breakdown, full));
        }

        // 每天一行
        foreach (var day in dayOrder)
        {
            HeatmapRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = HeatmapRoot.RowDefinitions.Count - 1;

            // 天标签
            var label = new TextBlock
            {
                Text = day,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["SubtleTextBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 10, 2),
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            HeatmapRoot.Children.Add(label);

            // 24 小时格
            var rowGrid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            for (var c = 0; c < 24; c++)
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = Star });

            var cells = byDay[day];
            for (var h = 0; h < 24; h++)
            {
                var match = cells.FirstOrDefault(x => x.Hour == h);
                var border = MakeCell(match.FullTime, match.Val, match.Calls, match.Breakdown, max, match.Val > 0);
                Grid.SetColumn(border, h);
                rowGrid.Children.Add(border);
            }

            Grid.SetRow(rowGrid, row);
            Grid.SetColumn(rowGrid, 1);
            HeatmapRoot.Children.Add(rowGrid);
        }

        // 小时轴
        HeatmapRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var axis = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        for (var c = 0; c < 24; c++)
            axis.ColumnDefinitions.Add(new ColumnDefinition { Width = Star });
        foreach (var h in new[] { 0, 6, 12, 18 })
        {
            var hl = new TextBlock
            {
                Text = $"{h:00}",
                FontSize = 10,
                Foreground = (Brush)Application.Current.Resources["SubtleTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 0),
            };
            Grid.SetColumn(hl, h);
            axis.Children.Add(hl);
        }
        Grid.SetRow(axis, HeatmapRoot.RowDefinitions.Count - 1);
        Grid.SetColumn(axis, 1);
        HeatmapRoot.Children.Add(axis);
    }

    /// <summary>非小时数据：单行 N 列，每列标注原始时间。</summary>
    private void BuildDaily(List<(string Day, int Hour, double Val, long Calls, string Breakdown)> parsed, double max)
    {
        HeatmapRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = Star });
        HeatmapRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var rowGrid = new Grid();
        for (var c = 0; c < parsed.Count; c++)
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = Star });

        for (var i = 0; i < parsed.Count; i++)
        {
            var p = parsed[i];
            var border = MakeCell(Trend!.XTime[i], p.Val, p.Calls, p.Breakdown, max, p.Val > 0);
            Grid.SetColumn(border, i);
            rowGrid.Children.Add(border);
        }
        Grid.SetRow(rowGrid, 0);
        HeatmapRoot.Children.Add(rowGrid);
    }

    private static Border MakeCell(string fullTime, double val, long calls, string breakdown, double max, bool hasValue)
    {
        var level = hasValue ? (int)Math.Ceiling(val / max * 4) : 0;
        if (level < 1 && hasValue) level = 1;
        if (level > 4) level = 4;

        var border = new Border
        {
            Height = 20,
            Margin = new Thickness(1.5, 1, 1.5, 1),
            CornerRadius = new CornerRadius(6),
            Background = LevelBrush(hasValue ? level : 0),
        };

        var tip = new ToolTip { MaxWidth = 300 };
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(new TextBlock { Text = fullTime, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 });
        sp.Children.Add(new TextBlock { Text = val > 0 ? $"{Formatters.FormatTokens((long)val)} tokens" : "无用量", FontSize = 11 });
        sp.Children.Add(new TextBlock { Text = $"调用 {calls} 次", FontSize = 11, Foreground = (Brush)Application.Current.Resources["SubtleTextBrush"] });
        if (!string.IsNullOrWhiteSpace(breakdown))
        {
            sp.Children.Add(new TextBlock { Text = breakdown, FontSize = 11, Foreground = (Brush)Application.Current.Resources["SubtleTextBrush"], TextWrapping = TextWrapping.Wrap });
        }
        tip.Content = sp;
        ToolTipService.SetToolTip(border, tip);
        return border;
    }

    private static string BuildBreakdown(List<ModelTrendSeries>? perModel, int i)
    {
        if (perModel is null || perModel.Count == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        var visible = 0;
        var hidden = 0;
        foreach (var s in perModel.OrderByDescending(s => i < s.YValue.Count ? s.YValue[i] ?? 0 : 0))
        {
            if (i < s.YValue.Count && s.YValue[i] is { } sv && sv > 0)
            {
                if (visible < 6)
                {
                    sb.Append(s.Model).Append("：").Append(Formatters.FormatTokens((long)sv)).Append("   ");
                    visible++;
                }
                else
                {
                    hidden++;
                }
            }
        }
        if (hidden > 0) sb.Append("等 ").Append(hidden).Append(" 个账号");
        return sb.ToString().Trim();
    }

    private static (string Day, int Hour) SplitTime(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (raw, -1);
        // 支持 "MM-dd HH:00" / "yyyy-MM-dd HH:00" / "yyyy-MM-dd"
        var space = raw.IndexOf(' ');
        if (space > 0)
        {
            var day = raw[..space];
            var timePart = raw[(space + 1)..];
            var colon = timePart.IndexOf(':');
            if (colon > 0 && int.TryParse(timePart[..colon], out var h)) return (day, h);
            return (day, -1);
        }
        return (raw, -1);
    }

    private static SolidColorBrush LevelBrush(int level)
    {
        var alpha = level <= 0 ? 0x14 : level switch
        {
            1 => 0x33,
            2 => 0x66,
            3 => 0x99,
            _ => 0xE6,
        };
        return new SolidColorBrush(Windows.UI.Color.FromArgb((byte)alpha, 0x0E, 0xA5, 0xE9));
    }

    private void PlayFadeIn()
    {
        var story = new Storyboard();
        var anim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(anim, HeatmapRoot);
        Storyboard.SetTargetProperty(anim, "Opacity");
        story.Children.Add(anim);
        HeatmapRoot.Opacity = 0;
        story.Begin();
    }
}
