// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/NuGet/NuGetManagerViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Main ViewModel for the VS-Like NuGet Package Manager document.
//     Manages three tabs (Browse / Installed / Updates), debounced live
//     search, version loading, install / uninstall / update commands,
//     and .csproj write-back through CsprojPackageWriter.
//
// Architecture Notes:
//     Pattern: MVVM with INotifyPropertyChanged
//     Debounce: 300ms CancellationTokenSource pattern on SearchText setter
//     Write-back: CsprojPackageWriter (pure XDocument manipulation)
//     Pagination: LoadMoreCommand appends results using _currentSkip offset
//     Event: ProjectModified raised after every write-back so MainWindow
//            can reload the VsProject and refresh the Solution Explorer.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Services.NuGet;

namespace WpfHexEditor.ProjectSystem.Documents.NuGet;

/// <summary>
/// ViewModel for <see cref="NuGetManagerDocument"/>.
/// </summary>
public sealed class NuGetManagerViewModel : INotifyPropertyChanged
{
    private const int PageSize = 25;

    private readonly IProject      _project;
    private readonly INuGetClient  _client;

    private string  _searchText        = "";
    private bool    _includePrerelease = false;
    private int     _activeTabIndex    = 0;   // 0=Browse, 1=Installed, 2=Updates
    private NuGetPackageViewModel? _selectedPackage;
    private string? _selectedVersion;
    private bool    _isBusy;
    private string  _statusText        = "";
    private bool    _hasMoreResults;
    private int     _currentSkip       = 0;
    private int     _updateCount;

    private CancellationTokenSource _searchCts = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public NuGetManagerViewModel(IProject project, INuGetClient client)
    {
        _project = project;
        _client  = client;

        PackageList      = [];
        AvailableVersions = [];

        SearchCommand    = new AsyncRelayCommand(RunSearchAsync);
        LoadMoreCommand  = new AsyncRelayCommand(RunLoadMoreAsync, () => HasMoreResults && !IsBusy);
        InstallCommand   = new AsyncRelayCommand(RunInstallAsync,  () => SelectedPackage is not null && !SelectedPackage.IsInstalled && SelectedVersion is not null && !IsBusy);
        UninstallCommand = new AsyncRelayCommand(RunUninstallAsync, () => SelectedPackage?.IsInstalled == true && !IsBusy);
        UpdateCommand    = new AsyncRelayCommand(RunUpdateAsync,   () => SelectedPackage?.HasUpdate == true && SelectedVersion is not null && !IsBusy);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised after any .csproj write-back so the host can reload the project.
    /// </summary>
    public event EventHandler? ProjectModified;

    // ── Properties ────────────────────────────────────────────────────────────

    public string ProjectName => _project.Name;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            TriggerDebouncedSearch();
        }
    }

    public bool IncludePrerelease
    {
        get => _includePrerelease;
        set
        {
            if (_includePrerelease == value) return;
            _includePrerelease = value;
            OnPropertyChanged();
            TriggerDebouncedSearch();
        }
    }

    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        set
        {
            if (_activeTabIndex == value) return;
            _activeTabIndex = value;
            OnPropertyChanged();
            HandleTabChanged(value);
        }
    }

    public NuGetPackageViewModel? SelectedPackage
    {
        get => _selectedPackage;
        set
        {
            if (_selectedPackage == value) return;
            _selectedPackage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedPackage));
            LoadVersionsAsync().ConfigureAwait(false);
        }
    }

    public string? SelectedVersion
    {
        get => _selectedVersion;
        set { _selectedVersion = value; OnPropertyChanged(); RaiseCommandsCanExecuteChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); RaiseCommandsCanExecuteChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public bool HasMoreResults
    {
        get => _hasMoreResults;
        private set { _hasMoreResults = value; OnPropertyChanged(); ((AsyncRelayCommand)LoadMoreCommand).RaiseCanExecuteChanged(); }
    }

    /// <summary>
    /// Number of packages with available updates. Drives the badge on the "Updates" tab header.
    /// </summary>
    public int UpdateCount
    {
        get => _updateCount;
        private set { _updateCount = value; OnPropertyChanged(); }
    }

    public bool HasSelectedPackage => _selectedPackage is not null;

    public ObservableCollection<NuGetPackageViewModel> PackageList      { get; }
    public ObservableCollection<string>               AvailableVersions { get; }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand SearchCommand    { get; }
    public ICommand LoadMoreCommand  { get; }
    public ICommand InstallCommand   { get; }
    public ICommand UninstallCommand { get; }
    public ICommand UpdateCommand    { get; }

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the view code-behind on Loaded.
    /// Loads the first page of popular packages (empty search)
    /// and checks for available updates in the background.
    /// </summary>
    public async Task LoadAsync()
    {
        await ExecuteSearchAsync(query: "", skip: 0, clearList: true);

        // Fire-and-forget: count available updates so the badge shows immediately.
        _ = CountUpdatesInBackgroundAsync();
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private void TriggerDebouncedSearch()
    {
        _searchCts.Cancel();
        _searchCts = new CancellationTokenSource();
        var cts = _searchCts;

        Task.Run(async () =>
        {
            await Task.Delay(300, cts.Token);
            if (cts.IsCancellationRequested) return;

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                _currentSkip = 0;
                await ExecuteSearchAsync(_searchText, skip: 0, clearList: true, ct: cts.Token);
            });
        }, cts.Token).ConfigureAwait(false);
    }

    private async Task RunSearchAsync()
    {
        _currentSkip = 0;
        await ExecuteSearchAsync(_searchText, skip: 0, clearList: true);
    }

    private async Task RunLoadMoreAsync()
    {
        _currentSkip += PageSize;
        await ExecuteSearchAsync(_searchText, skip: _currentSkip, clearList: false);
    }

    private async Task ExecuteSearchAsync(string query, int skip, bool clearList, CancellationToken ct = default)
    {
        IsBusy     = true;
        StatusText = "Searching…";
        if (clearList) PackageList.Clear();

        var installed = CsprojPackageWriter.GetPackageReferences(_project.ProjectFilePath);

        try
        {
            var results = await _client.SearchAsync(query, _includePrerelease, skip, PageSize, ct);
            foreach (var r in results)
            {
                var inst = installed.FirstOrDefault(i => string.Equals(i.Id, r.Id, StringComparison.OrdinalIgnoreCase));
                PackageList.Add(new NuGetPackageViewModel
                {
                    Id               = r.Id,
                    LatestVersion    = r.Version,
                    InstalledVersion = inst?.Version,
                    Authors          = r.Authors is { Count: > 0 } a ? string.Join(", ", a) : "",
                    Description      = r.Description ?? "",
                    TotalDownloads   = r.TotalDownloads,
                });
            }

            HasMoreResults = results.Count == PageSize;
            StatusText     = results.Count == 0 ? "No packages found." : $"{PackageList.Count} package(s) loaded.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Tab switching ─────────────────────────────────────────────────────────

    private void HandleTabChanged(int tabIndex)
    {
        switch (tabIndex)
        {
            case 0: TriggerDebouncedSearch(); break;
            case 1: LoadInstalledPackagesAsync().ConfigureAwait(false); break;
            case 2: LoadUpdatesAsync().ConfigureAwait(false); break;
        }
    }

    private async Task LoadInstalledPackagesAsync()
    {
        IsBusy = true;
        StatusText = "Loading installed packages…";
        PackageList.Clear();
        HasMoreResults = false;

        var installed = CsprojPackageWriter.GetPackageReferences(_project.ProjectFilePath);
        foreach (var pkg in installed)
        {
            PackageList.Add(new NuGetPackageViewModel
            {
                Id               = pkg.Id,
                LatestVersion    = pkg.Version ?? "",
                InstalledVersion = pkg.Version,
                Authors          = "",
                Description      = "",
            });
        }

        StatusText = $"{PackageList.Count} installed package(s).";
        IsBusy = false;
        await Task.CompletedTask;
    }

    private async Task LoadUpdatesAsync()
    {
        IsBusy = true;
        StatusText = "Checking for updates…";
        PackageList.Clear();
        HasMoreResults = false;

        var installed = CsprojPackageWriter.GetPackageReferences(_project.ProjectFilePath);
        foreach (var pkg in installed)
        {
            var versions = await _client.GetVersionsAsync(pkg.Id, _includePrerelease);
            var latest   = versions.FirstOrDefault();
            if (latest is null || string.Equals(latest, pkg.Version, StringComparison.OrdinalIgnoreCase))
                continue;

            PackageList.Add(new NuGetPackageViewModel
            {
                Id               = pkg.Id,
                LatestVersion    = latest,
                InstalledVersion = pkg.Version,
                Authors          = "",
                Description      = "Update available",
            });
        }

        StatusText = PackageList.Count == 0
            ? "All packages are up to date."
            : $"{PackageList.Count} update(s) available.";
        UpdateCount = PackageList.Count;
        IsBusy = false;
    }

    /// <summary>
    /// Counts available updates in the background without affecting the current PackageList.
    /// Sets <see cref="UpdateCount"/> so the badge on the "Updates" tab header appears immediately.
    /// </summary>
    private async Task CountUpdatesInBackgroundAsync()
    {
        try
        {
            var installed = CsprojPackageWriter.GetPackageReferences(_project.ProjectFilePath);
            int count = 0;
            foreach (var pkg in installed)
            {
                var versions = await _client.GetVersionsAsync(pkg.Id, _includePrerelease);
                var latest = versions.FirstOrDefault();
                if (latest is null) continue;

                if (!string.Equals(latest, pkg.Version, StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            UpdateCount = count;
        }
        catch
        {
            // Silently ignore — the badge will remain at 0 until the user clicks "Updates".
        }
    }

    // ── Version loading ───────────────────────────────────────────────────────

    private async Task LoadVersionsAsync()
    {
        AvailableVersions.Clear();
        SelectedVersion = null;

        if (_selectedPackage is null) return;

        var versions = await _client.GetVersionsAsync(_selectedPackage.Id, _includePrerelease);
        foreach (var v in versions)
            AvailableVersions.Add(v);

        // Pre-select: installed version first, then latest.
        SelectedVersion = _selectedPackage.InstalledVersion ?? AvailableVersions.FirstOrDefault();
    }

    // ── Install / Uninstall / Update ──────────────────────────────────────────

    private async Task RunInstallAsync()
    {
        if (_selectedPackage is null || SelectedVersion is null) return;

        IsBusy     = true;
        StatusText = $"Installing {_selectedPackage.Id} {SelectedVersion}…";
        try
        {
            CsprojPackageWriter.AddOrUpdatePackageReference(
                _project.ProjectFilePath, _selectedPackage.Id, SelectedVersion);

            StatusText = $"Installed {_selectedPackage.Id} {SelectedVersion}.";
            ProjectModified?.Invoke(this, EventArgs.Empty);
            await RefreshCurrentTabAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Install failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunUninstallAsync()
    {
        if (_selectedPackage is null) return;

        IsBusy     = true;
        StatusText = $"Removing {_selectedPackage.Id}…";
        try
        {
            CsprojPackageWriter.RemovePackageReference(
                _project.ProjectFilePath, _selectedPackage.Id);

            StatusText = $"Removed {_selectedPackage.Id}.";
            ProjectModified?.Invoke(this, EventArgs.Empty);
            await RefreshCurrentTabAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Uninstall failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunUpdateAsync()
    {
        if (_selectedPackage is null || SelectedVersion is null) return;

        IsBusy     = true;
        StatusText = $"Updating {_selectedPackage.Id} to {SelectedVersion}…";
        try
        {
            CsprojPackageWriter.AddOrUpdatePackageReference(
                _project.ProjectFilePath, _selectedPackage.Id, SelectedVersion);

            StatusText = $"Updated {_selectedPackage.Id} to {SelectedVersion}.";
            ProjectModified?.Invoke(this, EventArgs.Empty);
            await RefreshCurrentTabAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Update failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task RefreshCurrentTabAsync()
    {
        return _activeTabIndex switch
        {
            1 => LoadInstalledPackagesAsync(),
            2 => LoadUpdatesAsync(),
            _ => RunSearchAsync()
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RaiseCommandsCanExecuteChanged()
    {
        ((AsyncRelayCommand)InstallCommand)  .RaiseCanExecuteChanged();
        ((AsyncRelayCommand)UninstallCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)UpdateCommand)   .RaiseCanExecuteChanged();
        ((AsyncRelayCommand)LoadMoreCommand) .RaiseCanExecuteChanged();
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
