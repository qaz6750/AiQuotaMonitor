using System.Text.Json.Serialization;

namespace AiQuotaMonitor.Models.Api;

/// <summary>智谱监控接口通用响应外壳。</summary>
public sealed class GlmResponse<T>
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("msg")] public string? Msg { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
    [JsonPropertyName("success")] public bool Success { get; set; }
}

// ============ /api/monitor/usage/quota/limit ============

public sealed class QuotaLimitData
{
    [JsonPropertyName("limits")] public List<LimitItem>? Limits { get; set; }
    [JsonPropertyName("level")] public string? Level { get; set; }
}

public sealed class LimitItem
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("unit")] public int Unit { get; set; }
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("percentage")] public double Percentage { get; set; }
    [JsonPropertyName("nextResetTime")] public long? NextResetTime { get; set; }
    [JsonPropertyName("usage")] public double? Usage { get; set; }
    [JsonPropertyName("currentValue")] public double? CurrentValue { get; set; }
    [JsonPropertyName("remaining")] public double? Remaining { get; set; }
    [JsonPropertyName("usageDetails")] public List<UsageDetail>? UsageDetails { get; set; }
}

public sealed class UsageDetail
{
    [JsonPropertyName("modelCode")] public string? ModelCode { get; set; }
    [JsonPropertyName("usage")] public double Usage { get; set; }
}

// ============ /api/monitor/usage/model-usage ============

public sealed class ModelUsageData
{
    [JsonPropertyName("x_time")] public List<string>? XTime { get; set; }
    [JsonPropertyName("tokensUsage")] public List<double?>? TokensUsage { get; set; }
    [JsonPropertyName("modelCallCount")] public List<double?>? ModelCallCount { get; set; }
    [JsonPropertyName("modelDataList")] public List<ModelTrendItem>? ModelDataList { get; set; }
    [JsonPropertyName("totalUsage")] public TotalUsage? TotalUsage { get; set; }

    /// <summary>部分接口会把各模型汇总放在 modelUsage 数组里。</summary>
    [JsonPropertyName("modelUsage")] public List<ModelUsageItem>? ModelUsage { get; set; }
}

public sealed class ModelTrendItem
{
    [JsonPropertyName("modelName")] public string? ModelName { get; set; }
    [JsonPropertyName("tokensUsage")] public List<double?>? TokensUsage { get; set; }
    [JsonPropertyName("modelCallCount")] public List<double?>? ModelCallCount { get; set; }
    [JsonPropertyName("callCount")] public List<double?>? CallCount { get; set; }
}

public sealed class ModelUsageItem
{
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("inputTokens")] public long InputTokens { get; set; }
    [JsonPropertyName("outputTokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("totalTokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("requestCount")] public int RequestCount { get; set; }
}

public sealed class TotalUsage
{
    [JsonPropertyName("totalModelCallCount")] public double TotalModelCallCount { get; set; }
    [JsonPropertyName("totalTokensUsage")] public double TotalTokensUsage { get; set; }
}

// ============ /api/monitor/usage/tool-usage ============

public sealed class ToolUsageItem
{
    [JsonPropertyName("tool")] public string? Tool { get; set; }
    [JsonPropertyName("callCount")] public int CallCount { get; set; }
    [JsonPropertyName("successCount")] public int SuccessCount { get; set; }
    [JsonPropertyName("failureCount")] public int FailureCount { get; set; }
}
