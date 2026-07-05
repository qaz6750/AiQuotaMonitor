using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;
using Microsoft.UI.Xaml;
using AiQuotaMonitor.Services;
using AiQuotaMonitor.Views;

namespace AiQuotaMonitor.ViewModels;

/// <summary>账号列表展示行（包装 GlmAccount + 当前激活标记）。</summary>
public sealed class AccountRow
{
    public GlmAccount Account { get; init; } = new();
    public bool IsCurrent { get; set; }
    public string Name => Account.DisplayLabel;
    public string PlanBadge => Account.PlanBadge;
    public string ProviderName => Account.Provider.Name;
    public string ProviderGlyph => Account.Provider.Glyph;
    public string ProviderIconGlyph => Account.Provider.IconGlyph;
    public string ProviderColor => Account.Provider.BrandColor;
    public string KeyHint => CookieIdentityFormatter.CredentialHint(Account);
}

/// <summary>设置页提供商下拉项。</summary>
public sealed class ProviderOption
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Glyph { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;
    public string BrandColor { get; init; } = "#5B8DEF";
    public string Description { get; init; } = string.Empty;
    public PlanType PlanType { get; init; }
}

/// <summary>设置页 ViewModel：多账号管理 + Plan 类型切换 + 全局刷新偏好。</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _s = SettingsService.Instance;

    public ObservableCollection<AccountRow> Accounts { get; } = new();
    public ObservableCollection<ProviderOption> ProviderOptions { get; } = new();

    public string VersionText => BuildInfo.DisplayBuild;
    public string CommitText => BuildInfo.ShortCommit;
    public string SettingsPathText => _s.SettingsFilePath;
    public string DataDirectoryText => _s.SettingsDirectory;
    public string RuntimeText => $"{Environment.OSVersion.VersionString} · .NET {Environment.Version}";
    public string AccountStatsText => Accounts.Count == 0
        ? "尚未保存账号"
        : $"{Accounts.Count} 个账号 · {Accounts.Select(a => a.ProviderName).Distinct().Count()} 个提供商";

    // ===== 编辑表单 =====
    [ObservableProperty] private string _editingName = "";
    [ObservableProperty] private string _editingProviderId = "glm";
    [ObservableProperty] private PlanType _editingPlan = PlanType.Coding;
    [ObservableProperty] private string _editingApiKey = "";
    [ObservableProperty] private string _editingBaseUrl = "https://open.bigmodel.cn";
    [ObservableProperty] private string? _editingId;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editingHeader = "新建账号";
    [ObservableProperty] private string? _saveMessage;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private bool _hasAccounts;

    /// <summary>ComboBox 绑定用：0=Coding, 1=Token, 2=按量付费。</summary>
    public int EditingPlanIndex
    {
        get => (int)EditingPlan;
        set
        {
            var normalized = Enum.IsDefined(typeof(PlanType), value) ? (PlanType)value : PlanType.Coding;
            if (EditingPlan != normalized)
            {
                EditingPlan = normalized;
                OnPropertyChanged();
            }
        }
    }

    partial void OnEditingPlanChanged(PlanType value)
    {
        RefreshProviderOptions();
        if (ProviderOptions.Count > 0 && !ProviderOptions.Any(p => p.Id.Equals(EditingProviderId, StringComparison.OrdinalIgnoreCase)))
        {
            ApplyProvider(ProviderOptions[0].Id, forcePlan: false);
        }
        OnPropertyChanged(nameof(EditingPlanIndex));
        NotifyProviderChanged();
    }

    /// <summary>当前编辑的提供商是否为 Cookie 鉴权（MiMo，隐藏 URL）。</summary>
    public bool IsCookieProvider => Providers.GetById(EditingProviderId).Capabilities.IsCookieAuth;
    public bool CanAutoFetchCredential => IsCookieProvider || Providers.GetById(EditingProviderId).Capabilities.SupportsCredentialAutoFetch;
    public string CredentialLabel => Providers.GetById(EditingProviderId).Capabilities.CredentialLabel;
    public string CredentialHelpText
    {
        get
        {
            var p = Providers.GetById(EditingProviderId);
            if (p.Capabilities.IsCookieAuth || p.Capabilities.SupportsCredentialAutoFetch)
            {
                return "Cookie 登录会优先用网页登录账户名/用户 ID 作为默认账号显示名；可手动填写账号名称覆盖。";
            }
            return p.SupportedPlan == PlanType.PayAsYouGo ? "按量付费平台使用官方 API/Admin Key，不采集网页登录 Cookie。" : "订阅/Token 平台使用官方 Key 或 Token 查询可用额度。";
        }
    }

    /// <summary>当前编辑的提供商是否需要 URL 输入（Cookie 鉴权的隐藏）。</summary>
    public bool ShowBaseUrl => !IsCookieProvider;

    /// <summary>通知 UI 提供商相关属性已变更。</summary>
    private void NotifyProviderChanged()
    {
        OnPropertyChanged(nameof(IsCookieProvider));
        OnPropertyChanged(nameof(CanAutoFetchCredential));
        OnPropertyChanged(nameof(ShowBaseUrl));
        OnPropertyChanged(nameof(CredentialLabel));
        OnPropertyChanged(nameof(CredentialHelpText));
        OnPropertyChanged(nameof(EditingProviderIndex));
        OnPropertyChanged(nameof(EditingPlanIndex));
    }

    private void RefreshProviderOptions()
    {
        ProviderOptions.Clear();
        foreach (var p in Providers.All.Where(p => p.SupportedPlan == EditingPlan))
        {
            ProviderOptions.Add(new ProviderOption
            {
                Id = p.Id,
                DisplayName = p.Name,
                Glyph = p.Glyph,
                IconGlyph = p.IconGlyph,
                BrandColor = p.BrandColor,
                Description = p.Capabilities.CredentialLabel,
                PlanType = p.SupportedPlan,
            });
        }
    }

    private void ApplyProvider(string providerId, bool forcePlan)
    {
        var provider = Providers.GetById(providerId);
        EditingProviderId = provider.Id;
        if (forcePlan && EditingPlan != provider.SupportedPlan)
        {
            EditingPlan = provider.SupportedPlan;
        }
        EditingBaseUrl = provider.DefaultBaseUrl;
        NotifyProviderChanged();
    }

    /// <summary>一键获取凭据：Cookie 鉴权提供商打开 WebView2 提取 Cookie。</summary>
    [RelayCommand]
    private async Task FetchCredentialAsync()
    {
        try
        {
            var url = Providers.GetById(EditingProviderId).DocsUrl ?? "https://platform.xiaomimimo.com/console/plan-manage";
            var win = new MiMoLoginWindow(url);
            win.CookieReady += cookie =>
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    EditingApiKey = cookie;
                    SaveMessage = "✓ 已自动获取 cookie；若 Cookie 中包含账号名/用户 ID，将作为默认显示名";
                });
            };
            win.Activate();
            await win.StartAsync();
        }
        catch (Exception ex) { SaveMessage = "无法打开登录窗口：" + ex.Message; }
    }

    /// <summary>ComboBox 绑定用：0=智谱 GLM，1=小米 MiMo，2=自定义。</summary>
    public int EditingProviderIndex
    {
        get
        {
            var idx = ProviderOptions.ToList().FindIndex(p => p.Id.Equals(EditingProviderId, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 ? idx : 0;
        }
        set
        {
            var newId = value >= 0 && value < ProviderOptions.Count ? ProviderOptions[value].Id : "glm";
            if (EditingProviderId != newId)
            {
                ApplyProvider(newId, forcePlan: false);
                OnPropertyChanged();
            }
        }
    }

    // ===== 全局偏好 =====
    [ObservableProperty] private int _refreshInterval = 10;
    [ObservableProperty] private bool _enableRetry = true;
    [ObservableProperty] private bool _autoRefresh = true;
    [ObservableProperty] private bool _warnOnHighUsage = true;
    /// <summary>主题选择：0=跟随系统，1=浅色，2=深色。</summary>
    [ObservableProperty] private int _appThemeIndex;
    [ObservableProperty] private int _warnThreshold = 80;

    public SettingsViewModel()
    {
        RefreshProviderOptions();
        RefreshAccounts();
        RefreshInterval = _s.RefreshIntervalMinutes;
        EnableRetry = _s.EnableRetry;
        AutoRefresh = _s.AutoRefresh;
        WarnOnHighUsage = _s.WarnOnHighUsage;
        AppThemeIndex = _s.AppTheme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        WarnThreshold = _s.WarnThreshold;
        _s.AccountsChanged += OnAccountsChanged;
    }

    private void OnAccountsChanged() => RefreshAccounts();

    private void RefreshAccounts()
    {
        Accounts.Clear();
        var activeId = _s.ActiveAccount?.Id;
        foreach (var a in _s.Accounts)
        {
            Accounts.Add(new AccountRow { Account = a, IsCurrent = a.Id == activeId });
        }
        HasAccounts = Accounts.Count > 0;
        OnPropertyChanged(nameof(AccountStatsText));
    }

    /// <summary>页面卸载时取消事件订阅。</summary>
    public void Detach() => _s.AccountsChanged -= OnAccountsChanged;

    /// <summary>从导航参数初始化（WelcomePage 传入 PlanType 时进入新建模式）。</summary>
    public void ApplyNavigationParameter(object? param)
    {
        if (param is not null) StartAdd();
    }

    [RelayCommand]
    private void StartAdd()
    {
        EditingId = null;
        EditingName = "";
        EditingPlan = Providers.Glm.SupportedPlan;
        RefreshProviderOptions();
        EditingProviderId = Providers.Glm.Id;
        EditingApiKey = "";
        EditingBaseUrl = Providers.Glm.DefaultBaseUrl;
        EditingHeader = "新建账号";
        IsEditing = true;
        SaveMessage = null;
        NotifyProviderChanged();
    }

    [RelayCommand]
    private void EditAccount(AccountRow? row)
    {
        if (row is null) return;
        var acc = row.Account;
        EditingId = acc.Id;
        EditingName = acc.Name;
        EditingPlan = acc.PlanType;
        RefreshProviderOptions();
        EditingProviderId = acc.ProviderId;
        EditingApiKey = acc.ApiKey;
        EditingBaseUrl = acc.BaseUrl;
        EditingHeader = "编辑账号";
        IsEditing = true;
        SaveMessage = null;
        NotifyProviderChanged();
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SaveAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingApiKey))
        {
            SaveMessage = $"⚠ 请填写 {CredentialLabel}";
            return;
        }
        if (Providers.GetById(EditingProviderId).IsCustom && string.IsNullOrWhiteSpace(EditingBaseUrl))
        {
            SaveMessage = "⚠ 自定义提供商需填写 Base URL";
            return;
        }
        IsTesting = true;
        try
        {
            if (EditingId is null)
            {
                var acc = _s.AddAccount(EditingName, EditingProviderId, EditingPlan, EditingApiKey, EditingBaseUrl);
                _s.SetActive(acc.Id);
                SaveMessage = "✓ 已添加并切换为新账号";
            }
            else
            {
                _s.UpdateAccount(EditingId, EditingName, EditingProviderId, EditingPlan, EditingApiKey, EditingBaseUrl);
                SaveMessage = "✓ 账号已更新";
            }
            UsageDataService.Instance.StartAutoRefresh();
            await UsageDataService.Instance.RefreshAsync();
            IsEditing = false;
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(AccountRow? row)
    {
        if (row is null) return;
        var id = row.Account.Id;
        var wasCurrent = _s.ActiveAccount?.Id == id;
        _s.RemoveAccount(id);
        if (IsEditing && EditingId == id) IsEditing = false;
        if (wasCurrent)
        {
            UsageDataService.Instance.Clear();
            UsageDataService.Instance.StartAutoRefresh();
            if (_s.HasApiKey) await UsageDataService.Instance.RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task ActivateAccountAsync(AccountRow? row)
    {
        if (row is null) return;
        if (_s.SetActive(row.Account.Id))
        {
            UsageDataService.Instance.StartAutoRefresh();
            await UsageDataService.Instance.RefreshAsync();
        }
    }

    [RelayCommand]
    private void SavePreferences()
    {
        _s.SetRefreshInterval(RefreshInterval);
        _s.SetEnableRetry(EnableRetry);
        _s.SetAutoRefresh(AutoRefresh);
        _s.SetWarnOnHighUsage(WarnOnHighUsage);
        _s.SetWarnThreshold(WarnThreshold);
        _s.SetAppTheme(AppThemeIndex switch { 1 => "Light", 2 => "Dark", _ => "System" });
        ApplyThemeToRoot();
        UsageDataService.Instance.StartAutoRefresh();
        SaveMessage = "✓ 偏好已保存";
    }

    /// <summary>把当前主题应用到主窗口根元素。</summary>
    private static void ApplyThemeToRoot()
    {
        try
        {
            var theme = SettingsService.Instance.AppTheme;
            var em = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            if (App.MainWindow.Content is FrameworkElement fe) fe.RequestedTheme = em;
            ThemeHelper.UpdateTitleBarTheme(App.MainWindow);
        }
        catch { /* 主题应用失败不影响功能 */ }
    }

    [RelayCommand]
    private void ResetAll()
    {
        _s.ResetAll();
        UsageDataService.Instance.StopAutoRefresh();
        UsageDataService.Instance.Clear();
        App.MainWindowNavigate(typeof(WelcomePage));
    }

    [RelayCommand]
    private void GoOverview()
    {
        if (_s.HasApiKey) App.MainWindowNavigate(typeof(OverviewPage));
        else App.MainWindowNavigate(typeof(WelcomePage));
    }
}
