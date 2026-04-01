// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/ClassToolboxPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     ViewModel for the diagram toolbox panel.
//     Provides filtered access to ClassToolboxRegistry entries
//     with real-time search support via ICollectionView.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM) with CollectionView filtering.
//     Registry is obtained from ClassToolboxRegistry.Entries which
//     is pre-built once at startup (no heavy re-allocations).
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using WpfHexEditor.Editor.ClassDiagram.Services;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// ViewModel for the Class Diagram toolbox panel.
/// </summary>
public sealed class ClassToolboxPanelViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<ToolboxEntry> _entries;
    private string _searchText = string.Empty;

    public ClassToolboxPanelViewModel()
    {
        var registry = new ClassToolboxRegistry();
        _entries = new ObservableCollection<ToolboxEntry>(registry.Entries);

        FilteredEntries = CollectionViewSource.GetDefaultView(_entries);
        FilteredEntries.Filter = FilterEntry;
    }

    // ---------------------------------------------------------------------------
    // Collections
    // ---------------------------------------------------------------------------

    public ObservableCollection<ToolboxEntry> Entries => _entries;

    /// <summary>Filtered view used for display in the toolbox panel list.</summary>
    public ICollectionView FilteredEntries { get; }

    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            FilteredEntries.Refresh();
        }
    }

    // ---------------------------------------------------------------------------
    // Filter predicate
    // ---------------------------------------------------------------------------

    private bool FilterEntry(object obj)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        if (obj is not ToolboxEntry entry) return false;

        return entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || entry.Kind.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
