using System.Net;

namespace AiQuotaMonitor.Services;

/// <summary>
/// 全局共享 HTTP 基础设施：所有 provider 客户端复用同一个 <see cref="SocketsHttpHandler"/>，
/// 从而共享连接池 / keep-alive / DNS 缓存，显著降低内存占用与重复握手开销，
/// 并避免每个 provider 各自维护一套独立连接池。
/// </summary>
internal static class SharedHttp
{
    /// <summary>进程级唯一的连接处理器。多个 <see cref="HttpClient"/> 可安全共享同一个 handler。</summary>
    public static readonly SocketsHttpHandler Handler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 4,
        AutomaticDecompression = DecompressionMethods.All,
        UseCookies = false,
    };

    /// <summary>基于共享 handler 创建带超时的客户端（handler 不被 dispose）。</summary>
    public static HttpClient Create(TimeSpan? timeout = null)
        => new(Handler, disposeHandler: false) { Timeout = timeout ?? TimeSpan.FromSeconds(30) };

    public static string EnsureHttps(string url)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("为保护凭据安全，仅支持 HTTPS 接口地址。");
        return url;
    }
}
