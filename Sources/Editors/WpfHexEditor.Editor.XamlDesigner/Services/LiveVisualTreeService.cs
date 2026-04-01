// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: LiveVisualTreeService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19 — Sets LiveTreeNode.Parent on each child after construction
//                        to support breadcrumb navigation and full-path computation.
//                        Added BuildLogicalTree() for the Logical Tree toggle (Phase 10).
// Description:
//     Builds a tree of LiveTreeNode instances by walking the WPF visual tree
//     of the rendered design root. Provides the data source for the "Visual Tree"
//     mode of the XamlOutlinePanel.
//
// Architecture Notes:
//     Pure service — no WPF rendering side effects; reads only.
//     Recursion depth capped at 64 to guard against degenerate XAML.
// ==========================================================

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Constructs a <see cref="LiveTreeNode"/> tree from a live WPF visual tree.
/// </summary>
public sealed class LiveVisualTreeService
{
    private const int MaxDepth = 64;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a root <see cref="LiveTreeNode"/> from <paramref name="root"/>
    /// by walking the WPF <b>visual</b> tree.
    /// Returns null if <paramref name="root"/> is null.
    /// </summary>
    public LiveTreeNode? BuildTree(DependencyObject? root)
    {
        if (root is null) return null;
        return BuildNode(root, null, 0);
    }

    /// <summary>
    /// Builds a root <see cref="LiveTreeNode"/> from <paramref name="root"/>
    /// by walking the WPF <b>logical</b> tree (fewer nodes, closer to XAML source).
    /// Returns null if <paramref name="root"/> is null.
    /// </summary>
    public LiveTreeNode? BuildLogicalTree(DependencyObject? root)
    {
        if (root is null) return null;
        return BuildLogicalNode(root, null, 0);
    }

    // ── Private — visual tree ─────────────────────────────────────────────────

    private static LiveTreeNode? BuildNode(DependencyObject obj, LiveTreeNode? parent, int depth)
    {
        if (depth > MaxDepth) return null;

        var node   = new LiveTreeNode(obj) { Parent = parent };

        int childCount = VisualTreeHelper.GetChildrenCount(obj);
        for (int i = 0; i < childCount; i++)
        {
            var child     = VisualTreeHelper.GetChild(obj, i);
            var childNode = BuildNode(child, node, depth + 1);
            if (childNode is not null)
                node.Children.Add(childNode);
        }

        return node;
    }

    // ── Private — logical tree ────────────────────────────────────────────────

    private static LiveTreeNode? BuildLogicalNode(DependencyObject obj, LiveTreeNode? parent, int depth)
    {
        if (depth > MaxDepth) return null;

        var node = new LiveTreeNode(obj) { Parent = parent };

        foreach (var child in LogicalTreeHelper.GetChildren(obj).OfType<DependencyObject>())
        {
            var childNode = BuildLogicalNode(child, node, depth + 1);
            if (childNode is not null)
                node.Children.Add(childNode);
        }

        return node;
    }
}
