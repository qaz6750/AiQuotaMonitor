using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiQuotaMonitor.Controls;

/// <summary>KPI 卡片：标签 + 大数值 + 副标。</summary>
public sealed partial class StatCard : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty SubLabelProperty =
        DependencyProperty.Register(nameof(SubLabel), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty, OnChanged));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string SubLabel { get => (string)GetValue(SubLabelProperty); set => SetValue(SubLabelProperty, value); }

    public StatCard()
    {
        this.InitializeComponent();
        Loaded += (_, _) => Update();
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((StatCard)d).Update();

    private void Update()
    {
        if (!IsLoaded) return;
        LabelHost.Text = Label ?? string.Empty;
        ValueHost.Text = Value ?? string.Empty;
        var sub = SubLabel ?? string.Empty;
        SubLabelHost.Text = sub;
        SubLabelHost.Visibility = string.IsNullOrEmpty(sub) ? Visibility.Collapsed : Visibility.Visible;
    }
}
