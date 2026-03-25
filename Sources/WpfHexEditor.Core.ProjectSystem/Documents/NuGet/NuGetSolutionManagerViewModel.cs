// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/NuGet/NuGetSolutionManagerViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     ViewModel for the solution-level NuGet Package Manager document.
//     Aggregates NuGet packages across all VS projects (.csproj/.vbproj)
//     of the loaded solution, providing Browse / Installed / Consolidate /
//     Updates tabs with per-project install/uninstall operations.
//
// Architecture Notes:
//     Pattern: MVVM with INotifyPropertyChanged
//     Reads: CsprojPackageWriter.GetPackageReferences per VS project
//     Writes: CsprojPackageWriter.AddOrUpdatePackageReference /
//             RemovePackageReference per selected project
//     Search: NuGetV3Client (same as per-project manager)
//     Event: SolutionModified raised after any write-back so the host
//            can reload the solution and refresh the Solution Explorer.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Services.NuGet;

namespace WpfHexEditor.Core.ProjectSystem.Documents.NuGet;

/// <summary>
/// ViewModel for <see cref="NuGetSolutionManagerDocument"/>.
/// Manages packages across all VS projects in the solution.
/// </summary>
public sealed class NuGetSolutionManagerViewModel : INotifyPropertyChanged
{
    private const int PageSize = 25;

    private readonly ISolution    _solution;
    private readonly INuGetClient _client;

    // All csproj/vbproj/fsproj projects in the solution.
    private readonly IReadOnlyList<IProject> _vsProjects;

    private string  _searchText        = "";
    private bool    _includePrerelease = false;
    private int     _activeTabIndex    = 0;  // 0=Browse, 1=Installed, 2=Consolidate, 3=Updates
    private SolutionPackageViewModel? _selectedPackage;
    private string? _selectedVersion;
    private bool    _isBusy;
    private string  _statusText        = "";
    private bool    _hasMoreResults;
    private int     _currentSkip       = 0;
    private int     _updateCount;

    private CancellationTokenSource _searchCts = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public NuGetSolutionManagerViewModel(ISolution solution, INuGetClient client)
    {
        _solution   = solution;
        _client     = client;
        _vsProjects = solution.Projects
            .Where(p => IsMsBuildProject(p.ProjectFilePath))
            .ToList();

        PackageList       = [];
        AvailableVersions = [];

        SearchCommand    = new AsyncRelayCommand(RunSearchAsync);
        LoadMoreCommand  = new AsyncRelayCommand(RunLoadMoreAsync, () => HasMoreResults && !IsBusy);
        InstallCommand   = new AsyncRelayCommand(RunInstallAsync,
            () => _selectedPackage is not null && SelectedVersion is not null && !IsBusy &&
                  _selectedPackage.Projects.Any(p => p.IsSelected && !p.HasPackage));
        UninstallCommand = new AsyncRelayCommand(RunUninstallAsync,
            () => _selectedPackage is not null && !IsBusy &&
                  _selectedPackage.Projects.Any(p => p.IsSelected && p.HasPackage));
        UpdateCommand    = new AsyncRelayCommand(RunUpdateAsync,
            () => _selectedPackage?.HasUpdate == true && SelectedVersion is not null && !IsBusy &&
                  _selectedPackage.Projects.Any(p => p.IsSelected && p.HasPackage));
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised after any .csproj write-back so the host can refresh the Solution Explorer.
    /// </summary>
    public event EventHandler? SolutionModified;

    // ── Properties ────────────────────────────────────────────────────────────

    public string SolutionName => _solution.Name;

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

    public SolutionPackageViewModel? SelectedPackage
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
    /// Reset to 0 at the start of each update check, set to the final count on completion.
    /// </summary>
    public int UpdateCount
    {
        get => _updateCount;
        private set { _updateCount = value; OnPropertyChanged(); }
    }

    public bool HasSelectedPackage => _selectedPackage is not null;

    public ObservableCollection<SolutionPackageViewModel> PackageList       { get; }
    public ObservableCollection<string>                   AvailableVersions { get; }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand SearchCommand    { get; }
    public ICommand LoadMoreCommand  { get; }
    public ICommand InstallCommand   { get; }
    public ICommand UninstallCommand { get; }
    public ICommand UpdateCommand    { get; }

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the view code-behind on Loaded.
    /// Loads popular packages from nuget.org (Browse tab)
    /// and checks for available updates in the background.
    /// </summary>
    public async Task LoadAsync()
    {
        await ExecuteSearchAsync(query: "", skip: 0, clearList: true);

        // Fire-and-forget: count available updates so the badge shows immediately.
        _ = CountUpdatesInBackgroundAsync();
    }

    // ── Search (Browse tab) ───────────────────────────────────────────────────

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

        // Pre-load installed packages to mark them in the Browse list.
        var allInstalled = GetAllInstalledPackages();

        try
        {
            var results = await _client.SearchAsync(query, _includePrerelease, skip, PageSize, ct);
            foreach (var r in results)
            {
                // Build per-project selection rows for the Browse result.
                var projectRows = BuildProjectRows(r.Id, allInstalled);
                var displayVer  = GetDisplayVersion(projectRows);
                var vm = new SolutionPackageViewModel
                {
                    Id             = r.Id,
                    DisplayVersion = displayVer,
                    LatestVersion  = r.Version,
                    Description    = r.Description ?? "",
                    Authors        = r.Authors is { Count: > 0 } a ? string.Join(", ", a) : "",
                    TotalDownloads = r.TotalDownloads,
                    Projects       = new(projectRows),
                    HasConflict    = DetectConflict(projectRows),
                };
                PackageList.Add(vm);
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
            case 1: LoadInstalledAsync().ConfigureAwait(false); break;
            case 2: LoadConsolidateAsync().ConfigureAwait(false); break;
            case 3: LoadUpdatesAsync().ConfigureAwait(false); break;
        }
    }

    // ── Installed tab ─────────────────────────────────────────────────────────

    private async Task LoadInstalledAsync()
    {
        IsBusy = true;
        StatusText = "Loading installed packages…";
        PackageList.Clear();
        HasMoreResults = false;

        var allInstalled = GetAllInstalledPackages();

        // Build one SolutionPackageViewModel per unique package id.
        var unique = new Dictionary<string, SolutionPackageViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var (proj, refs) in allInstalled)
        {
            foreach (var pkg in refs)
            {
                if (!unique.TryGetValue(pkg.Id, out var existing))
                {
                    // Build project rows for this package across all VS projects.
                    var rows = BuildProjectRows(pkg.Id, allInstalled);
                    existing = new SolutionPackageViewModel
                    {
                        Id             = pkg.Id,
                        DisplayVersion = GetDisplayVersion(rows),
                        Projects       = new(rows),
                        HasConflict    = DetectConflict(rows),
                    };
                    unique[pkg.Id] = existing;
                    PackageList.Add(existing);
                }
            }
        }

        int projectCount = _vsProjects.Count;
        StatusText = PackageList.Count == 0
            ? $"No NuGet packages found across {projectCount} project(s)."
            : $"{PackageList.Count} unique package(s) across {projectCount} project(s).";

        IsBusy = false;
        await Task.CompletedTask;
    }

    // ── Consolidate tab ───────────────────────────────────────────────────────

    private async Task LoadConsolidateAsync()
    {
        IsBusy = true;
        StatusText = "Checking version conflicts…";
        PackageList.Clear();
        HasMoreResults = false;

        var allInstalled = GetAllInstalledPackages();

        var unique = new Dictionary<string, SolutionPackageViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, refs) in allInstalled)
        {
            foreach (var pkg in refs)
            {
                if (!unique.ContainsKey(pkg.Id))
                {
                    var rows = BuildProjectRows(pkg.Id, allInstalled);
                    bool conflict = DetectConflict(rows);
                    var vm = new SolutionPackageViewModel
                    {
                        Id             = pkg.Id,
                        DisplayVersion = GetDisplayVersion(rows),
                        Projects       = new(rows),
                        HasConflict    = conflict,
                    };
                    unique[pkg.Id] = vm;
                    if (conflict) PackageList.Add(vm);
                }
            }
        }

        StatusText = PackageList.Count == 0
            ? "All packages are consolidated (no version conflicts)."
            : $"{PackageList.Count} package(s) with version conflicts.";

        IsBusy = false;
        await Task.CompletedTask;
    }

    // ── Updates tab ───────────────────────────────────────────────────────────

    private async Task LoadUpdatesAsync()
    {
        IsBusy = true;
        StatusText = "Checking for updates…";
        PackageList.Clear();
        HasMoreResults = false;
        UpdateCount = 0;

        var allInstalled = GetAllInstalledPackages();
        var unique = new Dictionary<string, List<ProjectSelectionViewModel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, refs) in allInstalled)
        {
            foreach (var pkg in refs)
            {
                if (!unique.ContainsKey(pkg.Id))
                    unique[pkg.Id] = BuildProjectRows(pkg.Id, allInstalled);
            }
        }

        foreach (var (id, rows) in unique)
        {
            var versions = await _client.GetVersionsAsync(id, _includePrerelease);
            var latest   = versions.FirstOrDefault();
            if (latest is null) continue;

            bool anyOutdated = rows.Any(r => r.HasPackage &&
                !string.Equals(r.InstalledVersion, latest, StringComparison.OrdinalIgnoreCase));
            if (!anyOutdated) continue;

            var vm = new SolutionPackageViewModel
            {
                Id             = id,
                DisplayVersion = GetDisplayVersion(rows),
                LatestVersion  = latest,
                Projects       = new(rows),
                HasConflict    = DetectConflict(rows),
            };
            PackageList.Add(vm);
        }

        UpdateCount = PackageList.Count;
        StatusText  = PackageList.Count == 0
            ? "All packages are up to date."
            : $"{PackageList.Count} update(s) available.";

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
            var allInstalled = GetAllInstalledPackages();
            var unique = new Dictionary<string, List<ProjectSelectionViewModel>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, refs) in allInstalled)
            {
                foreach (var pkg in refs)
                {
                    if (!unique.ContainsKey(pkg.Id))
                        unique[pkg.Id] = BuildProjectRows(pkg.Id, allInstalled);
                }
            }

            int count = 0;
            foreach (var (id, rows) in unique)
            {
                var versions = await _client.GetVersionsAsync(id, _includePrerelease);
                var latest = versions.FirstOrDefault();
                if (latest is null) continue;

                bool anyOutdated = rows.Any(r => r.HasPackage &&
                    !string.Equals(r.InstalledVersion, latest, StringComparison.OrdinalIgnoreCase));
                if (anyOutdated) count++;
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

        // Pre-select: common installed version first, then latest.
        SelectedVersion = _selectedPackage.DisplayVersion != "Multiple"
            ? _selectedPackage.DisplayVersion
            : AvailableVersions.FirstOrDefault();
    }

    // ── Install / Uninstall / Update ──────────────────────────────────────────

    private async Task RunInstallAsync()
    {
        if (_selectedPackage is null || SelectedVersion is null) return;

        IsBusy     = true;
        StatusText = $"Installing {_selectedPackage.Id} {SelectedVersion}…";
        try
        {
            foreach (var row in _selectedPackage.Projects.Where(p => p.IsSelected && !p.HasPackage))
            {
                CsprojPackageWriter.AddOrUpdatePackageReference(
                    row.Project.ProjectFilePath, _selectedPackage.Id, SelectedVersion);
            }

            StatusText = $"Installed {_selectedPackage.Id} {SelectedVersion}.";
            SolutionModified?.Invoke(this, EventArgs.Empty);
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
            foreach (var row in _selectedPackage.Projects.Where(p => p.IsSelected && p.HasPackage))
            {
                CsprojPackageWriter.RemovePackageReference(
                    row.Project.ProjectFilePath, _selectedPackage.Id);
            }

            StatusText = $"Removed {_selectedPackage.Id} from selected projects.";
            SolutionModified?.Invoke(this, EventArgs.Empty);
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
            foreach (var row in _selectedPackage.Projects.Where(p => p.IsSelected && p.HasPackage))
            {
                CsprojPackageWriter.AddOrUpdatePackageReference(
                    row.Project.ProjectFilePath, _selectedPackage.Id, SelectedVersion);
            }

            StatusText = $"Updated {_selectedPackage.Id} to {SelectedVersion}.";
            SolutionModified?.Invoke(this, EventArgs.Empty);
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

    private Task RefreshCurrentTabAsync() => _activeTabIndex switch
    {
        1 => LoadInstalledAsync(),
        2 => LoadConsolidateAsync(),
        3 => LoadUpdatesAsync(),
        _ => RunSearchAsync()
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns per-project installed package references for all VS projects.
    /// Keyed by IProject so callers can match project identity.
    /// </summary>
    private List<(IProject Project, IReadOnlyList<PackageReferenceInfo> Refs)> GetAllInstalledPackages()
    {
        var result = new List<(IProject, IReadOnlyList<PackageReferenceInfo>)>(_vsProjects.Count);
        foreach (var proj in _vsProjects)
            result.Add((proj, CsprojPackageWriter.GetPackageReferences(proj.ProjectFilePath)));
        return result;
    }

    /// <summary>
    /// Builds one <see cref="ProjectSelectionViewModel"/> per VS project for a given package id.
    /// </summary>
    private List<ProjectSelectionViewModel> BuildProjectRows(
        string packageId,
        IEnumerable<(IProject Project, IReadOnlyList<PackageReferenceInfo> Refs)> allInstalled)
    {
        var rows = new List<ProjectSelectionViewModel>(_vsProjects.Count);
        foreach (var (proj, refs) in allInstalled)
        {
            var installed = refs.FirstOrDefault(r =>
                string.Equals(r.Id, packageId, StringComparison.OrdinalIgnoreCase));
            rows.Add(new ProjectSelectionViewModel
            {
                Project          = proj,
                InstalledVersion = installed?.Version,
                IsSelected       = installed is not null,
            });
        }
        return rows;
    }

    /// <summary>
    /// Returns the single common version across projects that have the package,
    /// or "Multiple" if different versions are present.
    /// </summary>
    private static string GetDisplayVersion(IEnumerable<ProjectSelectionViewModel> rows)
    {
        var versions = rows
            .Where(r => r.HasPackage)
            .Select(r => r.InstalledVersion ?? "")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return versions.Count switch
        {
            0 => "",
            1 => versions[0],
            _ => "Multiple"
        };
    }

    /// <summary>
    /// Returns <see langword="true"/> when ≥ 2 projects reference the package with different versions.
    /// </summary>
    private static bool DetectConflict(IEnumerable<ProjectSelectionViewModel> rows)
    {
        var versions = rows
            .Where(r => r.HasPackage)
            .Select(r => r.InstalledVersion ?? "")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return versions.Count >= 2;
    }

    private static bool IsMsBuildProject(string path)
    {
        var ext = System.IO.Path.GetExtension(path);
        return ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".vbproj", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".fsproj", StringComparison.OrdinalIgnoreCase);
    }

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
