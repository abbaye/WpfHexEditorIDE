// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/DiagramSearchPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     ViewModel for the diagram search panel. Full-text search
//     across class names and member names/types in the document.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM).
//     Search is synchronous and case-insensitive; called on demand
//     via Search(). Results carry both the parent ClassNode and the
//     optional matched ClassMember for precise navigation.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// Represents a single search result pointing to a node or member.
/// </summary>
/// <param name="Node">The class node that matched (or contains the matching member).</param>
/// <param name="Member">The specific member that matched, or null if the match is the node itself.</param>
/// <param name="DisplayText">One-line display string shown in the results list.</param>
public sealed record SearchResultItem(ClassNode Node, ClassMember? Member, string DisplayText);

/// <summary>
/// ViewModel for the diagram full-text search panel.
/// </summary>
public sealed class DiagramSearchPanelViewModel : ViewModelBase
{
    private readonly ObservableCollection<SearchResultItem> _results = [];
    private readonly DispatcherTimer _debounce;
    private string _searchText = string.Empty;
    private DiagramDocument? _document;
    private SearchResultItem? _selectedResult;

    public DiagramSearchPanelViewModel()
    {
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Search(); };
    }

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
            // Incremental search: restart debounce on every keystroke
            _debounce.Stop();
            _debounce.Start();
        }
    }

    public ObservableCollection<SearchResultItem> Results => _results;

    public bool HasResults => _results.Count > 0;

    public SearchResultItem? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (_selectedResult == value) return;
            _selectedResult = value;
            OnPropertyChanged();
            ResultSelected?.Invoke(this, value);
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public event EventHandler<SearchResultItem?>? ResultSelected;

    // ---------------------------------------------------------------------------
    // Document binding
    // ---------------------------------------------------------------------------

    public void SetDocument(DiagramDocument doc)
    {
        _document = doc;
        _results.Clear();
        OnPropertyChanged(nameof(HasResults));
        SelectedResult = null;
    }

    // ---------------------------------------------------------------------------
    // Search
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Executes a search across the current document and populates <see cref="Results"/>.
    /// Searches class names and all member names/type names.
    /// </summary>
    public void Search()
    {
        _results.Clear();
        SelectedResult = null;

        if (_document is null || string.IsNullOrWhiteSpace(_searchText))
        {
            OnPropertyChanged(nameof(HasResults));
            return;
        }

        string term = _searchText.Trim();

        foreach (ClassNode node in _document.Classes)
        {
            if (node.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                _results.Add(new SearchResultItem(Node: node, Member: null, DisplayText: $"[{node.Kind}] {node.Name}"));

            foreach (ClassMember member in node.Members)
            {
                if (member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                 || member.TypeName.Contains(term, StringComparison.OrdinalIgnoreCase))
                    _results.Add(new SearchResultItem(Node: node, Member: member,
                        DisplayText: $"  [{member.Kind}] {member.DisplayLabel} in {node.Name}"));
            }
        }

        OnPropertyChanged(nameof(HasResults));
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------


}
