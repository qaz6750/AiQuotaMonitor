using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;
using Microsoft.UI.Xaml;

namespace AiQuotaMonitor.Services;

/// <summary>
/// 全局用量数据服务（单例）。负责拉取、缓存、定时刷新，并向所有 ViewModel 广播更新。
/// 设计上对应 cc-bar 的 AppState —— 概览页、统计页、悬浮 HUD 共享同一份数据。
/// </summary>
public sealed class UsageDataService
{
    public static UsageDataService Instance { get; } = new();

    private DispatcherTimer? _timer;
    private string? _lastAccountId;
    private SemaphoreSlim? _refreshLock;
    private bool _refreshing;

    public UsageResult? Current { get; private set; }
    public string? CurrentAccountId => _lastAccountId;
    public DateTimeOffset? LastUpdated { get; private set; }
    public bool IsLoading { get; private set; }
    public string? LastError { get; private set; }

    public event Action? Updated;
    public event Action? LoadingChanged;

    private UsageDataService() { }

    /// <summary>立即刷新一次。失败时记录 LastError 但不抛出。并发调用自动去重。</summary>
    public async Task RefreshAsync()
    {
        // 并发去重：多 ViewModel 同时触发刷新时只执行一次
        _refreshLock ??= new SemaphoreSlim(1, 1);
        await _refreshLock.WaitAsync();
        try
        {
            if (_refreshing) return;
            _refreshing = true;
        }
        finally { _refreshLock.Release(); }

        try
        {
            await RefreshCoreAsync();
        }
        finally
        {
            await _refreshLock.WaitAsync();
            try { _refreshing = false; }
            finally { _refreshLock.Release(); }
        }
    }

    private async Task RefreshCoreAsync()
    {
        var settings = SettingsService.Instance;
        var acc = settings.ActiveAccount;
        if (acc is null || !acc.HasKey)
        {
            Current = null;
            LastError = "尚未配置凭据，请先到「设置」填写。";
            Updated?.Invoke();
            return;
        }

        // 账号切换时丢弃上一个账号的缓存数据，避免展示串号
        var currentId = acc.Id;
        if (currentId != _lastAccountId)
        {
            Current = null;
            _lastAccountId = currentId;
        }

        IsLoading = true;
        LoadingChanged?.Invoke();
        Updated?.Invoke();

        try
        {
            AppLogger.Info($"刷新账号: {acc.Provider.Name} ({acc.Id[..6]})");
            // 按平台分发：GLM 用 GlmClient，MiMo 用 MiMoClient
            var client = PlatformClientFactory.Get(acc);
            var result = await client.QueryUsageAsync(acc.ApiKey, acc.BaseUrl, settings.EnableRetry);
            Current = result;
            LastUpdated = DateTimeOffset.Now;
            LastError = null;
        }
        catch (Exception ex)
        {
            AppLogger.Error("刷新失败", ex);
            LastError = ex.Message;
            // 保留上次成功的 Current，UI 可继续显示旧数据
        }
        finally
        {
            IsLoading = false;
            LoadingChanged?.Invoke();
            Updated?.Invoke();
        }
    }

    /// <summary>按设置启动定时自动刷新。重复调用会先停止旧计时器。</summary>
    public void StartAutoRefresh()
    {
        StopAutoRefresh();
        var settings = SettingsService.Instance;
        if (!settings.AutoRefresh) return;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(Math.Max(1, settings.RefreshIntervalMinutes)),
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
    }

    public void StopAutoRefresh()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer = null;
        }
    }
}
