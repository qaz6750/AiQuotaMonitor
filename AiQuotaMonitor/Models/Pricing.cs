namespace AiQuotaMonitor.Models;

/// <summary>单模型定价（元 / 百万 tokens）。</summary>
public sealed record ModelPrice(double InputPerMillion, double OutputPerMillion);

/// <summary>费用显示用固定汇率。官方账单为 USD 时统一折算为 CNY。</summary>
public static class CurrencyRates
{
    public const double UsdToCny = 7.25;
}

/// <summary>
/// 多提供商模型定价表（元 / 百万 tokens）。
/// 用量接口没有输入/输出和缓存明细时，仅用于把 token 用量折算为「按量 API 计费等价金额」。
/// Kimi / DeepSeek 等有缓存命中价的模型，默认使用缓存未命中价做保守估算。
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
            ["kimi-k2.7-code"] = new(6.5, 27),
            ["kimi-k2.7-code-highspeed"] = new(13, 54),
            ["kimi-k2.6"] = new(6.5, 27),
            ["kimi-k2.5"] = new(4, 21),
            ["moonshot-v1-8k"] = new(2, 10),
            ["moonshot-v1-32k"] = new(5, 20),
            ["moonshot-v1-128k"] = new(10, 30),
            ["deepseek-v4-flash"] = new(0.14 * CurrencyRates.UsdToCny, 0.28 * CurrencyRates.UsdToCny),
            ["deepseek-v4-pro"] = new(0.435 * CurrencyRates.UsdToCny, 0.87 * CurrencyRates.UsdToCny),
            ["deepseek-chat"] = new(0.14 * CurrencyRates.UsdToCny, 0.28 * CurrencyRates.UsdToCny),
            ["deepseek-reasoner"] = new(0.14 * CurrencyRates.UsdToCny, 0.28 * CurrencyRates.UsdToCny),
            ["gpt-4o"] = new(18.125, 72.5),
            ["gpt-4.1"] = new(14.5, 58),
            ["gpt-4.1-mini"] = new(2.9, 11.6),
            ["gpt-4.1-nano"] = new(0.725, 2.9),
            ["gpt-5"] = new(9.0625, 72.5),
            ["gpt-5-mini"] = new(1.8125, 14.5),
            ["gpt-5-nano"] = new(0.3625, 2.9),
            ["claude-fable-5"] = new(10 * CurrencyRates.UsdToCny, 50 * CurrencyRates.UsdToCny),
            ["claude-opus-4.8"] = new(5 * CurrencyRates.UsdToCny, 25 * CurrencyRates.UsdToCny),
            ["claude-opus-4.7"] = new(5 * CurrencyRates.UsdToCny, 25 * CurrencyRates.UsdToCny),
            ["claude-opus-4.6"] = new(5 * CurrencyRates.UsdToCny, 25 * CurrencyRates.UsdToCny),
            ["claude-opus-4.5"] = new(5 * CurrencyRates.UsdToCny, 25 * CurrencyRates.UsdToCny),
            ["claude-opus-4.1"] = new(15 * CurrencyRates.UsdToCny, 75 * CurrencyRates.UsdToCny),
            ["claude-opus-4"] = new(5 * CurrencyRates.UsdToCny, 25 * CurrencyRates.UsdToCny),
            ["claude-sonnet-5"] = new(2 * CurrencyRates.UsdToCny, 10 * CurrencyRates.UsdToCny),
            ["claude-sonnet-4.6"] = new(3 * CurrencyRates.UsdToCny, 15 * CurrencyRates.UsdToCny),
            ["claude-sonnet-4.5"] = new(3 * CurrencyRates.UsdToCny, 15 * CurrencyRates.UsdToCny),
            ["claude-sonnet-4"] = new(3 * CurrencyRates.UsdToCny, 15 * CurrencyRates.UsdToCny),
            ["claude-haiku-4.5"] = new(1 * CurrencyRates.UsdToCny, 5 * CurrencyRates.UsdToCny),
            ["claude-3-5-sonnet"] = new(21.75, 108.75),
            ["claude-3-5-haiku"] = new(5.8, 29),
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
            ["gpt"] = "#10A37F",
            ["claude"] = "#D97757",
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

        var match = Prices
            .Where(kv => key.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Key.Length)
            .FirstOrDefault();
        if (match.Key is not null) return (match.Value, false);

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
