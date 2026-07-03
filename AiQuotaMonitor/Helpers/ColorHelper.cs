using Windows.UI;

namespace AiQuotaMonitor.Helpers;

/// <summary>颜色工具：根据配额百分比等返回合适的颜色。</summary>
public static class ColorHelper
{
    /// <summary>把 #RRGGBB / #AARRGGBB 十六进制字符串转为 Windows.UI.Color。</summary>
    public static Color ToColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        var a = Convert.ToByte(hex.Substring(0, 2), 16);
        var r = Convert.ToByte(hex.Substring(2, 2), 16);
        var g = Convert.ToByte(hex.Substring(4, 2), 16);
        var b = Convert.ToByte(hex.Substring(6, 2), 16);
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>根据配额百分比返回颜色：全部用完(≥85%)红 / 用到一半(≥50%)黄 / 其余浅蓝。</summary>
    public static Color GetQuotaColor(double pct)
    {
        if (pct >= 85) return ToColor("#EF4444");
        if (pct >= 50) return ToColor("#E0A800");
        return ToColor("#0EA5E9");
    }

    /// <summary>取 5h 与周配额的较大值决定颜色。</summary>
    public static Color GetCombinedColor(double fiveHourPct, double weeklyPct)
        => GetQuotaColor(Math.Max(fiveHourPct, weeklyPct));

    /// <summary>把 Color 转为 WinUI SolidColorBrush。</summary>
    public static Microsoft.UI.Xaml.Media.SolidColorBrush ToBrush(Color c) => new(c);

    /// <summary>把 Color 转为 «#RRGGBB» 字符串。</summary>
    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
