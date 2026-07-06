using System.Diagnostics;
using AiQuotaMonitor.Services;

namespace AiQuotaMonitor.Helpers;

/// <summary>
/// 轻量级应用日志：Debug.WriteLine 输出 + 可选文件日志。
/// 替代静默 catch，便于调试。
/// </summary>
public static class AppLogger
{
    private static readonly string LogDir = AppPaths.LogDirectory;
    private static readonly object _lock = new();
    private static StreamWriter? _writer;
    private static DateTime _logDate;

    /// <summary>记录信息。</summary>
    public static void Info(string msg, [System.Runtime.CompilerServices.CallerMemberName] string? source = null)
        => Log("INFO", msg, source);

    /// <summary>记录详细诊断信息，仅在 Verbose 日志级别写入。</summary>
    public static void Verbose(string msg, [System.Runtime.CompilerServices.CallerMemberName] string? source = null)
        => Log("VERBOSE", msg, source);

    /// <summary>记录警告。</summary>
    public static void Warn(string msg, [System.Runtime.CompilerServices.CallerMemberName] string? source = null)
        => Log("WARN", msg, source);

    /// <summary>记录错误（含异常）。</summary>
    public static void Error(string msg, Exception? ex = null, [System.Runtime.CompilerServices.CallerMemberName] string? source = null)
        => Log("ERROR", ex is null ? msg : $"{msg}: {ex.Message}", source);

    /// <summary>记录静默捕获的异常（替代 catch {} ）。</summary>
    public static void Swallowed(string context, Exception? ex = null, [System.Runtime.CompilerServices.CallerMemberName] string? source = null)
        => Log("SWALLOWED", ex is null ? context : $"{context}: {ex.Message}", source);

    private static void Log(string level, string msg, string? source)
    {
        if (!ShouldLog(level)) return;
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [{source ?? "?"}] {msg}";
        Debug.WriteLine(line);

        // 文件日志（每天一个文件）
        try
        {
            EnsureWriter();
            lock (_lock)
            {
                _writer?.WriteLine(line);
                _writer?.Flush();
            }
        }
        catch { /* 日志本身不应抛出 */ }
    }

    public static void ClearCurrentLog()
    {
        lock (_lock)
        {
            try
            {
                _writer?.Dispose();
                _writer = null;
                _logDate = default;
                Directory.CreateDirectory(LogDir);
                foreach (var f in Directory.GetFiles(LogDir, "*.log")) File.Delete(f);
            }
            catch { }
        }
    }

    private static bool ShouldLog(string level)
    {
        string setting;
        try { setting = SettingsService.Instance.LogLevel; }
        catch { setting = "Info"; }
        if (setting.Equals("Verbose", StringComparison.OrdinalIgnoreCase)) return true;
        if (setting.Equals("Error", StringComparison.OrdinalIgnoreCase)) return level is "ERROR";
        return level is not "VERBOSE";
    }

    private static void EnsureWriter()
    {
        var today = DateTime.Now.Date;
        if (_writer is not null && today == _logDate) return;

        _writer?.Dispose();
        Directory.CreateDirectory(LogDir);
        var path = Path.Combine(LogDir, $"app-{today:yyyy-MM-dd}.log");
        _writer = new StreamWriter(path, append: true) { AutoFlush = true };
        _logDate = today;

        // 清理 7 天前的日志
        try
        {
            foreach (var f in Directory.GetFiles(LogDir, "app-*.log"))
            {
                if (File.GetLastWriteTime(f) < today.AddDays(-7))
                    File.Delete(f);
            }
        }
        catch { }
    }
}
