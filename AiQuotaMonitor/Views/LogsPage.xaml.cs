using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AiQuotaMonitor.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiQuotaMonitor.Views;

public sealed partial class LogsPage : Page, INotifyPropertyChanged
{
    private string _searchText = string.Empty;

    public ObservableCollection<LogRow> Entries { get; } = new();
    public ObservableCollection<LogRow> FilteredEntries { get; } = new();
    public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); ApplyFilter(); } }
    public string CountText => $"{FilteredEntries.Count} / {Entries.Count} 行";
    public string LogPathText => AppPaths.LogDirectory;
    public bool HasNoLogs => FilteredEntries.Count == 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LogsPage()
    {
        InitializeComponent();
        DataContext = this;
        LoadLogs();
        Loaded += (_, _) => LoadLogs();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadLogs();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AppLogger.ClearAllLogs();
            Entries.Clear();
            ApplyFilter();
            AppLogger.Info("日志已由用户清空");
        }
        catch (Exception ex)
        {
            AppLogger.Error("清空日志失败", ex);
            LoadLogs();
        }
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.LogDirectory,
            UseShellExecute = true,
        });
    }

    private void LoadLogs()
    {
        Entries.Clear();
        try
        {
            if (Directory.Exists(AppPaths.LogDirectory))
            {
                foreach (var line in Directory.GetFiles(AppPaths.LogDirectory, "*.log")
                             .OrderByDescending(File.GetLastWriteTime)
                             .Take(3)
                             .SelectMany(ReadTail)
                             .TakeLast(500))
                {
                    Entries.Add(LogRow.Parse(line));
                }
            }
        }
        catch (Exception ex)
        {
            Entries.Add(new LogRow { Time = DateTime.Now.ToString("HH:mm:ss.fff"), Level = "ERROR", Source = "LogsPage", Message = ex.Message });
        }
        ApplyFilter();
    }

    private static IEnumerable<string> ReadTail(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var lines = new Queue<string>(300);
            while (reader.ReadLine() is { } line)
            {
                if (lines.Count == 300) lines.Dequeue();
                lines.Enqueue(line);
            }
            return lines.ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        FilteredEntries.Clear();
        foreach (var row in Entries.Where(r => string.IsNullOrEmpty(q) || r.Raw.Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredEntries.Add(row);
        }
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasNoLogs));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class LogRow
{
    private static readonly Regex LineRegex = new(@"^\[(?<time>[^\]]+)\] \[(?<level>[^\]]+)\] \[(?<source>[^\]]+)\] (?<message>.*)$", RegexOptions.Compiled);

    public string Time { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO";
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Raw => $"{Time} {Level} {Source} {Message}";
    public string LevelColor => Level switch
    {
        "ERROR" => "#EF4444",
        "WARN" => "#F59E0B",
        "SWALLOWED" => "#8B5CF6",
        _ => "#0EA5E9",
    };

    public static LogRow Parse(string line)
    {
        var match = LineRegex.Match(line);
        if (!match.Success) return new LogRow { Time = "--", Level = "LOG", Source = "file", Message = line };
        return new LogRow
        {
            Time = match.Groups["time"].Value,
            Level = match.Groups["level"].Value,
            Source = match.Groups["source"].Value,
            Message = match.Groups["message"].Value,
        };
    }
}
