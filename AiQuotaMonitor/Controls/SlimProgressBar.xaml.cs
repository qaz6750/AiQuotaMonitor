using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AiQuotaMonitor.Controls;

/// <summary>细进度条（默认 5pt 高，圆角 2.5）。</summary>
public sealed partial class SlimProgressBar : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SlimProgressBar), new PropertyMetadata(0.0, OnChanged));

    public static readonly DependencyProperty FillBrushProperty =
        DependencyProperty.Register(nameof(FillBrush), typeof(Brush), typeof(SlimProgressBar), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty TrackHeightProperty =
        DependencyProperty.Register(nameof(TrackHeight), typeof(double), typeof(SlimProgressBar), new PropertyMetadata(5.0, OnChanged));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush FillBrush { get => (Brush)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public double TrackHeight { get => (double)GetValue(TrackHeightProperty); set => SetValue(TrackHeightProperty, value); }

    public SlimProgressBar()
    {
        this.InitializeComponent();
        Loaded += (_, _) => Update();
        SizeChanged += (_, _) => Update();
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SlimProgressBar)d).Update();

    private void Update()
    {
        if (!IsLoaded) return;
        Root.Height = TrackHeight;
        Fill.Background = FillBrush;
        var w = Root.ActualWidth;
        Fill.Width = w * Math.Clamp(Value, 0, 100) / 100.0;
    }
}
