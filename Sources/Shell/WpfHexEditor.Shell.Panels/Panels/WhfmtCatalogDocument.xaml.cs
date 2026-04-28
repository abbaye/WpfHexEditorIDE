// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WhfmtCatalogDocument.xaml.cs
// Description: Code-behind for the Format Catalog virtual document tab.
//              Implements IEditorToolbarContributor, IStatusBarContributor,
//              and ISearchTarget (Ctrl+F QuickSearchBar overlay on the grid).
// Architecture: MVVM + interface adapters; no business logic here.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Options;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Views;
using WpfHexEditor.Shell.Panels.Services;
using WpfHexEditor.Shell.Panels.ViewModels;

namespace WpfHexEditor.Shell.Panels.Panels;

public partial class WhfmtCatalogDocument : UserControl,
    IEditorToolbarContributor,
    IStatusBarContributor,
    ISearchTarget
{
    private readonly WhfmtCatalogViewModel _vm;
    private QuickSearchBar? _searchBar;

    private readonly StatusBarItem _sbTotal;
    private readonly StatusBarItem _sbSelected;

    // ISearchTarget state
    private int _matchCount;
    private int _currentMatchIndex;

    public WhfmtCatalogDocument()
    {
        _vm = new WhfmtCatalogViewModel();
        _vm.OpenFormatsRequested   += (s, e) => OpenFormatsRequested?.Invoke(this, e);
        _vm.ExportFormatsRequested += (s, e) => ExportFormatsRequested?.Invoke(this, e);
        _vm.AddFormatRequested     += OnVmAddFormatRequested;
        _vm.PropertyChanged        += OnVmPropertyChanged;

        DataContext = _vm;
        InitializeComponent();

        // ── Toolbar items ──────────────────────────────────────────────────
        ToolbarItems =
        [
            new EditorToolbarItem { Icon = "\uE7AC", Tooltip = "Open selected", Command = new LambdaCmd(() => _vm.OpenSelectedCommand.Execute(null)) },
            new EditorToolbarItem { Icon = "\uE74E", Tooltip = "Export selected to file…", Command = new LambdaCmd(() => _vm.ExportSelectedCommand.Execute(null)) },
            new EditorToolbarItem { IsSeparator = true },
            new EditorToolbarItem { Icon = "\uE109", Tooltip = "Add format file…", Command = new LambdaCmd(() => _vm.AddFormatCommand.Execute(null)) },
            new EditorToolbarItem { Icon = "\uE72C", Tooltip = "Refresh catalog (F5)", Command = new LambdaCmd(() => _vm.RefreshCommand.Execute(null)) },
        ];

        // ── Status bar items ───────────────────────────────────────────────
        _sbTotal    = new StatusBarItem { Label = "0 formats" };
        _sbSelected = new StatusBarItem { Label = string.Empty, IsVisible = false };

        StatusBarItems = [ _sbTotal, _sbSelected ];

        Loaded += OnLoaded;
    }

    // ------------------------------------------------------------------
    // IEditorToolbarContributor
    // ------------------------------------------------------------------

    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; }

    // ------------------------------------------------------------------
    // IStatusBarContributor
    // ------------------------------------------------------------------

    public ObservableCollection<StatusBarItem> StatusBarItems { get; }

    public void RefreshStatusBarItems()
    {
        _sbTotal.Label     = $"{_vm.TotalCount} formats";
        _sbSelected.Label  = _vm.SelectedCount > 0 ? $"{_vm.SelectedCount} selected" : string.Empty;
        _sbSelected.IsVisible = _vm.SelectedCount > 0;
    }

    // ------------------------------------------------------------------
    // ISearchTarget — filters the DataGrid via ViewModel.SearchText
    // ------------------------------------------------------------------

    public SearchBarCapabilities Capabilities => SearchBarCapabilities.CaseSensitive;

    public int MatchCount        => _matchCount;
    public int CurrentMatchIndex => _currentMatchIndex;

    public event EventHandler? SearchResultsChanged;

    public void Find(string query, SearchTargetOptions options = default)
    {
        _vm.SearchText    = query;
        _matchCount       = CatalogGrid.Items.Count;
        _currentMatchIndex = _matchCount > 0 ? 1 : 0;
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FindNext()
    {
        if (_matchCount == 0) return;
        _currentMatchIndex = (_currentMatchIndex % _matchCount) + 1;
        ScrollToMatchIndex(_currentMatchIndex - 1);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FindPrevious()
    {
        if (_matchCount == 0) return;
        _currentMatchIndex = _currentMatchIndex <= 1 ? _matchCount : _currentMatchIndex - 1;
        ScrollToMatchIndex(_currentMatchIndex - 1);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSearch()
    {
        _vm.SearchText    = string.Empty;
        _matchCount       = 0;
        _currentMatchIndex = 0;
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Replace(string replacement)     { /* not supported */ }
    public void ReplaceAll(string replacement)  { /* not supported */ }
    public UIElement? GetCustomFiltersContent() => null;

    private void ScrollToMatchIndex(int index)
    {
        if (index >= 0 && index < CatalogGrid.Items.Count)
        {
            CatalogGrid.ScrollIntoView(CatalogGrid.Items[index]);
            CatalogGrid.SelectedIndex = index;
        }
    }

    // ------------------------------------------------------------------
    // Loaded — install QuickSearchBar overlay
    // ------------------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _searchBar = new QuickSearchBar();
        _searchBar.BindToTarget(this);
        _searchBar.Visibility = Visibility.Collapsed;
        _searchBar.OnCloseRequested += (_, _) =>
        {
            _searchBar.Visibility = Visibility.Collapsed;
            SearchBarCanvas.IsHitTestVisible = false;
            ClearSearch();
        };
        SearchBarCanvas.Children.Add(_searchBar);
        SearchBarCanvas.IsHitTestVisible = false;
    }

    // ------------------------------------------------------------------
    // Keyboard
    // ------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowQuickSearchBar();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            _vm.RefreshCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ShowQuickSearchBar()
    {
        if (_searchBar is null) return;
        _searchBar.Visibility = Visibility.Visible;
        SearchBarCanvas.IsHitTestVisible = true;
        _searchBar.EnsureDefaultPosition(SearchBarCanvas);
        _searchBar.FocusSearchInput();
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public event EventHandler<IReadOnlyList<string>>? OpenFormatsRequested;
    public event EventHandler<IReadOnlyList<string>>? ExportFormatsRequested;

    public void SetCatalog(
        IEmbeddedFormatCatalog  embCatalog,
        IFormatCatalogService   catalogSvc,
        WhfmtAdHocFormatService adHocSvc,
        WhfmtExplorerSettings   settings)
    {
        _vm.Initialize(embCatalog, catalogSvc, adHocSvc, settings);
    }

    // ------------------------------------------------------------------
    // DataGrid events
    // ------------------------------------------------------------------

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CatalogGrid.SelectedItems
            .OfType<WhfmtFormatItemVm>()
            .ToList();
        _vm.SetMultiSelection(selected);
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CatalogGrid.SelectedItem is WhfmtFormatItemVm item)
            OpenFormatsRequested?.Invoke(this, [item.ResourceKey ?? item.FilePath ?? item.Name]);
    }

    // ------------------------------------------------------------------
    // VM events
    // ------------------------------------------------------------------

    private void OnVmAddFormatRequested(object? sender, EventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title           = "Add Format Definition",
            Filter          = "Whfmt definitions (*.whfmt)|*.whfmt",
            Multiselect     = false,
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
            OpenFormatsRequested?.Invoke(this, [dlg.FileName]);
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WhfmtCatalogViewModel.SelectedCount)
                           or nameof(WhfmtCatalogViewModel.TotalCount))
            RefreshStatusBarItems();
    }
}

file sealed class LambdaCmd(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
