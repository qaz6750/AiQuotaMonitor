using System.Diagnostics;
using System.Text.RegularExpressions;
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
    private static readonly Regex SensitiveJsonRegex = new(
        "(?i)(\\\"(?:api[_-]?key|key|token|access[_-]?token|authorization|email|organization[_-]?id|org[_-]?id)\\\"\\s*:\\s*)\\\"[^\\\"]*\\\"",
        RegexOptions.Compiled);
    private static readonly Regex SensitiveHeaderRegex = new(
        "(?i)\\b(api[_-]?key|key|token|access[_-]?token|authorization|email|organization[_-]?id|org[_-]?id)=([^&\\s;]+)",
        RegexOptions.Compiled);
    private static StreamWriter? _writer;

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
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [{source ?? "?"}] {Redact(msg)}";
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

    public static void StartNewSessionLog()
    {
        lock (_lock)
        {
            try
            {
                _writer?.Dispose();
                _writer = null;
                Directory.CreateDirectory(LogDir);
                var current = Path.Combine(LogDir, "app-current.log");
                var previous = Path.Combine(LogDir, "app-previous.log");
                if (File.Exists(previous)) File.Delete(previous);
                if (File.Exists(current)) File.Move(current, previous);
            }
            catch { }
        }
    }

    public static void ClearAllLogs()
    {
        lock (_lock)
        {
            try
            {
                _writer?.Dispose();
                _writer = null;
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
        if (_writer is not null) return;

        _writer?.Dispose();
        Directory.CreateDirectory(LogDir);
        var path = Path.Combine(LogDir, "app-current.log");
        _writer = new StreamWriter(path, append: true) { AutoFlush = true };

        // 清理 7 天前的日志
        try
        {
            foreach (var f in Directory.GetFiles(LogDir, "*.log"))
            {
                if (File.GetLastWriteTime(f) < DateTime.Now.Date.AddDays(-7))
                    File.Delete(f);
            }
        }
        catch { }
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var jsonRedacted = SensitiveJsonRegex.Replace(value, "$1\"***\"");
        return SensitiveHeaderRegex.Replace(jsonRedacted, "$1=***");
    }
}
