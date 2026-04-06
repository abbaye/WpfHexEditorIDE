// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: ResourceBrowserPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19
//          2026-03-22 â€” Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.ViewModels).
// Description:
//     ViewModel for the Resource Browser dockable panel.
//     Provides a filterable, grouped view of application resources
//     and exposes commands for scanning, navigating, renaming, and copying.
//
// Architecture: Plugin-owned panel ViewModel; uses ResourceScannerService from editor core.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the Resource Browser panel.
/// </summary>
public sealed class ResourceBrowserPanelViewModel : ViewModelBase
{
    private readonly ResourceScannerService   _scanner        = new();
    private readonly ResourceUsageAnalyzer    _usageAnalyzer  = new();
    private readonly ObservableCollection<ResourceEntryViewModel> _entries = new();
    private DispatcherTimer? _debounceTimer;

    private string _filterText    = string.Empty;
    private string _xamlSource    = string.Empty;
    private string _sortMode      = "Name";
    private string _scopeFilter   = "All";
    private ResourceEntryViewModel? _selectedEntry;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ResourceBrowserPanelViewModel()
    {
        EntriesView = CollectionViewSource.GetDefaultView(_entries);
        EntriesView.Filter = FilterEntry;

        if (EntriesView.GroupDescriptions != null)
            EntriesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ResourceEntryViewModel.Scope)));

        AddResourceCommand     = new RelayCommand(_ => ExecuteAddResource());
        ScanCommand            = new RelayCommand(_ => Scan());
        FindUsagesCommand      = new RelayCommand(_ => OnFindUsages(), _ => _selectedEntry is not null);
        GoToDefinitionCommand  = new RelayCommand(p => ExecuteGoToDefinition(p as ResourceEntryViewModel), _ => _selectedEntry is not null);
        RenameCommand          = new RelayCommand(p => ExecuteRename(p as ResourceEntryViewModel));
        CopyKeyCommand         = new RelayCommand(p => ExecuteCopyKey(p as ResourceEntryViewModel));
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public ICollectionView EntriesView { get; }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value) return;
            _filterText = value;
            OnPropertyChanged();
            EntriesView.Refresh();
        }
    }

    public ResourceEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (_selectedEntry == value) return;
            _selectedEntry = value;
            OnPropertyChanged();
        }
    }

    public string XamlSource
    {
        get => _xamlSource;
        set { _xamlSource = value; ScheduleRescan(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public System.Windows.Input.ICommand AddResourceCommand    { get; }
    public System.Windows.Input.ICommand ScanCommand           { get; }
    public System.Windows.Input.ICommand FindUsagesCommand     { get; }
    public System.Windows.Input.ICommand GoToDefinitionCommand { get; }
    public System.Windows.Input.ICommand RenameCommand         { get; }
    public System.Windows.Input.ICommand CopyKeyCommand        { get; }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<string>?                        FindUsagesRequested;
    public event EventHandler<(string key, int line)>?        GoToDefinitionRequested;

    // ── Public API ────────────────────────────────────────────────────────────

    public void ScheduleRescan()
    {
        _debounceTimer?.Stop();
        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounceTimer.Tick += (_, _) => { _debounceTimer.Stop(); Scan(); };
        _debounceTimer.Start();
    }

    public string SortMode
    {
        get => _sortMode;
        set
        {
            if (_sortMode == value) return;
            _sortMode = value;
            OnPropertyChanged();
            ApplySortAndFilter();
        }
    }

    public string ScopeFilter
    {
        get => _scopeFilter;
        set
        {
            if (_scopeFilter == value) return;
            _scopeFilter = value;
            OnPropertyChanged();
            ApplySortAndFilter();
        }
    }

    public void Scan()
    {
        _entries.Clear();
        foreach (var entry in _scanner.ScanAll())
            _entries.Add(entry);

        PostProcessEntries();
        EntriesView.Refresh();
    }

    // ── Private: post-processing ──────────────────────────────────────────────

    private void PostProcessEntries()
    {
        FillUsageCounts();
        DetectDuplicates();
    }

    private void FillUsageCounts()
    {
        if (string.IsNullOrEmpty(_xamlSource)) return;

        var usages = _usageAnalyzer.AnalyzeUsages(_xamlSource);
        foreach (var entry in _entries)
            entry.UsageCount = usages.TryGetValue(entry.Key, out int count) ? count : 0;
    }

    private void DetectDuplicates()
    {
        var groups = _entries
            .GroupBy(e => e.PreviewText, StringComparer.Ordinal)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
            foreach (var entry in group)
                entry.HasDuplicate = true;
    }

    // ── Private: sort / scope ─────────────────────────────────────────────────

    private void ApplySortAndFilter()
    {
        using (EntriesView.DeferRefresh())
        {
            EntriesView.SortDescriptions.Clear();
            var sortDescription = _sortMode switch
            {
                "Type"  => new SortDescription(nameof(ResourceEntryViewModel.ValueType),  ListSortDirection.Ascending),
                "Usage" => new SortDescription(nameof(ResourceEntryViewModel.UsageCount), ListSortDirection.Descending),
                _       => new SortDescription(nameof(ResourceEntryViewModel.Key),        ListSortDirection.Ascending),
            };
            EntriesView.SortDescriptions.Add(sortDescription);

            EntriesView.Filter = FilterEntry;
        }
    }

    private void ExecuteAddResource()
    {
        var blank = new ResourceEntryViewModel("(new key)", null, "Local");
        _entries.Add(blank);
        SelectedEntry = blank;
        blank.BeginRename();
    }

    // ── Private: commands ─────────────────────────────────────────────────────

    private void ExecuteGoToDefinition(ResourceEntryViewModel? entry)
    {
        if (entry is null) return;
        GoToDefinitionRequested?.Invoke(this, (entry.Key, entry.LineNumber));
    }

    private void ExecuteRename(ResourceEntryViewModel? entry)
    {
        entry?.BeginRename();
    }

    private void ExecuteCopyKey(ResourceEntryViewModel? entry)
    {
        if (entry is null) return;
        Clipboard.SetText(entry.Key);
    }

    private void OnFindUsages()
    {
        if (_selectedEntry is not null)
            FindUsagesRequested?.Invoke(this, _selectedEntry.Key);
    }

    // ── INPC ──────────────────────────────────────────────────────────────────



    // ── Private: filter ───────────────────────────────────────────────────────

    private bool FilterEntry(object obj)
    {
        if (obj is not ResourceEntryViewModel entry) return false;

        if (_scopeFilter != "All" && entry.Scope != _scopeFilter)
            return false;

        if (string.IsNullOrWhiteSpace(_filterText)) return true;

        return entry.Key.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
            || entry.ValueType.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
    }
}
