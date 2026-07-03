using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>单个模型的等价计费。</summary>
public sealed record ModelCost(string Model, double CostCny);

/// <summary>等价 API 计费估算结果（CNY）。</summary>
public sealed class CostEstimate
{
    public double TotalCny { get; init; }
    public IReadOnlyList<ModelCost> PerModel { get; init; } = Array.Empty<ModelCost>();
    public bool HasFallback { get; init; }
}

/// <summary>
/// 把 Coding Plan 的 token 用量折算为「按量 API 计费等价金额」。
/// 趋势数据无输入/输出拆分，按 (输入价 + 输出价) / 2 的均价估算。
/// </summary>
public static class CostCalculator
{
    public static CostEstimate? EstimateFromModels(IEnumerable<ModelUsageSummary>? models)
    {
        if (models is null) return null;
        var totals = models
            .Where(m => m.TotalTokens > 0)
            .Select(m => (m.Model, (double)m.TotalTokens));
        return Estimate(totals);
    }

    public static CostEstimate? EstimateFromTrend(TrendSeries? trend)
    {
        if (trend?.PerModel is null || trend.PerModel.Count == 0) return null;
        var totals = trend.PerModel
            .Select(p => (p.Model, p.YValue.Sum(v => v ?? 0)));
        return Estimate(totals);
    }

    public static CostEstimate? Estimate(IEnumerable<(string Model, double Tokens)> totals)
    {
        double total = 0;
        var hasFallback = false;
        var per = new List<ModelCost>();

        foreach (var (model, tokens) in totals)
        {
            if (tokens <= 0) continue;
            var (price, isFallback) = GlmPricing.GetPrice(model);
            if (isFallback) hasFallback = true;
            var blended = (price.InputPerMillion + price.OutputPerMillion) / 2.0;
            var cost = tokens / 1_000_000.0 * blended;
            total += cost;
            if (cost > 0) per.Add(new ModelCost(model, cost));
        }

        if (total <= 0) return null;
        per.Sort((a, b) => b.CostCny.CompareTo(a.CostCny));
        return new CostEstimate { TotalCny = total, PerModel = per, HasFallback = hasFallback };
    }
}
