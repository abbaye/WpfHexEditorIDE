// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: UI/MarketplacePanelViewModel.cs
// Description:
//     ViewModel for the Marketplace dockable panel — v2.
//     3-tab layout: Browse / Installed / Updates.
//     Orchestrates IMarketplaceService: search, install, uninstall,
//     SHA-256 progress, update detection.
// Architecture Notes:
//     Pattern: MVVM + RelayCommand (SDK).
//     IMarketplaceService is the sole dependency (no concrete impl ref).
//     All async ops run on thread-pool; UI mutations via Dispatcher.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.SDK;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// ViewModel for the Plugin Marketplace panel (v2 — Browse/Installed/Updates).
/// </summary>
public sealed class MarketplacePanelViewModel : INotifyPropertyChanged
{
    private readonly IMarketplaceService _marketplace;
    private readonly WpfPluginHost       _host;
    private readonly Action<string>      _log;

    // ── Browse tab state ──────────────────────────────────────────────────────

    private string              _searchQuery      = string.Empty;
    private string              _selectedCategory = "All";
    private bool                _showVerifiedOnly;
    private MarketplaceListing? _selectedBrowseListing;

    public ObservableCollection<MarketplaceListing> AllListings      { get; } = [];
    public ObservableCollection<MarketplaceListing> FilteredListings { get; } = [];
    public ObservableCollection<string>             Categories        { get; } = ["All"];

    public string SearchQuery
    {
        get => _searchQuery;
        set { _searchQuery = value; OnPropertyChanged(); ApplyBrowseFilter(); }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { _selectedCategory = value; OnPropertyChanged(); ApplyBrowseFilter(); }
    }

    public bool ShowVerifiedOnly
    {
        get => _showVerifiedOnly;
        set { _showVerifiedOnly = value; OnPropertyChanged(); ApplyBrowseFilter(); }
    }

    public MarketplaceListing? SelectedBrowseListing
    {
        get => _selectedBrowseListing;
        set
        {
            _selectedBrowseListing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanInstallSelected));
        }
    }

    public bool CanInstallSelected =>
        SelectedBrowseListing is not null
        && !IsInstalling
        && !string.IsNullOrEmpty(SelectedBrowseListing.DownloadUrl)
        && !_marketplace.IsInstalled(SelectedBrowseListing.ListingId);

    // ── Installed tab state ───────────────────────────────────────────────────

    private MarketplaceListing? _selectedInstalledListing;

    public ObservableCollection<MarketplaceListing> InstalledListings { get; } = [];

    public MarketplaceListing? SelectedInstalledListing
    {
        get => _selectedInstalledListing;
        set { _selectedInstalledListing = value; OnPropertyChanged(); }
    }

    // ── Updates tab state ─────────────────────────────────────────────────────

    public ObservableCollection<MarketplaceListing> UpdateListings { get; } = [];

    // ── Install progress state ────────────────────────────────────────────────

    private bool   _isInstalling;
    private int    _installPercent;
    private string _statusText = "Ready.";

    public bool IsInstalling
    {
        get => _isInstalling;
        private set { _isInstalling = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanInstallSelected)); }
    }

    public int InstallPercent
    {
        get => _installPercent;
        private set { _installPercent = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    // ── Active tab ────────────────────────────────────────────────────────────

    private int _activeTab;
    public int ActiveTab
    {
        get => _activeTab;
        set { _activeTab = value; OnPropertyChanged(); OnTabActivated(value); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand RefreshCommand        { get; }
    public ICommand InstallSelectedCommand { get; }
    public ICommand InstallFromFileCommand { get; }
    public ICommand UninstallCommand       { get; }
    public ICommand UpdateSelectedCommand  { get; }
    public ICommand UpdateAllCommand       { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public MarketplacePanelViewModel(
        IMarketplaceService marketplace,
        WpfPluginHost host,
        Action<string>? logger = null)
    {
        _marketplace = marketplace ?? throw new ArgumentNullException(nameof(marketplace));
        _host        = host        ?? throw new ArgumentNullException(nameof(host));
        _log         = logger ?? (_ => { });

        _marketplace.InstallProgressChanged += OnInstallProgress;

        RefreshCommand         = new RelayCommand(_ => _ = RefreshCurrentTabAsync(), _ => !IsInstalling);
        InstallSelectedCommand = new RelayCommand(_ => _ = InstallSelectedAsync(),   _ => CanInstallSelected);
        InstallFromFileCommand = new RelayCommand(_ => _ = InstallFromFileDialogAsync());
        UninstallCommand       = new RelayCommand(p  => _ = UninstallAsync(p as MarketplaceListing),
                                                  p  => p is MarketplaceListing && !IsInstalling);
        UpdateSelectedCommand  = new RelayCommand(p  => _ = UpdateAsync(p as MarketplaceListing),
                                                  p  => p is MarketplaceListing && !IsInstalling);
        UpdateAllCommand       = new RelayCommand(_ => _ = UpdateAllAsync(), _ => UpdateListings.Count > 0 && !IsInstalling);

        _ = LoadBrowseTabAsync();
    }

    // ── Tab switching ─────────────────────────────────────────────────────────

    private void OnTabActivated(int tab)
    {
        switch (tab)
        {
            case 0: _ = LoadBrowseTabAsync();    break;
            case 1: _ = LoadInstalledTabAsync(); break;
            case 2: _ = LoadUpdatesTabAsync();   break;
        }
    }

    // ── Browse tab ────────────────────────────────────────────────────────────

    private async Task LoadBrowseTabAsync()
    {
        StatusText = "Loading marketplace…";
        try
        {
            var listings = await _marketplace.SearchAsync(string.Empty).ConfigureAwait(false);
            await UIInvokeAsync(() =>
            {
                AllListings.Clear();
                foreach (var l in listings) AllListings.Add(l);
                RebuildCategories();
                ApplyBrowseFilter();
                StatusText = $"{AllListings.Count} plugin(s) available.";
            });
        }
        catch (Exception ex)
        {
            await UIInvokeAsync(() => StatusText = $"Feed error: {ex.Message}");
            _log($"[Marketplace] Browse load failed: {ex}");
        }
    }

    private async Task RefreshCurrentTabAsync()
    {
        switch (_activeTab)
        {
            case 0: await LoadBrowseTabAsync();    break;
            case 1: await LoadInstalledTabAsync(); break;
            case 2: await LoadUpdatesTabAsync();   break;
        }
    }

    private void ApplyBrowseFilter()
    {
        var query = _searchQuery.Trim().ToUpperInvariant();
        FilteredListings.Clear();
        foreach (var l in AllListings)
        {
            if (_showVerifiedOnly && !l.Verified) continue;
            if (_selectedCategory != "All" && l.Category != _selectedCategory) continue;
            if (!string.IsNullOrEmpty(query))
            {
                var match = l.Name.ToUpperInvariant().Contains(query)
                    || l.Description.ToUpperInvariant().Contains(query)
                    || l.Publisher.ToUpperInvariant().Contains(query)
                    || l.Tags.Any(t => t.ToUpperInvariant().Contains(query));
                if (!match) continue;
            }
            FilteredListings.Add(l);
        }
        StatusText = $"{FilteredListings.Count} of {AllListings.Count} plugin(s) shown.";
    }

    private void RebuildCategories()
    {
        var cats = AllListings.Select(l => l.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct().OrderBy(c => c).ToList();
        Categories.Clear();
        Categories.Add("All");
        foreach (var c in cats) Categories.Add(c);
    }

    // ── Installed tab ─────────────────────────────────────────────────────────

    private async Task LoadInstalledTabAsync()
    {
        try
        {
            var installed = await _marketplace.GetInstalledAsync().ConfigureAwait(false);
            await UIInvokeAsync(() =>
            {
                InstalledListings.Clear();
                foreach (var l in installed) InstalledListings.Add(l);
                StatusText = $"{InstalledListings.Count} plugin(s) installed.";
            });
        }
        catch (Exception ex)
        {
            await UIInvokeAsync(() => StatusText = $"Error: {ex.Message}");
        }
    }

    private async Task UninstallAsync(MarketplaceListing? listing)
    {
        if (listing is null) return;
        IsInstalling = true;
        StatusText = $"Uninstalling '{listing.Name}'…";
        try
        {
            var ok = await _marketplace.UninstallAsync(listing.ListingId).ConfigureAwait(false);
            await UIInvokeAsync(() =>
            {
                if (ok) InstalledListings.Remove(listing);
                StatusText = ok ? $"'{listing.Name}' uninstalled." : $"Uninstall failed.";
            });
        }
        finally { IsInstalling = false; }
    }

    // ── Updates tab ───────────────────────────────────────────────────────────

    private async Task LoadUpdatesTabAsync()
    {
        try
        {
            var updates = await _marketplace.GetUpdatesAsync().ConfigureAwait(false);
            await UIInvokeAsync(() =>
            {
                UpdateListings.Clear();
                foreach (var l in updates) UpdateListings.Add(l);
                StatusText = UpdateListings.Count > 0
                    ? $"{UpdateListings.Count} update(s) available."
                    : "All plugins are up to date.";
            });
        }
        catch (Exception ex)
        {
            await UIInvokeAsync(() => StatusText = $"Error: {ex.Message}");
        }
    }

    private async Task UpdateAsync(MarketplaceListing? listing)
    {
        if (listing is null) return;
        await RunInstallAsync(listing.ListingId);
        await LoadUpdatesTabAsync();
    }

    private async Task UpdateAllAsync()
    {
        var toUpdate = UpdateListings.ToList();
        foreach (var l in toUpdate)
            await RunInstallAsync(l.ListingId);
        await LoadUpdatesTabAsync();
    }

    // ── Install pipeline ──────────────────────────────────────────────────────

    private async Task InstallSelectedAsync()
    {
        if (SelectedBrowseListing is null) return;
        await RunInstallAsync(SelectedBrowseListing.ListingId);
    }

    private async Task InstallFromFileDialogAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title     = "Install Plugin",
            Filter    = "Plugin Packages (*.whxplugin)|*.whxplugin",
            Multiselect = false,
        };
        if (dlg.ShowDialog() != true) return;

        IsInstalling = true;
        StatusText = $"Installing {System.IO.Path.GetFileNameWithoutExtension(dlg.FileName)}…";
        try
        {
            var entry = await _host.InstallFromFileAsync(dlg.FileName).ConfigureAwait(false);
            await UIInvokeAsync(() => StatusText = $"'{entry.Manifest.Name}' installed.");
        }
        catch (Exception ex)
        {
            await UIInvokeAsync(() => StatusText = $"Install failed: {ex.Message}");
            _log($"[Marketplace] File install failed: {ex}");
        }
        finally { IsInstalling = false; InstallPercent = 0; }
    }

    /// <summary>Called from drag-and-drop in code-behind.</summary>
    public void InstallFromDroppedFiles(string[] files)
    {
        foreach (var f in files.Where(f => f.EndsWith(".whxplugin", StringComparison.OrdinalIgnoreCase)))
        {
            IsInstalling = true;
            _ = _host.InstallFromFileAsync(f).ContinueWith(t =>
            {
                _ = UIInvokeAsync(() =>
                {
                    IsInstalling = false;
                    StatusText = t.IsCompletedSuccessfully
                        ? $"'{t.Result.Manifest.Name}' installed."
                        : $"Drop install failed: {t.Exception?.GetBaseException().Message}";
                });
            });
        }
    }

    private async Task RunInstallAsync(string listingId)
    {
        IsInstalling  = true;
        InstallPercent = 0;

        var progress = new Progress<InstallProgress>(p =>
        {
            _ = UIInvokeAsync(() =>
            {
                InstallPercent = p.PercentComplete;
                StatusText     = p.StatusMessage;
            });
        });

        try
        {
            var result = await _marketplace.InstallAsync(listingId, progress).ConfigureAwait(false);
            await UIInvokeAsync(() => StatusText = result.Success
                ? $"Installed successfully → {result.InstalledPath}"
                : $"Install failed: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            await UIInvokeAsync(() => StatusText = $"Install error: {ex.Message}");
            _log($"[Marketplace] InstallAsync failed for '{listingId}': {ex}");
        }
        finally { IsInstalling = false; InstallPercent = 0; }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnInstallProgress(object? sender, InstallProgressEventArgs e)
    {
        _ = UIInvokeAsync(() =>
        {
            InstallPercent = e.Progress.PercentComplete;
            StatusText     = e.Progress.StatusMessage;
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Task UIInvokeAsync(Action action)
    {
        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            return d.InvokeAsync(action).Task;
        action();
        return Task.CompletedTask;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
