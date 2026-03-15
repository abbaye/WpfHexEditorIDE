// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: UI/MarketplacePanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     ViewModel for the Marketplace dockable panel.
//     Orchestrates feed search, listing display, download progress and
//     plugin installation via WpfPluginHost.
//
// Architecture Notes:
//     - Pattern: MVVM + RelayCommand (from SDK)
//     - MarketplaceServiceImpl handles feed + download
//     - WpfPluginHost.InstallFromFileAsync handles extract + load
//     - All async operations run on thread-pool; UI updates via Dispatcher
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.SDK;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// ViewModel for the Plugin Marketplace panel.
/// </summary>
public sealed class MarketplacePanelViewModel : INotifyPropertyChanged
{
    private readonly WpfPluginHost _host;
    private readonly MarketplaceServiceImpl _marketplaceService;
    private readonly Action<string> _log;

    // ── Observable state ──────────────────────────────────────────────────────
    private string _searchQuery = string.Empty;
    private string _selectedCategory = "All";
    private MarketplaceListing? _selectedListing;
    private bool _showVerifiedOnly;
    private bool _isDownloading;
    private double _downloadPercent;
    private string _statusText = "Ready.";

    public ObservableCollection<MarketplaceListing> AllListings { get; } = [];
    public ObservableCollection<MarketplaceListing> FilteredListings { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["All"];

    // ── Properties ────────────────────────────────────────────────────────────

    public string SearchQuery
    {
        get => _searchQuery;
        set { _searchQuery = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { _selectedCategory = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public MarketplaceListing? SelectedListing
    {
        get => _selectedListing;
        set { _selectedListing = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanInstallSelected)); }
    }

    public bool ShowVerifiedOnly
    {
        get => _showVerifiedOnly;
        set { _showVerifiedOnly = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set { _isDownloading = value; OnPropertyChanged(); }
    }

    public double DownloadPercent
    {
        get => _downloadPercent;
        private set { _downloadPercent = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public bool CanInstallSelected =>
        SelectedListing is not null && !IsDownloading
        && !string.IsNullOrEmpty(SelectedListing.DownloadUrl);

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand RefreshFeedCommand { get; }
    public ICommand InstallSelectedCommand { get; }
    public ICommand InstallFromFileCommand { get; }

    // ─────────────────────────────────────────────────────────────────────────
    public MarketplacePanelViewModel(WpfPluginHost host, MarketplaceServiceImpl marketplace,
        Action<string>? logger = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _marketplaceService = marketplace ?? throw new ArgumentNullException(nameof(marketplace));
        _log = logger ?? (_ => { });

        _marketplaceService.DownloadProgressChanged += OnDownloadProgress;

        RefreshFeedCommand = new RelayCommand(
            _ => _ = RefreshFeedAsync(),
            _ => !IsDownloading);

        InstallSelectedCommand = new RelayCommand(
            _ => _ = InstallSelectedAsync(),
            _ => CanInstallSelected);

        InstallFromFileCommand = new RelayCommand(
            _ => _ = InstallFromFileDialogAsync());

        // Load feed on first open
        _ = RefreshFeedAsync();
    }

    // ── Feed operations ───────────────────────────────────────────────────────

    private async Task RefreshFeedAsync()
    {
        StatusText = "Refreshing marketplace feed...";
        try
        {
            var listings = await _marketplaceService.SearchAsync(string.Empty).ConfigureAwait(false);
            await UIInvokeAsync(() =>
            {
                AllListings.Clear();
                foreach (var l in listings) AllListings.Add(l);

                RebuildCategories();
                ApplyFilter();
                StatusText = $"{AllListings.Count} plugin(s) available.";
            });
        }
        catch (Exception ex)
        {
            await UIInvokeAsync(() => StatusText = $"Feed error: {ex.Message}");
            _log($"[Marketplace] Feed refresh failed: {ex}");
        }
    }

    private async Task InstallSelectedAsync()
    {
        if (SelectedListing is null) return;
        await DownloadAndInstallAsync(SelectedListing.ListingId).ConfigureAwait(false);
    }

    private async Task InstallFromFileDialogAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Install Plugin",
            Filter = "Plugin Packages (*.whxplugin)|*.whxplugin",
            Multiselect = false,
        };

        if (dlg.ShowDialog() != true) return;
        await InstallLocalFileAsync(dlg.FileName).ConfigureAwait(false);
    }

    /// <summary>Called from drag-and-drop in the code-behind.</summary>
    public void InstallFromDroppedFiles(string[] files)
    {
        foreach (var f in files.Where(f => f.EndsWith(".whxplugin", StringComparison.OrdinalIgnoreCase)))
            _ = InstallLocalFileAsync(f);
    }

    private async Task DownloadAndInstallAsync(string listingId)
    {
        IsDownloading = true;
        DownloadPercent = 0;
        StatusText = "Downloading...";

        try
        {
            var packagePath = await _marketplaceService.DownloadAsync(listingId).ConfigureAwait(false);
            await InstallLocalFileAsync(packagePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await UIInvokeAsync(() => StatusText = $"Download failed: {ex.Message}");
            _log($"[Marketplace] Download failed: {ex}");
        }
        finally
        {
            IsDownloading = false;
            DownloadPercent = 0;
        }
    }

    private async Task InstallLocalFileAsync(string packagePath)
    {
        StatusText = $"Installing {System.IO.Path.GetFileNameWithoutExtension(packagePath)}...";
        try
        {
            var entry = await _host.InstallFromFileAsync(packagePath).ConfigureAwait(false);
            await UIInvokeAsync(() => StatusText = $"'{entry.Manifest.Name}' installed successfully.");
        }
        catch (Exception ex)
        {
            await UIInvokeAsync(() => StatusText = $"Install failed: {ex.Message}");
            _log($"[Marketplace] Install failed: {ex}");
        }
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    private void ApplyFilter()
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
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        Categories.Clear();
        Categories.Add("All");
        foreach (var c in cats) Categories.Add(c);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnDownloadProgress(object? sender, DownloadProgressArgs e)
    {
        _ = UIInvokeAsync(() =>
        {
            DownloadPercent = e.Percent;
            StatusText = $"Downloading '{e.Name}'… {e.Percent:F0}%";
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
