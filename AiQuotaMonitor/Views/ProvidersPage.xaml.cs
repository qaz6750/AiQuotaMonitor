using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AiQuotaMonitor.Models;
using AiQuotaMonitor.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AiQuotaMonitor.Views;

public sealed class ProviderRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LogoText { get; set; } = string.Empty;
    public double LogoFontSize { get; set; }
    public string LogoPath { get; set; } = string.Empty;
    public string BrandColor { get; set; } = "#0EA5E9";
    public int AccountCount { get; set; }
    public string StatusText => AccountCount > 0 ? "已接入" : "可接入";
    public string StatusBrush => AccountCount > 0 ? "#22C55E" : "#64748B";
    public string PlanText { get; set; } = string.Empty;
    public string CapabilityText { get; set; } = string.Empty;
}

public sealed partial class ProvidersPage : Page, INotifyPropertyChanged
{
    public ObservableCollection<ProviderRow> Rows { get; } = new();
    public string ConnectedText => SettingsService.Instance.Accounts.Count.ToString();
    public string ProviderCountText => Providers.All.Count.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProvidersPage()
    {
        InitializeComponent();
        DataContext = this;
        LoadRows();
        SettingsService.Instance.AccountsChanged += OnAccountsChanged;
        Loaded += (_, _) => RefreshAll();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RefreshAll();
    }

    private void OnAccountsChanged() => RefreshAll();

    private void RefreshAll()
    {
        LoadRows();
        OnPropertyChanged(nameof(ConnectedText));
        OnPropertyChanged(nameof(ProviderCountText));
    }

    private void LoadRows()
    {
        Rows.Clear();
        var accounts = SettingsService.Instance.Accounts;
        foreach (var p in Providers.All)
        {
            var caps = p.Capabilities;
            Rows.Add(new ProviderRow
            {
                Id = p.Id,
                Name = p.Name,
                LogoText = p.LogoText,
                LogoFontSize = p.LogoFontSize,
                LogoPath = p.LogoPath,
                BrandColor = p.BrandColor,
                AccountCount = accounts.Count(a => a.ProviderId.Equals(p.Id, StringComparison.OrdinalIgnoreCase)),
                PlanText = p.SupportedPlan.DisplayName(),
                CapabilityText = string.Join(" · ", new[]
                {
                    caps.CredentialLabel,
                    caps.PrimaryQuotaLabel,
                    caps.SecondaryQuotaLabel,
                    caps.HasTrend ? "趋势" : null,
                    caps.HasCost ? "费用" : null,
                    caps.SupportsCredentialAutoFetch ? "一键获取" : null,
                }.Where(x => !string.IsNullOrWhiteSpace(x))),
            });
        }
    }

    private void AddAccount_Click(object sender, RoutedEventArgs e) => App.MainWindowNavigate(typeof(SettingsPage), new object());

    private void ProviderAdd_Click(object sender, RoutedEventArgs e) => App.MainWindowNavigate(typeof(SettingsPage), new object());

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
