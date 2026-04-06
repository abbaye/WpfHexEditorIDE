// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/ClassOutlinePanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     ViewModel for the Class Outline panel: flat list of all nodes
//     with search/filter support. Mirrors document state on SetDocument.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM) with ICollectionView filtering.
//     Search is case-insensitive, matches on Name and Kind.
//     SelectedNode raises a separate event so the host can
//     synchronise canvas selection.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// ViewModel for the Class Outline dockable panel.
/// </summary>
public sealed class ClassOutlinePanelViewModel : ViewModelBase
{
    private readonly ObservableCollection<ClassNodeViewModel> _nodes = [];
    private ClassNodeViewModel? _selectedNode;
    private string _searchText = string.Empty;

    public ClassOutlinePanelViewModel()
    {
        FilteredNodes = CollectionViewSource.GetDefaultView(_nodes);
        FilteredNodes.Filter = FilterNode;
    }

    // ---------------------------------------------------------------------------
    // Collections
    // ---------------------------------------------------------------------------

    public ObservableCollection<ClassNodeViewModel> Nodes => _nodes;
    public ICollectionView FilteredNodes { get; }

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
            FilteredNodes.Refresh();
        }
    }

    public ClassNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode == value) return;
            _selectedNode = value;
            OnPropertyChanged();
            SelectedNodeChanged?.Invoke(this, value?.Node);
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public event EventHandler<ClassNode?>? SelectedNodeChanged;

    // ---------------------------------------------------------------------------
    // Document binding
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the node list from the given <paramref name="doc"/>.
    /// Preserves selection if the same node (by Id) is still present.
    /// </summary>
    public void SetDocument(DiagramDocument doc)
    {
        string? selectedId = _selectedNode?.Id;
        _nodes.Clear();

        foreach (ClassNode node in doc.Classes)
            _nodes.Add(new ClassNodeViewModel(node));

        // Restore selection
        if (selectedId is not null)
            SelectedNode = _nodes.FirstOrDefault(n => n.Id == selectedId);

        FilteredNodes.Refresh();
    }

    // ---------------------------------------------------------------------------
    // Private filter
    // ---------------------------------------------------------------------------

    private bool FilterNode(object obj)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        if (obj is not ClassNodeViewModel vm) return false;

        return vm.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || vm.Kind.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------


}
