// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlOutlinePanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     ViewModel for the XAML Outline dockable panel.
//     Rebuilds the element tree when the design canvas parses new XAML.
//
// Architecture Notes:
//     INPC. Tree rebuild is synchronous (XElement is already parsed).
//     SelectNodeByPath used for persistence restore.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the XAML Outline panel.
/// </summary>
public sealed class XamlOutlinePanelViewModel : INotifyPropertyChanged
{
    private XamlOutlineNode? _selectedNode;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Root nodes of the outline tree (typically 0 or 1 entries).</summary>
    public ObservableCollection<XamlOutlineNode> RootNodes { get; } = new();

    /// <summary>Currently selected node; null when nothing is selected.</summary>
    public XamlOutlineNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (ReferenceEquals(_selectedNode, value)) return;
            _selectedNode = value;
            OnPropertyChanged();
            SelectedNodeChanged?.Invoke(this, value);
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the selected node changes.</summary>
    public event EventHandler<XamlOutlineNode?>? SelectedNodeChanged;

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the outline tree from the provided root XElement.
    /// Pass null to show an empty tree.
    /// </summary>
    public void RebuildTree(XElement? root)
    {
        RootNodes.Clear();
        _selectedNode = null;
        OnPropertyChanged(nameof(SelectedNode));

        if (root is null) return;

        RootNodes.Add(new XamlOutlineNode(root));
        // Auto-expand the root node.
        if (RootNodes.Count > 0)
            RootNodes[0].IsExpanded = true;
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

    // ── Private ───────────────────────────────────────────────────────────────

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
