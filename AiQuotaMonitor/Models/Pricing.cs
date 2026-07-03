namespace AiQuotaMonitor.Models;

/// <summary>单模型定价（元 / 百万 tokens）。</summary>
public sealed record ModelPrice(double InputPerMillion, double OutputPerMillion);

/// <summary>
/// GLM 模型定价表（参考价，来源：智谱 BigModel 官网定价页）。
/// Coding Plan 本身是订阅制，这里仅用于把 token 用量折算为「按量 API 计费等价金额」。
/// </summary>
public static class GlmPricing
{
    /// <summary>键为小写、去空格的模型名，便于匹配。</summary>
    public static readonly IReadOnlyDictionary<string, ModelPrice> Prices =
        new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
        {
            ["glm-5.2"] = new(8, 28),
            ["glm-5.1"] = new(6, 24),
            ["glm-5-turbo"] = new(5, 22),
            ["glm-5"] = new(4, 18),
            ["glm-4.7"] = new(2, 8),
            ["glm-4.5-air"] = new(0.8, 2),
        };

    /// <summary>未知模型回退价（保守取旗舰 GLM-5.2 价位）。</summary>
    public static readonly ModelPrice Fallback = new(8, 28);

    /// <summary>已知模型 → 图表颜色（权威色表）。</summary>
    public static readonly IReadOnlyDictionary<string, string> ModelColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GLM-5.2"] = "#5B8FF9",
            ["GLM-5.1"] = "#5AD8A6",
            ["GLM-5-Turbo"] = "#F6BD16",
            ["GLM-5V-Turbo"] = "#9270CA",
            ["GLM4.7"] = "#5D7092",
            ["GLM-4.6V"] = "#E8684A",
            ["GLM-4.5-Air"] = "#6DC8EC",
        };

    /// <summary>通用调色板（用于未在 ModelColors 中登记的模型）。</summary>
    public static readonly string[] Palette =
    {
        "#5B8FF9", "#5AD8A6", "#5D7092", "#F6BD16",
        "#E8684A", "#6DC8EC", "#9270CA", "#FF9D4D",
    };

    /// <summary>查询模型定价：精确 → 前缀 → 回退价。</summary>
    public static (ModelPrice Price, bool IsFallback) GetPrice(string? model)
    {
        var key = Normalize(model);
        if (!string.IsNullOrEmpty(key) && Prices.TryGetValue(key, out var p))
        {
            return (p, false);
        }

        foreach (var kv in Prices)
        {
            if (key.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                return (kv.Value, false);
            }
        }

        return (Fallback, true);
    }

    /// <summary>查询模型图表颜色。</summary>
    public static string GetModelColor(string? model, int fallbackIndex = 0)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            foreach (var kv in ModelColors)
            {
                if (model.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kv.Value;
                }
            }
        }

        return Palette[Math.Abs(fallbackIndex) % Palette.Length];
    }

    private static string Normalize(string? s) => (s ?? string.Empty).ToLowerInvariant().Replace(" ", string.Empty);
}
