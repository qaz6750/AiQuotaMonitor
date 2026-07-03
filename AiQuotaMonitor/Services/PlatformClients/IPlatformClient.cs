using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>平台用量查询客户端抽象。每个平台实现一个，产出归一化的 UsageResult。</summary>
public interface IPlatformClient
{
    /// <summary>平台标识（glm / opencode-go / opencode-zen / custom）。</summary>
    string PlatformId { get; }

    /// <summary>查询用量。凭据可能是 API Key 或 Cookie，由实现自行处理。</summary>
    Task<UsageResult> QueryUsageAsync(string credential, string baseUrl, bool enableRetry = true);
}

/// <summary>按平台 id 解析客户端。</summary>
public static class PlatformClientFactory
{
    private static readonly GlmClient Glm = new();
    private static readonly KimiClient Kimi = new();
    private static readonly MiniMaxClient MiniMax = new();
    private static readonly MiMoClient MiMo = new();
    private static readonly FactoryClient Factory = new();
    private static readonly OpenAiClient Gpt = new();
    private static readonly ClaudeClient Claude = new();

    /// <summary>根据账号的 ProviderId 获取对应客户端。</summary>
    public static IPlatformClient Get(GlmAccount account)
    {
        return account.ProviderId switch
        {
            "kimi" => Kimi,
            "minimax" => MiniMax,
            "mimo" => MiMo,
            "factory" => Factory,
            "gpt" => Gpt,
            "claude" => Claude,
            _ => Glm,
        };
    }
}
