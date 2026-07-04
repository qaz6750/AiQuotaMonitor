using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiQuotaMonitor.Helpers;
using AiQuotaMonitor.Models;

namespace AiQuotaMonitor.Services;

/// <summary>本地设置快照（序列化到 JSON 文件）。支持多账号 + 全局偏好。</summary>
public sealed class SettingsSnapshot
{
    [JsonPropertyName("accounts")] public List<AccountRecord>? Accounts { get; set; }
    [JsonPropertyName("activeAccountId")] public string? ActiveAccountId { get; set; }

    [JsonPropertyName("refreshInterval")] public int? RefreshInterval { get; set; }
    [JsonPropertyName("enableRetry")] public bool? EnableRetry { get; set; }
    [JsonPropertyName("autoRefresh")] public bool? AutoRefresh { get; set; }
    [JsonPropertyName("warnOnHighUsage")] public bool? WarnOnHighUsage { get; set; }
    [JsonPropertyName("appTheme")] public string? AppTheme { get; set; }
    [JsonPropertyName("warnThreshold")] public int? WarnThreshold { get; set; }

    // —— 旧版（≤ 单账号）字段，仅用于一次性迁移 ——
    [JsonPropertyName("apiKeyEnc")] public string? LegacyApiKeyEnc { get; set; }
    [JsonPropertyName("baseUrl")] public string? LegacyBaseUrl { get; set; }
    [JsonPropertyName("platform")] public string? LegacyPlatform { get; set; }
}

/// <summary>
/// 全局设置单例。持久化到应用当前目录 data\settings.json。
/// 支持多账号管理：每个账号独立保存 API Key（DPAPI/CurrentUser 加密）与 BaseUrl，
/// 全局偏好（刷新间隔、重试、自动刷新、高用量警告）跨账号共享。
/// </summary>
public sealed class SettingsService
{
    public static SettingsService Instance { get; } = new();

    private static readonly string Dir = AppPaths.DataDirectory;
    private static readonly string FilePath = AppPaths.SettingsFile;
    private static readonly string LegacyDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AiQuotaMonitor");
    private static readonly string LegacyFilePath = Path.Combine(LegacyDir, "settings.json");

    public string SettingsDirectory => Dir;
    public string SettingsFilePath => FilePath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly List<GlmAccount> _accounts = new();
    private string? _lastSavedJson;

    /// <summary>全部已保存的账号。</summary>
    public IReadOnlyList<GlmAccount> Accounts => _accounts;

    /// <summary>当前活跃账号（数据刷新以此账号的凭据为准）。</summary>
    public GlmAccount? ActiveAccount { get; private set; }

    public bool HasAccounts => _accounts.Count > 0;

    // ===== 当前活跃账号的快捷访问（保持旧 API 兼容，减少上层改动） =====
    public string ApiKey => ActiveAccount?.ApiKey ?? string.Empty;
    public string BaseUrl => ActiveAccount?.BaseUrl ?? "https://open.bigmodel.cn";
    public bool HasApiKey => ActiveAccount?.HasKey ?? false;
    public bool IsConfigured => HasApiKey;
    public PlanType ActivePlanType => ActiveAccount?.PlanType ?? PlanType.Coding;

    // ===== 全局偏好 =====
    public int RefreshIntervalMinutes { get; private set; } = 10;
    public bool EnableRetry { get; private set; } = true;
    public bool AutoRefresh { get; private set; } = true;
    public bool WarnOnHighUsage { get; private set; } = true;
    /// <summary>应用主题：System / Light / Dark。</summary>
    public string AppTheme { get; private set; } = "System";
    /// <summary>高用量警告阈值（百分比）。</summary>
    public int WarnThreshold { get; private set; } = 80;

    /// <summary>全局设置变更事件。</summary>
    public event Action? Changed;
    /// <summary>账号列表或活跃账号变更事件。</summary>
    public event Action? AccountsChanged;

    private SettingsService()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            Load();
        }
        catch
        {
            LoadDefaults();
        }
    }

    private void Load()
    {
        var loadPath = File.Exists(FilePath) ? FilePath
            : File.Exists(LegacyFilePath) ? LegacyFilePath
            : null;

        if (loadPath is null)
        {
            LoadDefaults();
            return;
        }

        try
        {
            var json = File.ReadAllText(loadPath);
            var s = JsonSerializer.Deserialize<SettingsSnapshot>(json, JsonOpts) ?? new SettingsSnapshot();

            RefreshIntervalMinutes = s.RefreshInterval ?? 10;
            EnableRetry = s.EnableRetry ?? true;
            AutoRefresh = s.AutoRefresh ?? true;
            WarnOnHighUsage = s.WarnOnHighUsage ?? true;
            AppTheme = string.IsNullOrWhiteSpace(s.AppTheme) ? "System" : s.AppTheme;
            WarnThreshold = s.WarnThreshold ?? 80;

            _accounts.Clear();
            if (s.Accounts is { Count: > 0 })
            {
                foreach (var r in s.Accounts)
                {
                    _accounts.Add(new GlmAccount
                    {
                        Id = string.IsNullOrWhiteSpace(r.Id) ? Guid.NewGuid().ToString("N") : r.Id,
                        Name = r.Name ?? string.Empty,
                        ProviderId = string.IsNullOrWhiteSpace(r.ProviderId) ? "glm" : r.ProviderId,
                        PlanType = PlanTypeExtensions.Parse(r.PlanType),
                        ApiKey = UnprotectBase64(r.ApiKeyEnc),
                        BaseUrl = string.IsNullOrWhiteSpace(r.BaseUrl) ? "https://open.bigmodel.cn" : r.BaseUrl,
                        CreatedAt = r.CreatedAt == default ? DateTime.Now : r.CreatedAt,
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(s.LegacyApiKeyEnc))
            {
                // 旧版单账号一次性迁移
                _accounts.Add(new GlmAccount
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "默认账号",
                    PlanType = PlanType.Coding,
                    ApiKey = UnprotectBase64(s.LegacyApiKeyEnc),
                    BaseUrl = string.IsNullOrWhiteSpace(s.LegacyBaseUrl) ? "https://open.bigmodel.cn" : s.LegacyBaseUrl,
                    CreatedAt = DateTime.Now,
                });
            }

            ActiveAccount = _accounts.FirstOrDefault(a => a.Id == s.ActiveAccountId)
                            ?? _accounts.FirstOrDefault();

            // 从旧 LocalAppData 成功加载时，自动写入当前目录 data\settings.json，完成一次性便携迁移。
            if (!string.Equals(loadPath, FilePath, StringComparison.OrdinalIgnoreCase))
            {
                Save();
            }
            else
            {
                _lastSavedJson = JsonSerializer.Serialize(CreateSnapshot(), JsonOpts);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Swallowed("设置加载", ex);
            LoadDefaults();
        }
    }

    private void LoadDefaults()
    {
        _accounts.Clear();
        ActiveAccount = null;
        RefreshIntervalMinutes = 10;
        EnableRetry = true;
        AutoRefresh = true;
        WarnOnHighUsage = true;
        AppTheme = "System";
        WarnThreshold = 80;
    }

    // ===== 账号管理 =====

    public GlmAccount AddAccount(string name, string providerId, PlanType planType, string apiKey, string baseUrl)
    {
        var provider = Providers.GetById(providerId);
        var acc = new GlmAccount
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name?.Trim() ?? string.Empty,
            ProviderId = provider.Id,
            PlanType = planType,
            ApiKey = apiKey?.Trim() ?? string.Empty,
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? provider.DefaultBaseUrl : baseUrl.Trim().TrimEnd('/'),
            CreatedAt = DateTime.Now,
        };
        _accounts.Add(acc);
        if (ActiveAccount is null) ActiveAccount = acc;
        Save();
        AccountsChanged?.Invoke();
        Changed?.Invoke();
        return acc;
    }

    public void UpdateAccount(string id, string? name, string? providerId, PlanType? planType, string? apiKey, string? baseUrl)
    {
        var acc = _accounts.FirstOrDefault(a => a.Id == id);
        if (acc is null) return;
        if (name is not null) acc.Name = name.Trim();
        if (providerId is not null) acc.ProviderId = Providers.GetById(providerId).Id;
        if (planType is not null) acc.PlanType = planType.Value;
        if (apiKey is not null) acc.ApiKey = apiKey.Trim();
        if (baseUrl is not null && !string.IsNullOrWhiteSpace(baseUrl)) acc.BaseUrl = baseUrl.Trim().TrimEnd('/');
        Save();
        AccountsChanged?.Invoke();
        Changed?.Invoke();
    }

    public void RemoveAccount(string id)
    {
        var acc = _accounts.FirstOrDefault(a => a.Id == id);
        if (acc is null) return;
        _accounts.Remove(acc);
        if (ActiveAccount?.Id == id)
        {
            ActiveAccount = _accounts.FirstOrDefault();
        }
        Save();
        AccountsChanged?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>切换活跃账号。返回是否真的发生了切换。</summary>
    public bool SetActive(string id)
    {
        var acc = _accounts.FirstOrDefault(a => a.Id == id);
        if (acc is null || ActiveAccount?.Id == id) return false;
        ActiveAccount = acc;
        Save();
        AccountsChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }

    // ===== 全局偏好 setter =====

    public void SetRefreshInterval(int minutes)
    {
        RefreshIntervalMinutes = Math.Clamp(minutes, 1, 1440);
        Save();
        Changed?.Invoke();
    }

    public void SetEnableRetry(bool v) { EnableRetry = v; Save(); Changed?.Invoke(); }
    public void SetWarnOnHighUsage(bool v) { WarnOnHighUsage = v; Save(); Changed?.Invoke(); }
    public void SetAutoRefresh(bool v) { AutoRefresh = v; Save(); Changed?.Invoke(); }
    public void SetAppTheme(string t)
    {
        AppTheme = string.IsNullOrWhiteSpace(t) ? "System" : t;
        Save();
        Changed?.Invoke();
    }
    public void SetWarnThreshold(int v) { WarnThreshold = Math.Clamp(v, 1, 100); Save(); Changed?.Invoke(); }

    /// <summary>清理全部数据：所有账号、凭据、偏好与本地缓存（重置为初始状态）。</summary>
    public void ResetAll()
    {
        _accounts.Clear();
        ActiveAccount = null;
        LoadDefaults();
        try
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, recursive: true);
            if (Directory.Exists(LegacyDir)) Directory.Delete(LegacyDir, recursive: true);
        }
        catch (Exception ex) { AppLogger.Swallowed("ResetAll删除", ex); }
        AccountsChanged?.Invoke();
        Changed?.Invoke();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var snap = CreateSnapshot();
            var json = JsonSerializer.Serialize(snap, JsonOpts);

            // 避免偏好页多项设置连续保存时重复写盘。
            if (string.Equals(json, _lastSavedJson, StringComparison.Ordinal)) return;

            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
            _lastSavedJson = json;
        }
        catch (Exception ex)
        {
            AppLogger.Error("设置保存失败", ex);
        }
    }

    private SettingsSnapshot CreateSnapshot() => new()
    {
        Accounts = _accounts.Select(a => new AccountRecord
        {
            Id = a.Id,
            Name = a.Name,
            ProviderId = a.ProviderId,
            PlanType = PlanTypeExtensions.Encode(a.PlanType),
            ApiKeyEnc = ProtectBase64(a.ApiKey),
            BaseUrl = a.BaseUrl,
            CreatedAt = a.CreatedAt,
        }).ToList(),
        ActiveAccountId = ActiveAccount?.Id,
        RefreshInterval = RefreshIntervalMinutes,
        EnableRetry = EnableRetry,
        AutoRefresh = AutoRefresh,
        WarnOnHighUsage = WarnOnHighUsage,
        AppTheme = AppTheme,
        WarnThreshold = WarnThreshold,
    };

    // ===== DPAPI 加解密（保留原实现） =====

    private static string ProtectBase64(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var cipher = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipher);
        }
        catch (Exception ex)
        {
            AppLogger.Error("凭据加密失败", ex);
            throw new CryptographicException("凭据加密失败，已阻止以弱保护形式保存。", ex);
        }
    }

    private static string UnprotectBase64(string? enc)
    {
        if (string.IsNullOrWhiteSpace(enc)) return string.Empty;
        try
        {
            var cipher = Convert.FromBase64String(enc);
            var plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(enc)); }
            catch (Exception ex) { AppLogger.Swallowed("UnprotectBase64", ex); return string.Empty; }
        }
    }
}
