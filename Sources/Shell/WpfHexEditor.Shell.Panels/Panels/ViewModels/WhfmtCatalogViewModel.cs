// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/ViewModels/WhfmtCatalogViewModel.cs
// Description: ViewModel for the Format Catalog virtual document tab.
//              Displays all loaded formats in a sortable, searchable DataGrid.
// Architecture: Pure ViewModel; exposes an ICollectionView for DataGrid binding.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Options;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions.Query;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Shell.Panels.Services;

namespace WpfHexEditor.Shell.Panels.ViewModels;

/// <summary>
/// ViewModel for the Format Catalog document tab (full-width grid view of all formats).
/// </summary>
public sealed class WhfmtCatalogViewModel : ViewModelBase, IDisposable
{
    private IEmbeddedFormatCatalog?  _embCatalog;
    private IFormatCatalogService?   _catalogSvc;

    private string  _searchText   = string.Empty;
    private string  _statusText   = "Loading…";

    private readonly ObservableCollection<WhfmtFormatItemVm> _allItems = [];
    private readonly List<WhfmtFormatItemVm>                  _selectedItems = [];

    // ------------------------------------------------------------------
    // Collections / View
    // ------------------------------------------------------------------

    public ICollectionView FilteredView { get; }
    public WhfmtFormatDetailVm Detail   { get; } = new();

    // ------------------------------------------------------------------
    // Properties
    // ------------------------------------------------------------------

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            FilteredView.Refresh();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public int SelectedCount => _selectedItems.Count;
    public int TotalCount    => _allItems.Count;

    // ------------------------------------------------------------------
    // Commands
    // ------------------------------------------------------------------

    public ICommand OpenSelectedCommand       { get; }
    public ICommand ExportSelectedCommand     { get; }
    public ICommand AddFormatCommand          { get; }
    public ICommand RefreshCommand            { get; }

    // ------------------------------------------------------------------
    // Events
    // ------------------------------------------------------------------

    /// <summary>Raised when user requests opening one or more formats.</summary>
    public event EventHandler<IReadOnlyList<string>>? OpenFormatsRequested;

    /// <summary>Raised when user requests exporting one or more formats.</summary>
    public event EventHandler<IReadOnlyList<string>>? ExportFormatsRequested;

    /// <summary>Raised when user requests adding a format file.</summary>
    public event EventHandler? AddFormatRequested;

    // ------------------------------------------------------------------
    // Constructor
    // ------------------------------------------------------------------

    public WhfmtCatalogViewModel()
    {
        FilteredView = CollectionViewSource.GetDefaultView(_allItems);
        FilteredView.Filter = FilterItem;

        OpenSelectedCommand   = new RelayCommand(OnOpenSelected,   () => _selectedItems.Count > 0);
        ExportSelectedCommand = new RelayCommand(OnExportSelected, () => _selectedItems.Any(i => i.Source == FormatSource.BuiltIn));
        AddFormatCommand      = new RelayCommand(() => AddFormatRequested?.Invoke(this, EventArgs.Empty));
        RefreshCommand        = new RelayCommand(RebuildItems);
    }

    // ------------------------------------------------------------------
    // Initialization
    // ------------------------------------------------------------------

    public void Initialize(
        IEmbeddedFormatCatalog  embCatalog,
        IFormatCatalogService   catalogSvc,
        WhfmtAdHocFormatService adHocSvc,
        WhfmtExplorerSettings   settings)
    {
        _embCatalog = embCatalog;
        _catalogSvc = catalogSvc;

        adHocSvc.CatalogChanged += (_, _) => RebuildItems();
        catalogSvc.FormatReloaded += (_, _) => RebuildItems();

        RebuildItems();
    }

    // ------------------------------------------------------------------
    // Selection (called from code-behind via DataGrid.SelectionChanged)
    // ------------------------------------------------------------------

    public void SetMultiSelection(IEnumerable<WhfmtFormatItemVm> items)
    {
        _selectedItems.Clear();
        _selectedItems.AddRange(items);
        OnPropertyChanged(nameof(SelectedCount));

        if (_selectedItems.Count == 1)
        {
            var item = _selectedItems[0];
            Detail.LoadFrom(item, _embCatalog!, _catalogSvc!);
            Detail.OpenCommand     = new RelayCommand(() => OnOpenSelected());
            Detail.ExportCommand   = new RelayCommand(() => OnExportSelected(), () => item.Source == FormatSource.BuiltIn);
            Detail.CopyJsonCommand = new RelayCommand(() => OnCopyJson(item));
            Detail.RawJson         = LoadRawJson(item);
        }
        else
        {
            Detail.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Private
    // ------------------------------------------------------------------

    private void RebuildItems()
    {
        _allItems.Clear();

        if (_embCatalog is null) return;

        foreach (var entry in _embCatalog.Query().OrderByCategoryThenName().Execute())
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
                QualityScore     = entry.QualityScore,
                Source           = FormatSource.BuiltIn,
                ResourceKey      = entry.ResourceKey
            };
            vm.OpenCommand   = new RelayCommand(() => OpenFormatsRequested?.Invoke(this, [GetKey(vm)]));
            vm.ExportToFileCommand = new RelayCommand(() => ExportFormatsRequested?.Invoke(this, [GetKey(vm)]));
            _allItems.Add(vm);
        }

        // User formats
        if (_catalogSvc is not null)
        {
            foreach (var def in _catalogSvc.GetAllFormats()
                .Where(d => !_embCatalog.Query().WithName(d.FormatName ?? "").Any()))
            {
                var vm = new WhfmtFormatItemVm
                {
                    Name             = def.FormatName ?? "Unknown",
                    Category         = "User",
                    Extensions       = (IReadOnlyList<string>?)def.Extensions ?? [],
                    ExtensionsDisplay= def.Extensions is not null ? string.Join(", ", def.Extensions) : string.Empty,
                    Version          = def.Version ?? string.Empty,
                    Author           = def.Author  ?? string.Empty,
                    QualityScore     = def.QualityMetrics?.CompletenessScore ?? 0,
                    Source           = FormatSource.User,
                };
                _allItems.Add(vm);
            }
        }

        // Load failures
        if (_catalogSvc is not null)
        {
            foreach (var f in _catalogSvc.LoadFailures)
                _allItems.Add(new WhfmtFormatItemVm
                {
                    Name          = System.IO.Path.GetFileNameWithoutExtension(f.Source) ?? f.Source,
                    Category      = "Failed",
                    Source        = FormatSource.LoadFailure,
                    FailureReason = f.Reason,
                    QualityScore  = -1
                });
        }

        FilteredView.Refresh();
        UpdateStatus();
    }

    private bool FilterItem(object obj)
    {
        if (obj is not WhfmtFormatItemVm item) return false;
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        var q = _searchText.Trim();
        return item.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.Category.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.ExtensionsDisplay.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.Author.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateStatus()
    {
        var builtIn = _allItems.Count(i => i.Source == FormatSource.BuiltIn);
        var user    = _allItems.Count(i => i.Source == FormatSource.User);
        var failed  = _allItems.Count(i => i.Source == FormatSource.LoadFailure);
        StatusText  = $"{builtIn} built-in  |  {user} user  |  {failed} failed";
    }

    private void OnOpenSelected()
    {
        var keys = _selectedItems.Select(GetKey).ToList();
        if (keys.Count > 0) OpenFormatsRequested?.Invoke(this, keys);
    }

    private void OnExportSelected()
    {
        var keys = _selectedItems
            .Where(i => i.Source == FormatSource.BuiltIn)
            .Select(GetKey).ToList();
        if (keys.Count > 0) ExportFormatsRequested?.Invoke(this, keys);
    }

    private string? LoadRawJson(WhfmtFormatItemVm vm)
    {
        if (vm.Source == FormatSource.BuiltIn && vm.ResourceKey is not null)
            return _embCatalog?.GetJson(vm.ResourceKey);
        if (vm.FilePath is not null && System.IO.File.Exists(vm.FilePath))
            return System.IO.File.ReadAllText(vm.FilePath);
        return null;
    }

    private void OnCopyJson(WhfmtFormatItemVm vm)
    {
        var json = LoadRawJson(vm);
        if (json is not null)
            System.Windows.Clipboard.SetText(json);
    }

    private static string GetKey(WhfmtFormatItemVm vm) => vm.ResourceKey ?? vm.FilePath ?? vm.Name;

    public void Dispose() { }
}
