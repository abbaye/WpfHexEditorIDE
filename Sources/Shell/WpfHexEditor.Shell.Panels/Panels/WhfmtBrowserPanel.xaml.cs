// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WhfmtBrowserPanel.xaml.cs
// Description: Code-behind for the Format Browser tool window.
//              Routes UI events to WhfmtBrowserViewModel.
//              Implements IEditorToolbarContributor, IStatusBarContributor,
//              and ISearchTarget so the panel integrates with the IDE toolbar,
//              status bar, and QuickSearchBar overlay.
// Architecture: MVVM + interface adapters; no business logic here.
// ==========================================================

using System;
using System.Collections.ObjectModel;
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

public partial class WhfmtBrowserPanel : UserControl,
    IEditorToolbarContributor,
    IStatusBarContributor,
    ISearchTarget
{
    private readonly WhfmtBrowserViewModel _vm;
    private QuickSearchBar?               _searchBar;

    // Toolbar items kept as fields so we can update them at runtime
    private readonly EditorToolbarItem _tbTreeToggle;
    private readonly EditorToolbarItem _tbFlatToggle;
    private readonly EditorToolbarItem _tbShowBuiltIns;
    private readonly EditorToolbarItem _tbShowUser;
    private readonly EditorToolbarItem _tbShowFailures;
    private readonly EditorToolbarItem _tbSortDropdown;
    private readonly EditorToolbarItem _tbWatch;

    // Status bar items kept as fields for runtime updates
    private readonly StatusBarItem _sbBuiltIn;
    private readonly StatusBarItem _sbUser;
    private readonly StatusBarItem _sbFailures;
    private readonly StatusBarItem _sbSort;
    private readonly StatusBarItem _sbBusy;

    // ISearchTarget state
    private int _matchCount;
    private int _currentMatchIndex;

    public WhfmtBrowserPanel()
    {
        _vm = new WhfmtBrowserViewModel();
        _vm.OpenFormatRequested   += OnVmOpenFormatRequested;
        _vm.ExportFormatRequested += OnVmExportFormatRequested;
        _vm.ViewJsonRequested     += OnVmViewJsonRequested;
        _vm.PropertyChanged       += OnVmPropertyChanged;

        DataContext = _vm;
        InitializeComponent();

        // ── Toolbar items ──────────────────────────────────────────────────
        _tbTreeToggle   = new EditorToolbarItem { Icon = "\uE8EB", Tooltip = "Tree view — group by category", IsToggle = true, IsChecked = true,
                                                   Command = new LambdaCommand(() => { _vm.IsTreeView = true;  _tbTreeToggle.IsChecked = true;  _tbFlatToggle.IsChecked = false; }) };
        _tbFlatToggle   = new EditorToolbarItem { Icon = "\uE8FD", Tooltip = "Flat list view",                IsToggle = true, IsChecked = false,
                                                   Command = new LambdaCommand(() => { _vm.IsTreeView = false; _tbTreeToggle.IsChecked = false; _tbFlatToggle.IsChecked = true;  }) };
        _tbShowBuiltIns = new EditorToolbarItem { Icon = "\uE8C3", Tooltip = "Show built-in formats",         IsToggle = true, IsChecked = true,
                                                   Command = new LambdaCommand(() => { _vm.ShowBuiltIns = !_vm.ShowBuiltIns; _tbShowBuiltIns.IsChecked = _vm.ShowBuiltIns; RefreshStatusBarItems(); }) };
        _tbShowUser     = new EditorToolbarItem { Icon = "\uE77B", Tooltip = "Show user formats",             IsToggle = true, IsChecked = true,
                                                   Command = new LambdaCommand(() => { _vm.ShowUserFormats = !_vm.ShowUserFormats; _tbShowUser.IsChecked = _vm.ShowUserFormats; RefreshStatusBarItems(); }) };
        _tbShowFailures = new EditorToolbarItem { Icon = "\uE783", Tooltip = "Show load failures",            IsToggle = true, IsChecked = false,
                                                   Command = new LambdaCommand(() => { _vm.ToggleShowFailuresCommand.Execute(null); _tbShowFailures.IsChecked = !_tbShowFailures.IsChecked; RefreshStatusBarItems(); }) };

        var sortItems = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = "Name",     Command = new LambdaCommand(() => { _vm.CurrentWhfmtSortMode = WhfmtSortMode.ByName;     UpdateSortLabel(); }) },
            new() { Label = "Category", Command = new LambdaCommand(() => { _vm.CurrentWhfmtSortMode = WhfmtSortMode.ByCategory; UpdateSortLabel(); }) },
            new() { Label = "Quality",  Command = new LambdaCommand(() => { _vm.CurrentWhfmtSortMode = WhfmtSortMode.ByQuality;  UpdateSortLabel(); }) },
            new() { Label = "Source",   Command = new LambdaCommand(() => { _vm.CurrentWhfmtSortMode = WhfmtSortMode.BySource;   UpdateSortLabel(); }) },
        };
        _tbSortDropdown = new EditorToolbarItem { Icon = "\uE8CB", Label = "Name", Tooltip = "Sort order", DropdownItems = sortItems };

        _tbWatch = new EditorToolbarItem { Icon = "\uE91B", Tooltip = "Watch folder for hot reload", IsToggle = true,
                                           Command = new LambdaCommand(() => { _vm.ToggleWatchCommand.Execute(null); _tbWatch.IsChecked = _vm.IsWatching; }) };

        ToolbarItems =
        [
            _tbTreeToggle, _tbFlatToggle,
            new EditorToolbarItem { IsSeparator = true },
            _tbShowBuiltIns, _tbShowUser, _tbShowFailures,
            new EditorToolbarItem { IsSeparator = true },
            new EditorToolbarItem { Icon = "\uE72C", Tooltip = "Refresh catalog (F5)",  Command = new LambdaCommand(() => _vm.RefreshCommand.Execute(null)) },
            new EditorToolbarItem { Icon = "\uECC8", Tooltip = "New format (Ctrl+N)",   Command = new LambdaCommand(() => _vm.NewFormatCommand.Execute(null)) },
            new EditorToolbarItem { Icon = "\uE109", Tooltip = "Add format file…",      Command = new LambdaCommand(() => _vm.AddFormatCommand.Execute(null)) },
            new EditorToolbarItem { Icon = "\uEC50", Tooltip = "Open user formats folder", Command = new LambdaCommand(() => _vm.OpenFolderCommand.Execute(null)) },
            new EditorToolbarItem { IsSeparator = true },
            _tbWatch,
            new EditorToolbarItem { IsSeparator = true },
            _tbSortDropdown,
        ];

        // ── Status bar items ───────────────────────────────────────────────
        _sbBuiltIn  = new StatusBarItem { Label = "0 built-in" };
        _sbUser     = new StatusBarItem { Label = "0 user" };
        _sbFailures = new StatusBarItem { Label = string.Empty, IsVisible = false };
        _sbSort     = new StatusBarItem { Label = "Sort: Name" };
        _sbBusy     = new StatusBarItem { Label = string.Empty, IsVisible = false };

        StatusBarItems = [ _sbBuiltIn, _sbUser, _sbFailures, _sbBusy, _sbSort ];

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
        _sbBuiltIn.Label  = $"{_vm.BuiltInCount} built-in";
        _sbUser.Label     = $"{_vm.UserFormatCount} user";
        _sbFailures.Label = _vm.FailureCount > 0 ? $"\u26A0 {_vm.FailureCount} failed" : string.Empty;
        _sbFailures.IsVisible = _vm.FailureCount > 0;
        _sbSort.Label     = $"Sort: {_vm.WhfmtSortModeLabel}";
        _sbBusy.Label     = _vm.IsBusy ? "Loading…" : string.Empty;
        _sbBusy.IsVisible = _vm.IsBusy;
    }

    // ------------------------------------------------------------------
    // ISearchTarget
    // ------------------------------------------------------------------

    public SearchBarCapabilities Capabilities => SearchBarCapabilities.CaseSensitive | SearchBarCapabilities.Wildcard;

    public int MatchCount        => _matchCount;
    public int CurrentMatchIndex => _currentMatchIndex;

    public event EventHandler? SearchResultsChanged;

    public void Find(string query, SearchTargetOptions options = default)
    {
        _vm.IsRegexSearch = options.HasFlag(SearchTargetOptions.UseWildcard);
        _vm.SearchText    = query;
        _matchCount       = _vm.SearchMatchCount;
        _currentMatchIndex = _matchCount > 0 ? 1 : 0;
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FindNext()
    {
        if (_matchCount == 0) return;
        _currentMatchIndex = (_currentMatchIndex % _matchCount) + 1;
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FindPrevious()
    {
        if (_matchCount == 0) return;
        _currentMatchIndex = _currentMatchIndex <= 1 ? _matchCount : _currentMatchIndex - 1;
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
            _vm.ClearSearchCommand.Execute(null);
        };
        SearchBarCanvas.Children.Add(_searchBar);
        SearchBarCanvas.IsHitTestVisible = true;
    }

    // ------------------------------------------------------------------
    // Keyboard
    // ------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.F5:
                _vm.RefreshCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                ShowQuickSearchBar();
                e.Handled = true;
                break;

            case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                _vm.NewFormatCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.O when Keyboard.Modifiers == ModifierKeys.Control:
                _vm.AddFormatCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Delete:
                TryDeleteSelected();
                e.Handled = true;
                break;

            case Key.Enter:
                TryOpenSelected();
                e.Handled = true;
                break;
        }
    }

    private void ShowQuickSearchBar()
    {
        if (_searchBar is null) return;
        _searchBar.Visibility = Visibility.Visible;
        _searchBar.EnsureDefaultPosition(SearchBarCanvas);
        _searchBar.FocusSearchInput();
    }

    private void TryDeleteSelected()
    {
        var item = GetSelectedFormatItem();
        if (item is null || item.Source != FormatSource.User) return;

        var result = MessageBox.Show(Window.GetWindow(this),
            $"Delete format '{item.Name}'?", "Delete Format",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            item.DeleteCommand.Execute(null);
    }

    private void TryOpenSelected()
    {
        var item = GetSelectedFormatItem();
        if (item is not null)
            _vm.RequestOpenFormat(item, FormatOpenMode.Editable);
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public event EventHandler<FormatOpenRequest>? OpenFormatRequested;
    public event EventHandler<string>? ExportFormatRequested;
    public event EventHandler<string>? ViewJsonRequested;

    public void SetCatalog(
        IEmbeddedFormatCatalog  embCatalog,
        IFormatCatalogService   catalogSvc,
        WhfmtAdHocFormatService adHocSvc,
        WhfmtExplorerSettings   settings)
    {
        _vm.Initialize(embCatalog, catalogSvc, adHocSvc, settings,
                       a => Dispatcher.BeginInvoke(a));
    }

    // ------------------------------------------------------------------
    // TreeView events
    // ------------------------------------------------------------------

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is WhfmtFormatItemVm item)
            _vm.OnItemSelected(item);
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GetSelectedFormatItem() is { } item)
            _vm.RequestOpenFormat(item, FormatOpenMode.Editable);
    }

    // ------------------------------------------------------------------
    // ListView events
    // ------------------------------------------------------------------

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FormatList.SelectedItem is WhfmtFormatItemVm item)
            _vm.OnItemSelected(item);
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FormatList.SelectedItem is WhfmtFormatItemVm item)
            _vm.RequestOpenFormat(item, FormatOpenMode.Editable);
    }

    // ------------------------------------------------------------------
    // VM event relay
    // ------------------------------------------------------------------

    private void OnVmOpenFormatRequested(object? sender, FormatOpenRequest req)
    {
        if (req.Mode == FormatOpenMode.AddUserFormat)
        {
            var dlg = new OpenFileDialog
            {
                Title           = "Add Format Definition",
                Filter          = "Whfmt definitions (*.whfmt)|*.whfmt",
                Multiselect     = false,
                CheckFileExists = true
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) == true)
            {
                var err = _vm.AddFormatFromPath(dlg.FileName);
                if (err is not null)
                    MessageBox.Show(Window.GetWindow(this), err, "Add Format",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        if (req.Mode == FormatOpenMode.RevealFolder)
        {
            if (req.KeyOrPath is not null)
                System.Diagnostics.Process.Start("explorer.exe", $"\"{req.KeyOrPath}\"");
            return;
        }

        OpenFormatRequested?.Invoke(this, req);
    }

    private void OnVmExportFormatRequested(object? sender, string keyOrPath)
        => ExportFormatRequested?.Invoke(this, keyOrPath);

    private void OnVmViewJsonRequested(object? sender, string keyOrPath)
        => ViewJsonRequested?.Invoke(this, keyOrPath);

    // ------------------------------------------------------------------
    // VM property change → refresh status bar and toolbar state
    // ------------------------------------------------------------------

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WhfmtBrowserViewModel.BuiltInCount):
            case nameof(WhfmtBrowserViewModel.UserFormatCount):
            case nameof(WhfmtBrowserViewModel.FailureCount):
            case nameof(WhfmtBrowserViewModel.IsBusy):
            case nameof(WhfmtBrowserViewModel.WhfmtSortModeLabel):
                RefreshStatusBarItems();
                break;

            case nameof(WhfmtBrowserViewModel.IsWatching):
                _tbWatch.IsChecked = _vm.IsWatching;
                break;

            case nameof(WhfmtBrowserViewModel.SearchMatchCount):
                _matchCount = _vm.SearchMatchCount;
                SearchResultsChanged?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void UpdateSortLabel()
        => _tbSortDropdown.Label = _vm.WhfmtSortModeLabel;

    private WhfmtFormatItemVm? GetSelectedFormatItem()
        => FormatTree.SelectedItem as WhfmtFormatItemVm
        ?? FormatList.SelectedItem as WhfmtFormatItemVm;
}

/// <summary>
/// Minimal inline ICommand implementation for lambda-bound toolbar items.
/// </summary>
file sealed class LambdaCommand(Action execute) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
