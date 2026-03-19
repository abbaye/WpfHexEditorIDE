// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResourceBrowserPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ViewModel for the Resource Browser dockable panel.
//     Provides a filterable, grouped view of application resources
//     and exposes commands for scanning and navigating to usages.
//
// Architecture Notes:
//     INPC. ICollectionView grouped by Scope.
//     ResourceScannerService provides raw entries.
//     ResourceReferenceService provides usage navigation.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the Resource Browser panel.
/// </summary>
public sealed class ResourceBrowserPanelViewModel : INotifyPropertyChanged
{
    private readonly ResourceScannerService   _scanner   = new();
    private readonly ObservableCollection<ResourceEntryViewModel> _entries = new();
    private string _filterText = string.Empty;
    private ResourceEntryViewModel? _selectedEntry;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ResourceBrowserPanelViewModel()
    {
        EntriesView = CollectionViewSource.GetDefaultView(_entries);
        EntriesView.Filter = FilterEntry;

        if (EntriesView.GroupDescriptions != null)
            EntriesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ResourceEntryViewModel.Scope)));

        ScanCommand       = new RelayCommand(_ => Scan());
        FindUsagesCommand = new RelayCommand(_ => OnFindUsages(), _ => _selectedEntry is not null);
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

    // ── Commands ──────────────────────────────────────────────────────────────

    public System.Windows.Input.ICommand ScanCommand       { get; }
    public System.Windows.Input.ICommand FindUsagesCommand { get; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user clicks "Find Usages" on a resource entry.</summary>
    public event EventHandler<string>? FindUsagesRequested;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Rescans all application resources and rebuilds the view.</summary>
    public void Scan()
    {
        _entries.Clear();
        foreach (var entry in _scanner.ScanAll())
            _entries.Add(entry);
        EntriesView.Refresh();
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private ───────────────────────────────────────────────────────────────

    private bool FilterEntry(object obj)
    {
        if (obj is not ResourceEntryViewModel entry) return false;
        if (string.IsNullOrWhiteSpace(_filterText)) return true;

        return entry.Key.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
            || entry.ValueType.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
    }

    private void OnFindUsages()
    {
        if (_selectedEntry is not null)
            FindUsagesRequested?.Invoke(this, _selectedEntry.Key);
    }
}
