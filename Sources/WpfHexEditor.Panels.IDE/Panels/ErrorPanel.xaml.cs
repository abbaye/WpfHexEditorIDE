//////////////////////////////////////////////
// Apache 2.0  - 2026
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
using System.Windows.Data;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Panels.IDE.Panels;

/// <summary>
/// VS2026-style error panel that aggregates <see cref="DiagnosticEntry"/> instances
/// from one or more <see cref="IDiagnosticSource"/> providers and displays them in a
/// filterable, sortable list. Implements <see cref="IErrorPanel"/>.
/// </summary>
public partial class ErrorPanel : UserControl, IErrorPanel
{
    // ── Sources ──────────────────────────────────────────────────────────────
    private readonly List<IDiagnosticSource> _sources = [];
    private readonly ObservableCollection<DiagnosticEntryVm> _allEntries = [];

    // ── View ─────────────────────────────────────────────────────────────────
    private readonly CollectionViewSource _viewSource = new();
    private string _sortColumn = string.Empty;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    // ── IErrorPanel ──────────────────────────────────────────────────────────
    private ErrorPanelScope _scope = ErrorPanelScope.Solution;

    public ErrorPanelScope Scope
    {
        get => _scope;
        set { _scope = value; SyncScopeCombo(); ApplyFilter(); }
    }

    public event EventHandler<DiagnosticEntry>? EntryNavigationRequested;

    // ── Ctor ─────────────────────────────────────────────────────────────────
    public ErrorPanel()
    {
        InitializeComponent();

        _viewSource.Source = _allEntries;
        _viewSource.Filter += OnViewFilter;

        EntryList.ItemsSource = _viewSource.View;

        ScopeCombo.SelectedIndex = 0;
    }

    // ────────────────────────────────────────────────────────────────────────
    // IErrorPanel implementation
    // ────────────────────────────────────────────────────────────────────────

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

    // ────────────────────────────────────────────────────────────────────────
    // Source change handling
    // ────────────────────────────────────────────────────────────────────────

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

    // ────────────────────────────────────────────────────────────────────────
    // Filter
    // ────────────────────────────────────────────────────────────────────────

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

        e.Accepted = true;
    }

    private void ApplyFilter() => _viewSource.View?.Refresh();

    // ────────────────────────────────────────────────────────────────────────
    // Counts
    // ────────────────────────────────────────────────────────────────────────

    private void UpdateCounts()
    {
        int errors   = _allEntries.Count(v => v.Entry.Severity == DiagnosticSeverity.Error);
        int warnings = _allEntries.Count(v => v.Entry.Severity == DiagnosticSeverity.Warning);
        int messages = _allEntries.Count(v => v.Entry.Severity == DiagnosticSeverity.Message);

        ErrorCountText.Text   = errors   == 1 ? "1 Error"   : $"{errors} Errors";
        WarningCountText.Text = warnings == 1 ? "1 Warning" : $"{warnings} Warnings";
        MessageCountText.Text = messages == 1 ? "1 Message" : $"{messages} Messages";
    }

    // ────────────────────────────────────────────────────────────────────────
    // Event handlers
    // ────────────────────────────────────────────────────────────────────────

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
                _                 => ErrorPanelScope.Solution
            };
        }
    }

    private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntryList.SelectedItem is DiagnosticEntryVm vm)
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

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

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
            _                               => 0
        };
        if (ScopeCombo.SelectedIndex != idx)
            ScopeCombo.SelectedIndex = idx;
    }

    // ── Nested ViewModel ─────────────────────────────────────────────────────

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
                                      ? $"{Entry.Line}:{Entry.Column}"
                                      : Entry.Line.HasValue ? $"{Entry.Line}" : string.Empty;

        public DiagnosticEntryVm(DiagnosticEntry entry, IDiagnosticSource source)
        {
            Entry  = entry;
            Source = source;
        }
    }
}
