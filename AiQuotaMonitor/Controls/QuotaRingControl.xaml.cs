using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace AiQuotaMonitor.Controls;

/// <summary>
/// 圆环进度控件（参考 cc-bar Popover 56pt 服务 5h 环）。
/// 中央显示整数百分比，下方小字为 CenterLabel（如 5H / WEEK）。
/// </summary>
public sealed partial class QuotaRingControl : UserControl
{
    public static readonly DependencyProperty PercentageProperty =
        DependencyProperty.Register(nameof(Percentage), typeof(double), typeof(QuotaRingControl), new PropertyMetadata(0.0, OnVisualChanged));

    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(nameof(RingBrush), typeof(Brush), typeof(QuotaRingControl), new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty CenterLabelProperty =
        DependencyProperty.Register(nameof(CenterLabel), typeof(string), typeof(QuotaRingControl), new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty DiameterProperty =
        DependencyProperty.Register(nameof(Diameter), typeof(double), typeof(QuotaRingControl), new PropertyMetadata(56.0, OnVisualChanged));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(QuotaRingControl), new PropertyMetadata(5.5, OnVisualChanged));

    public double Percentage { get => (double)GetValue(PercentageProperty); set => SetValue(PercentageProperty, value); }
    public Brush RingBrush { get => (Brush)GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public string CenterLabel { get => (string)GetValue(CenterLabelProperty); set => SetValue(CenterLabelProperty, value); }
    public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public QuotaRingControl()
    {
        this.InitializeComponent();
        Loaded += (_, _) => UpdateAll();
        SizeChanged += (_, _) => UpdateAll();
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((QuotaRingControl)d).UpdateAll();

    private void UpdateAll()
    {
        if (!IsLoaded) return;

        var d = Diameter;
        Root.Width = d;
        Root.Height = d;
        ValueText.Text = Math.Round(Percentage).ToString("F0");
        LabelText.Text = CenterLabel ?? string.Empty;

        var stroke = StrokeThickness;
        TrackPath.StrokeThickness = stroke;
        ValuePath.StrokeThickness = stroke;
        ValuePath.Stroke = RingBrush;

        var r = (d - stroke) / 2.0;
        if (r <= 0) return;
        var cx = d / 2.0;
        var cy = d / 2.0;

        TrackPath.Data = new EllipseGeometry { Center = new Point(cx, cy), RadiusX = r, RadiusY = r };

        var pct = Math.Clamp(Percentage, 0, 100);
        var sweep = pct / 100.0 * 360.0;
        if (sweep <= 0)
        {
            ValuePath.Data = null;
            return;
        }
        if (sweep >= 359.99)
        {
            ValuePath.Data = new EllipseGeometry { Center = new Point(cx, cy), RadiusX = r, RadiusY = r };
            return;
        }

        var startAngle = -90.0;
        var endAngle = startAngle + sweep;
        var sp = Polar(cx, cy, r, startAngle);
        var ep = Polar(cx, cy, r, endAngle);
        var large = sweep > 180;

        var fig = new PathFigure { StartPoint = sp };
        fig.Segments.Add(new ArcSegment
        {
            Point = ep,
            Size = new Size(r, r),
            IsLargeArc = large,
            SweepDirection = SweepDirection.Clockwise,
        });
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        ValuePath.Data = geo;
    }

    private static Point Polar(double cx, double cy, double r, double angleDeg)
    {
        var a = angleDeg * Math.PI / 180.0;
        return new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
    }
}
