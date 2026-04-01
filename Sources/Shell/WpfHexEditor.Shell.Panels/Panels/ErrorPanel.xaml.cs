//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Shell.Panels.Panels;

/// <summary>
/// VS2026-style error panel that aggregates <see cref="DiagnosticEntry"/> instances
/// from one or more <see cref="IDiagnosticSource"/> providers and displays them in a
/// filterable, sortable list. Implements <see cref="IErrorPanel"/>.
/// </summary>
public partial class ErrorPanel : UserControl, IErrorPanel
{
    // -- Sources --------------------------------------------------------------
    private readonly List<IDiagnosticSource> _sources = [];
    private readonly ObservableCollection<DiagnosticEntryVm> _allEntries = [];

    // -- View -----------------------------------------------------------------
    private readonly CollectionViewSource _viewSource = new();
    private string _sortColumn = string.Empty;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    // -- IErrorPanel ----------------------------------------------------------
    private ErrorPanelScope _scope = ErrorPanelScope.Solution;

    public ErrorPanelScope Scope
    {
        get => _scope;
        set { _scope = value; SyncScopeCombo(); ApplyFilter(); }
    }

    /// <summary>Name of the currently active project; used for <see cref="ErrorPanelScope.CurrentProject"/> filtering.</summary>
    public string CurrentProjectName  { get; set; } = string.Empty;

    /// <summary>Absolute path of the currently active document; used for <see cref="ErrorPanelScope.CurrentDocument"/> filtering.</summary>
    public string CurrentDocumentPath { get; set; } = string.Empty;

    private readonly HashSet<string> _openDocumentPaths    = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _changedDocumentPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Re-applies the current filter. Call when <see cref="CurrentProjectName"/> or <see cref="CurrentDocumentPath"/> change.</summary>
    public void RefreshFilter() => ApplyFilter();

    public void SetOpenDocuments(IReadOnlyCollection<string> paths)
    {
        _openDocumentPaths.Clear();
        foreach (var p in paths) _openDocumentPaths.Add(p);
        ApplyFilter();
    }

    public void SetChangedDocuments(IReadOnlyCollection<string> paths)
    {
        _changedDocumentPaths.Clear();
        foreach (var p in paths) _changedDocumentPaths.Add(p);
        ApplyFilter();
    }

    public event EventHandler<DiagnosticEntry>? EntryNavigationRequested;

    /// <summary>
    /// Raised when the user chooses "Open in Text Editor" from the context menu.
    /// The host should open the file in the built-in text editor and navigate to
    /// <see cref="DiagnosticEntry.Line"/>/<see cref="DiagnosticEntry.Column"/>.
    /// </summary>
    public event EventHandler<DiagnosticEntry>? OpenInTextEditorRequested;

    private ToolbarOverflowManager? _overflowManager;

    // -- Ctor -----------------------------------------------------------------
    public ErrorPanel()
    {
        InitializeComponent();

        _viewSource.Source = _allEntries;
        _viewSource.Filter += OnViewFilter;

        EntryList.ItemsSource = _viewSource.View;

        ScopeCombo.SelectedIndex = 0;

        _overflowManager = new ToolbarOverflowManager(
            toolbarContainer:      ToolbarBorder,
            alwaysVisiblePanel:    ToolbarRightPanel,
            overflowButton:        OverflowButton,
            overflowMenu:          OverflowMenu,
            groupsInCollapseOrder: [TbgFilters],
            leftFixedElements:     [ToolbarLeftPanel]);
        Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
    }

    // ------------------------------------------------------------------------
    // IErrorPanel implementation
    // ------------------------------------------------------------------------

    public void AddSource(IDiagnosticSource source)
    {
        if (_sources.Contains(source)) return;
        _sources.Add(source);
        source.DiagnosticsChanged += OnSourceChanged;
        Rebuild();
    }

    public void RemoveSource(IDiagnosticSource source)
    {
        if (!_sources.Remove(source)) return;
        source.DiagnosticsChanged -= OnSourceChanged;
        // Remove entries that belonged to this source
        var toRemove = _allEntries.Where(v => v.Source == source).ToList();
        foreach (var e in toRemove) _allEntries.Remove(e);
        UpdateCounts();
    }

    public void ClearAll()
    {
        foreach (var src in _sources)
            src.DiagnosticsChanged -= OnSourceChanged;
        _sources.Clear();
        _allEntries.Clear();
        UpdateCounts();
    }

    // ------------------------------------------------------------------------
    // Source change handling
    // ------------------------------------------------------------------------

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        if (sender is IDiagnosticSource src)
            Dispatcher.BeginInvoke(() => RefreshSource(src));
    }

    private void Rebuild()
    {
        _allEntries.Clear();
        foreach (var src in _sources)
        {
            foreach (var entry in src.GetDiagnostics())
                _allEntries.Add(new DiagnosticEntryVm(entry, src));
        }
        UpdateCounts();
        _viewSource.View?.Refresh();
    }

    private void RefreshSource(IDiagnosticSource src)
    {
        // Remove old entries from this source
        var toRemove = _allEntries.Where(v => v.Source == src).ToList();
        foreach (var e in toRemove) _allEntries.Remove(e);

        // Add fresh entries
        foreach (var entry in src.GetDiagnostics())
            _allEntries.Add(new DiagnosticEntryVm(entry, src));

        UpdateCounts();
        _viewSource.View?.Refresh();
    }

    // ------------------------------------------------------------------------
    // Filter
    // ------------------------------------------------------------------------

    private void OnViewFilter(object sender, FilterEventArgs e)
    {
        if (e.Item is not DiagnosticEntryVm vm)
        {
            e.Accepted = false;
            return;
        }

        var entry = vm.Entry;

        // Severity filter
        if (entry.Severity == DiagnosticSeverity.Error   && ErrorToggle.IsChecked   != true) { e.Accepted = false; return; }
        if (entry.Severity == DiagnosticSeverity.Warning && WarningToggle.IsChecked != true) { e.Accepted = false; return; }
        if (entry.Severity == DiagnosticSeverity.Message && MessageToggle.IsChecked != true) { e.Accepted = false; return; }

        // Search text filter
        var search = SearchBox.Text.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var contains =
                (entry.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)        ||
                (entry.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                (entry.FileName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)    ||
                (entry.ProjectName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
            if (!contains) { e.Accepted = false; return; }
        }

        // Scope filter
        if (_scope == ErrorPanelScope.CurrentProject && !string.IsNullOrEmpty(CurrentProjectName))
        {
            if (!string.Equals(entry.ProjectName, CurrentProjectName, StringComparison.OrdinalIgnoreCase))
            { e.Accepted = false; return; }
        }
        else if (_scope == ErrorPanelScope.CurrentDocument && !string.IsNullOrEmpty(CurrentDocumentPath))
        {
            var matchPath = !string.IsNullOrEmpty(entry.FilePath) &&
                            string.Equals(entry.FilePath, CurrentDocumentPath, StringComparison.OrdinalIgnoreCase);
            var matchName = !matchPath &&
                            !string.IsNullOrEmpty(entry.FileName) &&
                            string.Equals(entry.FileName, System.IO.Path.GetFileName(CurrentDocumentPath), StringComparison.OrdinalIgnoreCase);
            if (!matchPath && !matchName) { e.Accepted = false; return; }
        }
        else if (_scope == ErrorPanelScope.OpenDocuments)
        {
            if (!MatchesPathSet(entry, _openDocumentPaths)) { e.Accepted = false; return; }
        }
        else if (_scope == ErrorPanelScope.ChangedDocuments)
        {
            if (!MatchesPathSet(entry, _changedDocumentPaths)) { e.Accepted = false; return; }
        }

        e.Accepted = true;
    }

    private static bool MatchesPathSet(DiagnosticEntry entry, HashSet<string> paths)
    {
        if (paths.Count == 0) return false;
        if (!string.IsNullOrEmpty(entry.FilePath) && paths.Contains(entry.FilePath))
            return true;
        if (!string.IsNullOrEmpty(entry.FileName))
            return paths.Any(p => string.Equals(
                System.IO.Path.GetFileName(p), entry.FileName,
                StringComparison.OrdinalIgnoreCase));
        return false;
    }

    private void ApplyFilter() => _viewSource.View?.Refresh();

    // ------------------------------------------------------------------------
    // Counts
    // ------------------------------------------------------------------------

    private void UpdateCounts()
    {
        int errors   = _allEntries.Count(v => v.Entry.Severity == DiagnosticSeverity.Error);
        int warnings = _allEntries.Count(v => v.Entry.Severity == DiagnosticSeverity.Warning);
        int messages = _allEntries.Count(v => v.Entry.Severity == DiagnosticSeverity.Message);

        ErrorCountText.Text   = errors   == 1 ? "1 Error"   : $"{errors} Errors";
        WarningCountText.Text = warnings == 1 ? "1 Warning" : $"{warnings} Warnings";
        MessageCountText.Text = messages == 1 ? "1 Message" : $"{messages} Messages";
    }

    // ------------------------------------------------------------------------
    // Event handlers
    // ------------------------------------------------------------------------

    // -- Toolbar overflow -----------------------------------------------------

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowMenu.PlacementTarget = OverflowButton;
        OverflowMenu.Placement       = PlacementMode.Bottom;
        OverflowMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
        => _overflowManager?.SyncMenuVisibility();

    private void OnOvfErrorToggle(object sender, RoutedEventArgs e)
    {
        ErrorToggle.IsChecked = !(ErrorToggle.IsChecked == true);
        ApplyFilter();
    }

    private void OnOvfWarningToggle(object sender, RoutedEventArgs e)
    {
        WarningToggle.IsChecked = !(WarningToggle.IsChecked == true);
        ApplyFilter();
    }

    private void OnOvfMessageToggle(object sender, RoutedEventArgs e)
    {
        MessageToggle.IsChecked = !(MessageToggle.IsChecked == true);
        ApplyFilter();
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScopeCombo.SelectedItem is ComboBoxItem item)
        {
            _scope = item.Tag?.ToString() switch
            {
                "CurrentProject"  => ErrorPanelScope.CurrentProject,
                "CurrentDocument" => ErrorPanelScope.CurrentDocument,
                "OpenDocuments"   => ErrorPanelScope.OpenDocuments,
                "ChangedDocuments"=> ErrorPanelScope.ChangedDocuments,
                _                 => ErrorPanelScope.Solution
            };
        }
    }

    private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? dep = e.OriginalSource as DependencyObject;
        while (dep is not null && dep is not ListViewItem)
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        if (dep is ListViewItem lvi && lvi.DataContext is DiagnosticEntryVm vm)
            EntryNavigationRequested?.Invoke(this, vm.Entry);
    }

    private void OnEntryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && EntryList.SelectedItem is DiagnosticEntryVm vm)
        {
            EntryNavigationRequested?.Invoke(this, vm.Entry);
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopySelectedEntry();
            e.Handled = true;
        }
    }

    // -- Context menu ----------------------------------------------------------

    private void OnErrListContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Walk up the visual tree to find the ListViewItem that was right-clicked.
        DependencyObject? dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not ListViewItem)
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);

        if (dep is not ListViewItem lvi || lvi.DataContext is not DiagnosticEntryVm vm)
        {
            e.Handled = true;
            return;
        }

        var entry = vm.Entry;
        var cm    = new ContextMenu();

        AddCtxItem(cm, "Open with Default Editor", () => EntryNavigationRequested?.Invoke(this, entry));
        AddCtxItem(cm, "Open in Text Editor",       () => OpenInTextEditorRequested?.Invoke(this, entry));
        cm.Items.Add(new Separator());
        AddCtxItem(cm, "Copy Message", () =>
        {
            var text = string.Join("\t",
                entry.Severity,
                entry.Code,
                entry.Description,
                entry.FileName ?? "",
                entry.Line.HasValue ? $"(line {entry.Line})" : "");
            try { Clipboard.SetText(text); } catch { }
        });

        lvi.ContextMenu = cm;
    }

    private static void AddCtxItem(ContextMenu cm, string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        cm.Items.Add(item);
    }

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header || header.Tag is not string col)
            return;

        if (_sortColumn == col)
            _sortDirection = _sortDirection == ListSortDirection.Ascending
                           ? ListSortDirection.Descending
                           : ListSortDirection.Ascending;
        else
        {
            _sortColumn    = col;
            _sortDirection = ListSortDirection.Ascending;
        }

        _viewSource.SortDescriptions.Clear();
        _viewSource.SortDescriptions.Add(new SortDescription($"Entry.{col}", _sortDirection));
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private void CopySelectedEntry()
    {
        if (EntryList.SelectedItem is not DiagnosticEntryVm vm) return;
        var e = vm.Entry;
        var text = string.Join("\t",
            e.Severity,
            e.Code,
            e.Description,
            e.ProjectName ?? "",
            e.FileName    ?? "",
            e.Offset.HasValue ? $"0x{e.Offset:X8}" : "",
            (e.Line.HasValue && e.Column.HasValue) ? $"{e.Line}:{e.Column}" : "");
        Clipboard.SetText(text);
    }

    private void SyncScopeCombo()
    {
        int idx = _scope switch
        {
            ErrorPanelScope.CurrentProject  => 1,
            ErrorPanelScope.CurrentDocument => 2,
            ErrorPanelScope.OpenDocuments   => 3,
            ErrorPanelScope.ChangedDocuments=> 4,
            _                               => 0
        };
        if (ScopeCombo.SelectedIndex != idx)
            ScopeCombo.SelectedIndex = idx;
    }

    // -- Nested ViewModel -----------------------------------------------------

    /// <summary>
    /// Wraps a <see cref="DiagnosticEntry"/> with its originating source for tracking.
    /// </summary>
    private sealed class DiagnosticEntryVm
    {
        public DiagnosticEntry     Entry  { get; }
        public IDiagnosticSource   Source { get; }

        /// <summary>
        /// Formatted offset string for binding (e.g. "0x00001A3F").
        /// </summary>
        public string OffsetDisplay  => Entry.Offset.HasValue  ? $"0x{Entry.Offset.Value:X8}" : string.Empty;

        /// <summary>
        /// Formatted line:col string for binding (e.g. "12:5").
        /// </summary>
        public string LineColDisplay => (Entry.Line.HasValue && Entry.Column.HasValue)
                                      ? $"{Entry.Line} / {Entry.Column}"
                                      : Entry.Line.HasValue ? $"{Entry.Line}" : string.Empty;

        public DiagnosticEntryVm(DiagnosticEntry entry, IDiagnosticSource source)
        {
            Entry  = entry;
            Source = source;
        }
    }
}
