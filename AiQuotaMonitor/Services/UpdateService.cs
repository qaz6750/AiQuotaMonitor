using System.Diagnostics;
using System.Text.Json;
using AiQuotaMonitor.Helpers;

namespace AiQuotaMonitor.Services;

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    string Message);

/// <summary>Checks GitHub Releases for newer published versions.</summary>
public sealed class UpdateService
{
    public static UpdateService Instance { get; } = new();

    public const string ReleasesUrl = "https://github.com/qaz6750/AiQuotaMonitor/releases";
    private const string LatestReleaseApi = "https://api.github.com/repos/qaz6750/AiQuotaMonitor/releases/latest";
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    private UpdateService() { }

    public async Task<UpdateCheckResult> CheckAsync()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        req.Headers.TryAddWithoutValidation("User-Agent", "AiQuotaMonitor/1.0");

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GitHub Release 检查失败 (HTTP {(int)resp.StatusCode})。");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var tag = Str(root, "tag_name") ?? Str(root, "name") ?? string.Empty;
        var url = Str(root, "html_url") ?? ReleasesUrl;
        var latest = NormalizeVersion(tag);
        var current = NormalizeVersion(BuildInfo.Version);
        if (string.IsNullOrWhiteSpace(latest))
        {
            throw new InvalidOperationException("GitHub Release 未返回版本号。");
        }

        var hasUpdate = CompareVersions(latest, current) > 0;
        return new UpdateCheckResult(
            hasUpdate,
            current,
            latest,
            url,
            hasUpdate ? $"发现新版本 v{latest}" : $"已是最新版本 v{current}");
    }

    public void OpenReleases(string? url = null)
    {
        var target = string.IsNullOrWhiteSpace(url) ? ReleasesUrl : url;
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private static string NormalizeVersion(string value)
    {
        var v = value.Trim();
        if (v.StartsWith('v') || v.StartsWith('V')) v = v[1..];
        var dash = v.IndexOf('-');
        if (dash >= 0) v = v[..dash];
        var plus = v.IndexOf('+');
        if (plus >= 0) v = v[..plus];
        return v.Trim();
    }

    private static int CompareVersions(string latest, string current)
    {
        if (Version.TryParse(latest, out var l) && Version.TryParse(current, out var c)) return l.CompareTo(c);
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Str(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String) return p.GetString();
        return null;
    }
}
