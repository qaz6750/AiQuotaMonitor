namespace AiQuotaMonitor.Helpers;

/// <summary>应用运行目录下的便携数据路径。</summary>
public static class AppPaths
{
    /// <summary>应用所在目录。发布为 zip 后即 exe 所在目录。</summary>
    public static string AppDirectory { get; } = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>配置目录：按要求保留在当前应用目录下。</summary>
    public static string DataDirectory { get; } = Path.Combine(AppDirectory, "data");

    /// <summary>设置文件路径。</summary>
    public static string SettingsFile { get; } = Path.Combine(DataDirectory, "settings.json");

    /// <summary>日志目录。</summary>
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "logs");
}