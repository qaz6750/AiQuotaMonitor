using System.Collections.ObjectModel;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AiQuotaMonitor.Controls;

/// <summary>
/// AIUsage 风格的「小方块」热力图：每个时间点一个小方块，
/// 颜色深浅按用量占比分级；鼠标悬浮可查看时段、token、调用次数与各账号明细。
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

    private readonly ObservableCollection<HeatCell> _cells = new();
    private int _columns = 24;

    public UsageHeatmapControl()
    {
        this.InitializeComponent();
        CellsHost.ItemsSource = _cells;
        Loaded += (_, _) => ApplyColumns();
    }

    private static void OnTrendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((UsageHeatmapControl)d).Rebuild();

    private void Rebuild()
    {
        _cells.Clear();
        var t = Trend;
        if (t is null || t.XTime is null || t.XTime.Count == 0)
        {
            return;
        }

        var n = t.XTime.Count;
        var yv = t.YValue ?? new List<double?>();
        var mc = t.ModelCallCount ?? new List<double?>();
        var perModel = t.PerModel;

        // 计算 max（忽略 null 与 0），用于颜色分级
        double max = 0;
        for (var i = 0; i < n; i++)
        {
            if (i < yv.Count && yv[i] is { } v && v > max) max = v;
        }
        if (max <= 0) max = 1;

        for (var i = 0; i < n; i++)
        {
            var time = t.XTime[i];
            var val = i < yv.Count ? (yv[i] ?? 0) : 0;
            var calls = i < mc.Count ? (mc[i] ?? 0) : 0;
            var level = val <= 0 ? 0 : (int)Math.Ceiling(val / max * 4);
            if (level < 1) level = 1;
            if (level > 4) level = 4;

            // 各账号明细
            string breakdown = string.Empty;
            if (perModel is { Count: > 0 })
            {
                var sb = new System.Text.StringBuilder();
                foreach (var s in perModel)
                {
                    if (i < s.YValue.Count && s.YValue[i] is { } sv && sv > 0)
                    {
                        sb.Append(s.Model).Append("：").Append(Formatters.FormatTokens((long)sv)).Append("  ");
                    }
                }
                breakdown = sb.ToString().Trim();
            }

            _cells.Add(new HeatCell
            {
                TimeLabel = FormatTime(time),
                TokenText = val > 0 ? $"{Formatters.FormatTokens((long)val)} tokens" : "无用量",
                CallText = $"调用 {(long)calls} 次",
                Breakdown = breakdown,
                Fill = LevelBrush(val <= 0 ? 0 : level),
            });
        }

        // 列数：优先按 24 小时排列；否则按 7（周）或数据点数
        _columns = n >= 24 && n % 24 == 0 ? 24
                 : n >= 12 && n % 12 == 0 ? 12
                 : n >= 7 ? 7
                 : n;
        ApplyColumns();
    }

    private void ApplyColumns()
    {
        if (GetDescendant<ItemsWrapGrid>(CellsHost) is { } panel)
        {
            panel.MaximumRowsOrColumns = _columns;
        }
    }

    private static T? GetDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            var inner = GetDescendant<T>(child);
            if (inner is not null) return inner;
        }
        return null;
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

    private static string FormatTime(string raw)
    {
        // raw 形如 "MM-dd HH:00" 或 "yyyy-MM-dd"；尽量原样展示
        return string.IsNullOrWhiteSpace(raw) ? "—" : raw;
    }
}

/// <summary>热力图单个方块的数据。</summary>
public sealed class HeatCell
{
    public string TimeLabel { get; set; } = string.Empty;
    public string TokenText { get; set; } = string.Empty;
    public string CallText { get; set; } = string.Empty;
    public string Breakdown { get; set; } = string.Empty;
    public SolidColorBrush Fill { get; set; } = new(Windows.UI.Color.FromArgb(0x14, 0x0E, 0xA5, 0xE9));
}
