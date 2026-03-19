// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlOutlinePanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-19 — Search highlight, element count, breadcrumb
// Description:
//     ViewModel for the XAML Outline dockable panel.
//     Rebuilds the element tree when the design canvas parses new XAML.
//     Exposes search filtering that dims non-matching nodes (IsMatch/DimOpacity).
//     Exposes ElementCount/ElementCountLabel and BreadcrumbItems.
//
// Architecture Notes:
//     INPC. Tree rebuild is synchronous (XElement is already parsed).
//     SelectNodeByPath used for persistence restore.
//     SearchHighlight recurses the full tree and sets IsMatch/DimOpacity per node.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Xml.Linq;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the XAML Outline panel.
/// </summary>
public sealed class XamlOutlinePanelViewModel : INotifyPropertyChanged
{
    // ── Internal state ────────────────────────────────────────────────────────

    private XamlOutlineNode? _selectedNode;
    private string           _searchText  = string.Empty;
    private int              _elementCount;
    private XElement?        _lastRoot;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Root nodes of the outline tree (typically 0 or 1 entries).</summary>
    public ObservableCollection<XamlOutlineNode> RootNodes { get; } = new();

    /// <summary>
    /// Breadcrumb path reflecting the ancestry of the currently selected node.
    /// Displayed as "Grid > StackPanel > Button" in the breadcrumb bar.
    /// </summary>
    public ObservableCollection<string> BreadcrumbItems { get; } = new();

    /// <summary>Currently selected node; null when nothing is selected.</summary>
    public XamlOutlineNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (ReferenceEquals(_selectedNode, value)) return;
            _selectedNode = value;
            OnPropertyChanged();
            RebuildBreadcrumb();
            SelectedNodeChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// Live search text. Setting this triggers <see cref="SearchHighlight"/>,
    /// which walks every node and sets its <c>IsMatch</c> / <c>DimOpacity</c>.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            SearchHighlight(value);
        }
    }

    /// <summary>Total number of nodes in the tree, recomputed on <see cref="RebuildTree"/>.</summary>
    public int ElementCount
    {
        get => _elementCount;
        private set
        {
            if (_elementCount == value) return;
            _elementCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ElementCountLabel));
        }
    }

    /// <summary>Human-readable element count label ("42 elements" or "1 element").</summary>
    public string ElementCountLabel =>
        _elementCount == 1 ? "1 element" : $"{_elementCount} elements";

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Rebuilds the outline tree from the last known XAML root.</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>Selects the parent of the currently selected node, if any.</summary>
    public ICommand SelectParentCommand { get; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the selected node changes.</summary>
    public event EventHandler<XamlOutlineNode?>? SelectedNodeChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlOutlinePanelViewModel()
    {
        RefreshCommand = new RelayCommand(_ => RebuildTree(_lastRoot));

        SelectParentCommand = new RelayCommand(
            _ =>
            {
                if (SelectedNode?.Parent is { } parent)
                    SelectedNode = parent;
            },
            _ => SelectedNode?.Parent is not null);
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the outline tree from the provided root XElement.
    /// Pass null to show an empty tree.
    /// Recomputes <see cref="ElementCount"/> after the rebuild.
    /// </summary>
    public void RebuildTree(XElement? root)
    {
        _lastRoot = root;
        RootNodes.Clear();
        _selectedNode = null;
        BreadcrumbItems.Clear();
        OnPropertyChanged(nameof(SelectedNode));

        if (root is null)
        {
            ElementCount = 0;
            return;
        }

        var rootNode = new XamlOutlineNode(root);
        RootNodes.Add(rootNode);

        // Auto-expand the root node.
        rootNode.IsExpanded = true;

        // Count elements after tree is built.
        ElementCount = CountElements(RootNodes);

        // Re-apply any active search filter.
        if (!string.IsNullOrEmpty(_searchText))
            SearchHighlight(_searchText);
    }

    /// <summary>
    /// Navigates the tree to select the node at the given path
    /// (used to restore persisted selection state).
    /// </summary>
    public void SelectNodeByPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || RootNodes.Count == 0) return;

        var found = FindByPath(RootNodes[0], path);
        if (found is not null)
            SelectedNode = found;
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private — search ──────────────────────────────────────────────────────

    /// <summary>
    /// Walks the entire node tree and sets <c>IsMatch</c> on every node.
    /// When <paramref name="text"/> is empty every node is reset to IsMatch=true.
    /// A node matches if its DisplayLabel or TagName contains the search string
    /// (ordinal, case-insensitive).
    /// </summary>
    private void SearchHighlight(string text)
    {
        foreach (var root in RootNodes)
            ApplySearchFilter(root, text);
    }

    private static bool ApplySearchFilter(XamlOutlineNode node, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            node.IsMatch = true;
            foreach (var child in node.Children)
                ApplySearchFilter(child, text);
            return true;
        }

        // Check children first (so a parent stays visible if any child matches).
        bool anyChildMatches = false;
        foreach (var child in node.Children)
        {
            if (ApplySearchFilter(child, text))
                anyChildMatches = true;
        }

        bool selfMatches =
            node.DisplayLabel.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            node.TagName.Contains(text, StringComparison.OrdinalIgnoreCase);

        node.IsMatch = selfMatches || anyChildMatches;
        return node.IsMatch;
    }

    // ── Private — element count ───────────────────────────────────────────────

    /// <summary>
    /// Recursively counts all nodes in the collection (self + all descendants).
    /// </summary>
    private static int CountElements(ObservableCollection<XamlOutlineNode> nodes)
    {
        int count = 0;
        foreach (var node in nodes)
        {
            count++;
            count += CountElements(node.Children);
        }
        return count;
    }

    // ── Private — breadcrumb ──────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the breadcrumb bar items from the selected node's ancestry path.
    /// The ElementPath format is "TagName/TagName[0]/TagName[1]" — split on '/'.
    /// </summary>
    private void RebuildBreadcrumb()
    {
        BreadcrumbItems.Clear();
        if (_selectedNode is null) return;

        // ElementPath is slash-delimited, e.g. "Grid/StackPanel[0]/Button[2]".
        // Strip index suffixes like [0] for a cleaner display.
        var segments = _selectedNode.ElementPath.Split('/');
        foreach (var seg in segments)
        {
            // Strip "[N]" index suffix.
            int bracketIdx = seg.IndexOf('[');
            BreadcrumbItems.Add(bracketIdx >= 0 ? seg[..bracketIdx] : seg);
        }
    }

    // ── Private — find ────────────────────────────────────────────────────────

    private static XamlOutlineNode? FindByPath(XamlOutlineNode node, string path)
    {
        if (node.ElementPath == path) return node;

        foreach (var child in node.Children)
        {
            var found = FindByPath(child, path);
            if (found is not null) return found;
        }

        return null;
    }
}
