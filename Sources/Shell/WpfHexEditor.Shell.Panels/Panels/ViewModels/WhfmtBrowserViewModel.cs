// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/ViewModels/WhfmtBrowserViewModel.cs
// Description: Root ViewModel for the Format Browser tool window.
//              Manages the category tree, flat list, search/filter,
//              detail card, and adhoc format operations.
// Architecture: No WPF controls here. Dispatcher is injected via Action<Action>
//              so the class can be tested without a running UI thread.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Options;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions.Query;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Shell.Panels.Services;

namespace WpfHexEditor.Shell.Panels.ViewModels;

/// <summary>
/// Root ViewModel for the Format Browser tool panel.
/// </summary>
public sealed class WhfmtBrowserViewModel : ViewModelBase, IDisposable
{
    // ------------------------------------------------------------------
    // Dependencies (set via Initialize)
    // ------------------------------------------------------------------

    private IEmbeddedFormatCatalog?  _embCatalog;
    private IFormatCatalogService?   _catalogSvc;
    private WhfmtAdHocFormatService? _adHocSvc;
    private WhfmtExplorerSettings?   _settings;
    private Action<Action>?          _dispatchToUi;

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------

    private string      _searchText        = string.Empty;
    private bool        _isTreeView        = true;
    private bool        _showBuiltIns      = true;
    private bool        _showUserFmts      = true;
    private bool        _isWatching;
    private string      _statusText        = "Loading…";
    private WhfmtSortMode    _currentWhfmtSortMode   = WhfmtSortMode.ByName;
    private WhfmtSearchField _currentWhfmtSearchField= WhfmtSearchField.All;
    private bool        _isRegexSearch;
    private bool        _isBusy;
    private int         _searchMatchCount;
    private int         _builtInCount;
    private int         _userFormatCount;
    private int         _failureCount;

    private Timer?  _filterTimer;
    private const int FilterDebounceMs = 200;

    // ------------------------------------------------------------------
    // Collections
    // ------------------------------------------------------------------

    public ObservableCollection<WhfmtCategoryNodeVm>   CategoryNodes { get; } = [];
    public ObservableCollection<WhfmtFormatItemVm>     FlatItems     { get; } = [];
    public WhfmtFormatDetailVm                          Detail        { get; } = new();

    // ------------------------------------------------------------------
    // Properties
    // ------------------------------------------------------------------

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            ScheduleFilter();
        }
    }

    public bool IsTreeView
    {
        get => _isTreeView;
        set { if (SetField(ref _isTreeView, value)) ApplyFilter(); }
    }

    public bool ShowBuiltIns
    {
        get => _showBuiltIns;
        set { if (SetField(ref _showBuiltIns, value)) RebuildTree(); }
    }

    public bool ShowUserFormats
    {
        get => _showUserFmts;
        set { if (SetField(ref _showUserFmts, value)) RebuildTree(); }
    }

    public bool IsWatching
    {
        get => _isWatching;
        private set => SetField(ref _isWatching, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public WhfmtSortMode CurrentWhfmtSortMode
    {
        get => _currentWhfmtSortMode;
        set { if (SetField(ref _currentWhfmtSortMode, value)) { OnPropertyChanged(nameof(WhfmtSortModeLabel)); RebuildTree(); } }
    }

    public WhfmtSearchField CurrentWhfmtSearchField
    {
        get => _currentWhfmtSearchField;
        set { if (SetField(ref _currentWhfmtSearchField, value)) ScheduleFilter(); }
    }

    public bool IsRegexSearch
    {
        get => _isRegexSearch;
        set { if (SetField(ref _isRegexSearch, value)) ScheduleFilter(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public int SearchMatchCount
    {
        get => _searchMatchCount;
        private set => SetField(ref _searchMatchCount, value);
    }

    public int BuiltInCount
    {
        get => _builtInCount;
        private set => SetField(ref _builtInCount, value);
    }

    public int UserFormatCount
    {
        get => _userFormatCount;
        private set => SetField(ref _userFormatCount, value);
    }

    public int FailureCount
    {
        get => _failureCount;
        private set => SetField(ref _failureCount, value);
    }

    public bool HasLoadFailures => _failureCount > 0;

    public int TotalFormatCount => _builtInCount + _userFormatCount;

    public string WhfmtSortModeLabel => _currentWhfmtSortMode switch
    {
        WhfmtSortMode.ByCategory => "Category",
        WhfmtSortMode.ByQuality  => "Quality",
        WhfmtSortMode.BySource   => "Source",
        _                   => "Name"
    };

    // ------------------------------------------------------------------
    // Commands
    // ------------------------------------------------------------------

    public ICommand RefreshCommand      { get; }
    public ICommand AddFormatCommand    { get; }
    public ICommand OpenFolderCommand   { get; }
    public ICommand ToggleWatchCommand  { get; }
    public ICommand ToggleViewCommand   { get; }
    public ICommand SetSortCommand      { get; }
    public ICommand NewFormatCommand    { get; }
    public ICommand FindNextCommand     { get; }
    public ICommand FindPreviousCommand { get; }
    public ICommand ToggleShowFailuresCommand { get; }

    // ------------------------------------------------------------------
    // Events raised toward the View / MainWindow
    // ------------------------------------------------------------------

    /// <summary>
    /// Raised when the user requests opening a format.
    /// Arg: resource key (for built-ins) or absolute file path (for user formats).
    /// </summary>
    public event EventHandler<FormatOpenRequest>? OpenFormatRequested;

    /// <summary>Raised when the user requests exporting a built-in format to disk.</summary>
    public event EventHandler<string>? ExportFormatRequested;

    /// <summary>Raised when the user wants to view the raw JSON in a code editor tab.</summary>
    public event EventHandler<string>? ViewJsonRequested;

    // ------------------------------------------------------------------
    // Constructor
    // ------------------------------------------------------------------

    public WhfmtBrowserViewModel()
    {
        RefreshCommand      = new RelayCommand(OnRefresh);
        AddFormatCommand    = new RelayCommand(OnAddFormat,   () => _adHocSvc is not null);
        OpenFolderCommand   = new RelayCommand(OnOpenFolder,  () => _adHocSvc is not null);
        ToggleWatchCommand  = new RelayCommand(OnToggleWatch, () => _adHocSvc is not null);
        ToggleViewCommand   = new RelayCommand(() => IsTreeView = !IsTreeView);
        ClearSearchCommand  = new RelayCommand(() => SearchText = string.Empty);
        SetSortCommand      = new RelayCommand<string>(s =>
        {
            if (Enum.TryParse<WhfmtSortMode>(s, ignoreCase: true, out var mode))
                CurrentWhfmtSortMode = mode;
        });
        NewFormatCommand    = new RelayCommand(OnNewFormat, () => _adHocSvc is not null);
        FindNextCommand     = new RelayCommand(() => { /* navigation handled by ISearchTarget in view */ });
        FindPreviousCommand = new RelayCommand(() => { /* navigation handled by ISearchTarget in view */ });
        ToggleShowFailuresCommand = new RelayCommand(OnToggleShowFailures);
    }

    public ICommand ClearSearchCommand { get; }

    // ------------------------------------------------------------------
    // Initialization
    // ------------------------------------------------------------------

    /// <summary>
    /// Wires up all dependencies and performs the initial catalog load.
    /// Must be called once on the UI thread before the panel is shown.
    /// </summary>
    public void Initialize(
        IEmbeddedFormatCatalog  embCatalog,
        IFormatCatalogService   catalogSvc,
        WhfmtAdHocFormatService adHocSvc,
        WhfmtExplorerSettings   settings,
        Action<Action>?         dispatchToUi = null)
    {
        _embCatalog    = embCatalog;
        _catalogSvc    = catalogSvc;
        _adHocSvc      = adHocSvc;
        _settings      = settings;
        _dispatchToUi  = dispatchToUi ?? (a => a());

        // Apply initial settings
        _showBuiltIns  = settings.ShowBuiltInFormats;
        _showUserFmts  = settings.ShowUserFormats;
        _isTreeView    = settings.DefaultViewMode == "Tree";

        // Subscribe to catalog reload events (partial hot-reload)
        catalogSvc.FormatReloaded += OnFormatReloaded;

        // Subscribe to adhoc file system changes
        adHocSvc.CatalogChanged += OnAdHocCatalogChanged;

        // Start watching if setting says so
        if (settings.EnableHotReload)
        {
            adHocSvc.StartWatching();
            _isWatching = true;
        }

        RebuildTree();
    }

    // ------------------------------------------------------------------
    // Tree building
    // ------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the entire category tree and flat list from the catalog.
    /// Called after initialization, refresh, or settings changes.
    /// </summary>
    public void RebuildTree()
    {
        if (_embCatalog is null || _catalogSvc is null || _adHocSvc is null) return;

        IsBusy = true;
        try
        {
            var allItems = new List<WhfmtFormatItemVm>();

            // -- Built-in formats --
            if (_showBuiltIns)
            {
                var minQuality = _settings?.QualityScoreThreshold ?? 0;
                foreach (var entry in _embCatalog.Query()
                    .WithMinQuality(minQuality)
                    .Where(e => !IsExcluded(e.Name))
                    .Execute())
                {
                    allItems.Add(BuildBuiltInItemVm(entry));
                }
            }

            // -- User formats --
            if (_showUserFmts)
            {
                foreach (var path in _adHocSvc.GetUserFormatPaths())
                    allItems.Add(BuildUserItemVm(path));
            }

            // -- Load failures --
            if (_settings?.ShowLoadFailures == true)
            {
                foreach (var failure in _catalogSvc.LoadFailures)
                    allItems.Add(BuildFailureItemVm(failure));
            }

            // Update raw counts before filter
            BuiltInCount    = allItems.Count(i => i.Source == FormatSource.BuiltIn);
            UserFormatCount = allItems.Count(i => i.Source == FormatSource.User);
            FailureCount    = allItems.Count(i => i.Source == FormatSource.LoadFailure);
            OnPropertyChanged(nameof(HasLoadFailures));
            OnPropertyChanged(nameof(TotalFormatCount));

            // Apply text filter
            var filtered = ApplySearchFilter(allItems, _searchText);
            SearchMatchCount = string.IsNullOrWhiteSpace(_searchText) ? 0 : filtered.Count;

            // Apply sort
            var sorted = ApplySort(filtered).ToList();

            // Rebuild tree nodes
            RebuildCategoryNodes(sorted);

            // Rebuild flat list
            FlatItems.Clear();
            foreach (var item in sorted)
                FlatItems.Add(item);

            UpdateStatusText(allItems);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ------------------------------------------------------------------
    // Filter
    // ------------------------------------------------------------------

    private void ScheduleFilter()
    {
        _filterTimer?.Change(FilterDebounceMs, Timeout.Infinite);
        _filterTimer ??= new Timer(_ => _dispatchToUi!(() => ApplyFilter()),
                                   null, FilterDebounceMs, Timeout.Infinite);
        _filterTimer.Change(FilterDebounceMs, Timeout.Infinite);
    }

    private void ApplyFilter()
    {
        if (_embCatalog is null) return;
        RebuildTree();
    }

    private static readonly StringComparison OrdinalIC = StringComparison.OrdinalIgnoreCase;

    private List<WhfmtFormatItemVm> ApplySearchFilter(List<WhfmtFormatItemVm> items, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return items;

        var q = search.Trim();

        if (_isRegexSearch)
        {
            try
            {
                var rx = new Regex(q, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                return items.Where(i => MatchesItemRegex(i, rx)).ToList();
            }
            catch (RegexParseException)
            {
                return items; // invalid pattern → show all
            }
        }

        return items.Where(i => MatchesItem(i, q)).ToList();
    }

    private bool MatchesItem(WhfmtFormatItemVm i, string q)
        => _currentWhfmtSearchField switch
        {
            WhfmtSearchField.Name      => i.Name.Contains(q, OrdinalIC),
            WhfmtSearchField.Extension => i.ExtensionsDisplay.Contains(q, OrdinalIC),
            WhfmtSearchField.Author    => i.Author.Contains(q, OrdinalIC),
            WhfmtSearchField.Category  => i.Category.Contains(q, OrdinalIC),
            _                     => i.Name.Contains(q, OrdinalIC)
                                  || i.Category.Contains(q, OrdinalIC)
                                  || i.ExtensionsDisplay.Contains(q, OrdinalIC)
                                  || i.Author.Contains(q, OrdinalIC)
                                  || i.Description.Contains(q, OrdinalIC),
        };

    private bool MatchesItemRegex(WhfmtFormatItemVm i, Regex rx)
        => _currentWhfmtSearchField switch
        {
            WhfmtSearchField.Name      => rx.IsMatch(i.Name),
            WhfmtSearchField.Extension => rx.IsMatch(i.ExtensionsDisplay),
            WhfmtSearchField.Author    => rx.IsMatch(i.Author),
            WhfmtSearchField.Category  => rx.IsMatch(i.Category),
            _                     => rx.IsMatch(i.Name)
                                  || rx.IsMatch(i.Category)
                                  || rx.IsMatch(i.ExtensionsDisplay)
                                  || rx.IsMatch(i.Author)
                                  || rx.IsMatch(i.Description),
        };

    private IEnumerable<WhfmtFormatItemVm> ApplySort(IEnumerable<WhfmtFormatItemVm> src)
        => _currentWhfmtSortMode switch
        {
            WhfmtSortMode.ByCategory => src.OrderBy(i => i.SortKeyCategory).ThenBy(i => i.SortKeyName),
            WhfmtSortMode.ByQuality  => src.OrderBy(i => i.SortKeyQuality).ThenBy(i => i.SortKeyName),
            WhfmtSortMode.BySource   => src.OrderBy(i => i.SourceLabel).ThenBy(i => i.SortKeyName),
            _                   => src.OrderBy(i => i.SortKeyName),
        };

    private void RebuildCategoryNodes(List<WhfmtFormatItemVm> items)
    {
        // Preserve expansion state
        var expanded = CategoryNodes.ToDictionary(n => n.Name, n => n.IsExpanded);

        CategoryNodes.Clear();

        foreach (var group in items.GroupBy(i => i.Category).OrderBy(g => g.Key))
        {
            var node = new WhfmtCategoryNodeVm(group.Key)
            {
                IsExpanded = expanded.GetValueOrDefault(group.Key, true)
            };
            foreach (var item in group.OrderBy(i => i.Name))
                node.Items.Add(item);
            node.Recalculate();
            CategoryNodes.Add(node);
        }
    }

    private void UpdateStatusText(List<WhfmtFormatItemVm> allItems)
    {
        var builtIn   = allItems.Count(i => i.Source == FormatSource.BuiltIn);
        var user      = allItems.Count(i => i.Source == FormatSource.User);
        var failures  = allItems.Count(i => i.Source == FormatSource.LoadFailure);
        var parts     = new List<string>();

        if (_showBuiltIns)  parts.Add($"{builtIn} built-in");
        if (_showUserFmts)  parts.Add($"{user} user");
        if (failures > 0)   parts.Add($"{failures} failed");

        StatusText = string.Join("  |  ", parts);
    }

    // ------------------------------------------------------------------
    // Selection
    // ------------------------------------------------------------------

    /// <summary>
    /// Called by the panel code-behind when the user selects a format item.
    /// </summary>
    /// <summary>Called by the code-behind to raise an open request for a specific item (e.g., on double-click).</summary>
    public void RequestOpenFormat(WhfmtFormatItemVm item, FormatOpenMode mode = FormatOpenMode.Editable)
        => OpenFormatRequested?.Invoke(this, new FormatOpenRequest(item.ResourceKey ?? item.FilePath, mode, item.Source));

    public void OnItemSelected(WhfmtFormatItemVm? item)
    {
        if (_embCatalog is null || _catalogSvc is null) return;

        Detail.LoadFrom(item, _embCatalog, _catalogSvc);

        if (item is null) return;

        // Wire detail card commands
        Detail.OpenCommand     = new RelayCommand(() => RaiseOpen(item, readOnly: false));
        Detail.ExportCommand   = new RelayCommand(() => ExportFormatRequested?.Invoke(this, GetKeyOrPath(item)),
                                                  ()  => item.Source == FormatSource.BuiltIn);
        Detail.CopyJsonCommand = new RelayCommand(() => OnCopyJson(item));
        Detail.RetryLoadCommand= new RelayCommand(() => { RebuildTree(); }, () => item.IsLoadFailure);
        Detail.ExcludeCommand  = new RelayCommand(() => OnExclude(item),  () => item.IsLoadFailure);
    }

    // ------------------------------------------------------------------
    // Per-item VM factories
    // ------------------------------------------------------------------

    private WhfmtFormatItemVm BuildBuiltInItemVm(EmbeddedFormatEntry entry)
    {
        var vm = new WhfmtFormatItemVm
        {
            Name             = entry.Name,
            Category         = entry.Category,
            Description      = entry.Description,
            Extensions       = entry.Extensions,
            ExtensionsDisplay= string.Join(", ", entry.Extensions),
            Version          = entry.Version,
            Author           = entry.Author,
            Platform         = entry.Platform,
            PreferredEditor  = entry.PreferredEditor,
            DiffMode         = entry.DiffMode,
            QualityScore     = entry.QualityScore,
            Source           = FormatSource.BuiltIn,
            ResourceKey      = entry.ResourceKey
        };
        WireItemCommands(vm);
        return vm;
    }

    private WhfmtFormatItemVm BuildUserItemVm(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        // Try to enrich from catalog if it was successfully loaded
        var def = _catalogSvc?.FindFormat(name);
        var vm = new WhfmtFormatItemVm
        {
            Name             = name,
            Category         = "User",
            Description      = def?.Description      ?? filePath,
            Extensions       = (IReadOnlyList<string>?)(def?.Extensions) ?? [],
            ExtensionsDisplay= def?.Extensions is not null
                                    ? string.Join(", ", def.Extensions)
                                    : string.Empty,
            Version          = def?.Version          ?? string.Empty,
            Author           = def?.Author           ?? string.Empty,
            QualityScore     = def?.QualityMetrics?.CompletenessScore ?? -1,
            Source           = FormatSource.User,
            FilePath         = filePath
        };
        WireItemCommands(vm);
        return vm;
    }

    private WhfmtFormatItemVm BuildFailureItemVm(FormatLoadFailure failure)
    {
        var sourceName = Path.GetFileNameWithoutExtension(failure.Source) ?? "Unknown";
        var vm = new WhfmtFormatItemVm
        {
            Name             = sourceName,
            Category         = "Failed",
            Description      = failure.Reason ?? string.Empty,
            Extensions       = [],
            ExtensionsDisplay= string.Empty,
            QualityScore     = -1,
            Source           = FormatSource.LoadFailure,
            FilePath         = failure.Source,
            FailureReason    = failure.Reason
        };
        WireItemCommands(vm);
        return vm;
    }

    private void WireItemCommands(WhfmtFormatItemVm vm)
    {
        vm.OpenCommand            = new RelayCommand(() => RaiseOpen(vm, readOnly: false));
        vm.OpenReadOnlyCommand    = new RelayCommand(() => RaiseOpen(vm, readOnly: true),
                                                     () => vm.Source == FormatSource.BuiltIn);
        vm.ExportToFileCommand    = new RelayCommand(() => ExportFormatRequested?.Invoke(this, GetKeyOrPath(vm)),
                                                     () => vm.Source == FormatSource.BuiltIn);
        vm.ViewJsonCommand        = new RelayCommand(() => ViewJsonRequested?.Invoke(this, GetKeyOrPath(vm)),
                                                     () => !vm.IsLoadFailure);
        vm.DeleteCommand          = new RelayCommand(() => OnDeleteUserFormat(vm),
                                                     () => vm.Source == FormatSource.User);
        vm.CopyPathCommand        = new RelayCommand(() => OnCopyPath(vm));
        vm.RevealInExplorerCommand= new RelayCommand(() => OnRevealInExplorer(vm),
                                                     () => vm.Source == FormatSource.User && vm.FilePath is not null);
        vm.DuplicateCommand       = new RelayCommand(() => OnDuplicateFormat(vm),
                                                     () => vm.Source == FormatSource.User && vm.FilePath is not null);
    }

    // ------------------------------------------------------------------
    // Command handlers
    // ------------------------------------------------------------------

    private void OnRefresh() => RebuildTree();

    private void OnNewFormat()
    {
        if (_adHocSvc is null) return;
        var dir  = _adHocSvc.UserFormatDirectory;
        var path = FindUniqueFileName(dir, "NewFormat", ".whfmt");
        var skeleton = $$"""
            {
              "$schema": "https://wpfhexeditor.dev/schema/whfmt-1.0.json",
              "name": "NewFormat",
              "description": "",
              "version": "1.0",
              "author": "",
              "category": "Other",
              "extensions": [],
              "blocks": []
            }
            """;
        File.WriteAllText(path, skeleton, System.Text.Encoding.UTF8);
        RebuildTree();
        ViewJsonRequested?.Invoke(this, path);
    }

    private void OnDuplicateFormat(WhfmtFormatItemVm vm)
    {
        if (_adHocSvc is null || vm.FilePath is null || !File.Exists(vm.FilePath)) return;
        var dir  = _adHocSvc.UserFormatDirectory;
        var dest = FindUniqueFileName(dir, vm.Name + "_copy", ".whfmt");
        File.Copy(vm.FilePath, dest, overwrite: false);
        RebuildTree();
    }

    private void OnToggleShowFailures()
    {
        if (_settings is null) return;
        _settings.ShowLoadFailures = !_settings.ShowLoadFailures;
        RebuildTree();
    }

    private static string FindUniqueFileName(string dir, string baseName, string ext)
    {
        var candidate = Path.Combine(dir, baseName + ext);
        if (!File.Exists(candidate)) return candidate;
        for (var i = 2; i < 100; i++)
        {
            candidate = Path.Combine(dir, $"{baseName}_{i}{ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return Path.Combine(dir, $"{baseName}_{Guid.NewGuid():N}{ext}");
    }

    private void OnAddFormat()
    {
        // The view handles the file picker; it calls AddFormatFromPath()
        OpenFormatRequested?.Invoke(this, new FormatOpenRequest(null, FormatOpenMode.AddUserFormat));
    }

    private void OnOpenFolder()
    {
        if (_adHocSvc is not null)
            OpenFormatRequested?.Invoke(this, new FormatOpenRequest(_adHocSvc.UserFormatDirectory, FormatOpenMode.RevealFolder));
    }

    private void OnToggleWatch()
    {
        if (_adHocSvc is null) return;
        if (_adHocSvc.IsWatching)
        {
            _adHocSvc.StopWatching();
            IsWatching = false;
        }
        else
        {
            _adHocSvc.StartWatching();
            IsWatching = true;
        }
    }

    private void OnDeleteUserFormat(WhfmtFormatItemVm vm)
    {
        if (_adHocSvc is null || vm.FilePath is null) return;
        var result = _adHocSvc.RemoveFormat(Path.GetFileName(vm.FilePath));
        if (result.Success) RebuildTree();
    }

    private static void OnCopyPath(WhfmtFormatItemVm vm)
    {
        var text = vm.FilePath ?? vm.ResourceKey ?? vm.Name;
        System.Windows.Clipboard.SetText(text);
    }

    private static void OnRevealInExplorer(WhfmtFormatItemVm vm)
    {
        if (vm.FilePath is not null && File.Exists(vm.FilePath))
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{vm.FilePath}\"");
    }

    private void OnCopyJson(WhfmtFormatItemVm vm)
    {
        string? json = null;
        if (vm.Source == FormatSource.BuiltIn && vm.ResourceKey is not null)
            json = _embCatalog?.GetJson(vm.ResourceKey);
        else if (vm.FilePath is not null && File.Exists(vm.FilePath))
            json = File.ReadAllText(vm.FilePath);

        if (json is not null)
            System.Windows.Clipboard.SetText(json);
    }

    private void OnExclude(WhfmtFormatItemVm vm)
    {
        if (_settings is null) return;
        var name = vm.FilePath is not null
            ? Path.GetFileName(vm.FilePath)
            : vm.Name + ".whfmt";

        if (!_settings.ExcludedFileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            _settings.ExcludedFileNames.Add(name);

        RebuildTree();
    }

    // ------------------------------------------------------------------
    // Public API for the panel code-behind
    // ------------------------------------------------------------------

    /// <summary>
    /// Adds a format from a path chosen by the user via a file picker.
    /// Returns the error message on failure, null on success.
    /// </summary>
    public string? AddFormatFromPath(string sourcePath)
    {
        if (_adHocSvc is null) return "Service not initialized.";
        var result = _adHocSvc.AddFormat(sourcePath);
        if (result.Success) RebuildTree();
        return result.Success ? null : result.Error;
    }

    // ------------------------------------------------------------------
    // Event handlers
    // ------------------------------------------------------------------

    private void OnAdHocCatalogChanged(object? sender, EventArgs e)
        => _dispatchToUi!(() => RebuildTree());

    private void OnFormatReloaded(object? sender, string formatName)
        => _dispatchToUi!(() => RebuildTree());

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private bool IsExcluded(string name)
        => _settings?.ExcludedFileNames.Contains(name + ".whfmt", StringComparer.OrdinalIgnoreCase) ?? false;

    private static string GetKeyOrPath(WhfmtFormatItemVm vm)
        => vm.ResourceKey ?? vm.FilePath ?? vm.Name;

    private void RaiseOpen(WhfmtFormatItemVm vm, bool readOnly)
    {
        var mode = readOnly ? FormatOpenMode.ReadOnly : FormatOpenMode.Editable;
        var key  = GetKeyOrPath(vm);
        OpenFormatRequested?.Invoke(this, new FormatOpenRequest(key, mode, vm.Source));
    }

    // ------------------------------------------------------------------
    // IDisposable
    // ------------------------------------------------------------------

    public void Dispose()
    {
        _filterTimer?.Dispose();
        if (_adHocSvc is not null)
            _adHocSvc.CatalogChanged -= OnAdHocCatalogChanged;
        _catalogSvc?.FormatReloaded -= OnFormatReloaded;
    }
}

// ------------------------------------------------------------------
// Supporting types
// ------------------------------------------------------------------

/// <summary>Sort order for the format list in the Format Browser.</summary>
public enum WhfmtSortMode  { ByName, ByCategory, ByQuality, BySource }

/// <summary>Field scope for the quick search filter in the Format Browser.</summary>
public enum WhfmtSearchField { All, Name, Extension, Author, Category }

/// <summary>Modes for opening a format from the browser.</summary>
public enum FormatOpenMode
{
    Editable,
    ReadOnly,
    AddUserFormat,
    RevealFolder
}

/// <summary>Payload for <see cref="WhfmtBrowserViewModel.OpenFormatRequested"/>.</summary>
public sealed record FormatOpenRequest(
    string? KeyOrPath,
    FormatOpenMode Mode,
    FormatSource Source = FormatSource.BuiltIn);

