namespace AiQuotaMonitor.Models;

/// <summary>规范化的配额类型常量（与智谱监控接口原始 type 解耦）。</summary>
public static class QuotaTypes
{
    /// <summary>5 小时滚动窗口的 token 配额。</summary>
    public const string FiveHour = "Token usage(5 Hour)";

    /// <summary>周（7 天）滚动窗口的 token 配额。</summary>
    public const string Weekly = "Token usage(Weekly)";

    /// <summary>MCP 工具的月度配额。</summary>
    public const string Mcp = "MCP usage(1 Month)";
}
