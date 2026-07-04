using System.Reflection;

namespace AiQuotaMonitor.Helpers;

/// <summary>应用版本与构建信息。</summary>
public static class BuildInfo
{
    private static readonly Assembly Assembly = typeof(BuildInfo).Assembly;

    public static string Version => Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static string InformationalVersion =>
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Version;

    /// <summary>从 InformationalVersion 中提取 commit hash（CI 通过 SourceRevisionId 注入）。</summary>
    public static string Commit
    {
        get
        {
            var info = InformationalVersion;
            var plus = info.LastIndexOf('+');
            if (plus >= 0 && plus + 1 < info.Length)
            {
                return info[(plus + 1)..];
            }
            return "dev";
        }
    }

    public static string ShortCommit => Commit.Length >= 7 ? Commit[..7] : Commit;

    public static string DisplayVersion => $"v{Version}";

    public static string DisplayBuild => $"{DisplayVersion} · {ShortCommit}";
}