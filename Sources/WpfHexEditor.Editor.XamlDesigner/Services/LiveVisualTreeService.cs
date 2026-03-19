// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: LiveVisualTreeService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Builds a tree of LiveTreeNode instances by walking the WPF visual tree
//     of the rendered design root. Provides the data source for the "Visual Tree"
//     mode of the XamlOutlinePanel.
//
// Architecture Notes:
//     Pure service — no WPF rendering side effects; reads only.
//     Recursion depth capped at 64 to guard against degenerate XAML.
// ==========================================================

using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Constructs a <see cref="LiveTreeNode"/> tree from a live WPF visual tree.
/// </summary>
public sealed class LiveVisualTreeService
{
    private const int MaxDepth = 64;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a root <see cref="LiveTreeNode"/> from <paramref name="root"/>.
    /// Returns null if <paramref name="root"/> is null.
    /// </summary>
    public LiveTreeNode? BuildTree(DependencyObject? root)
    {
        if (root is null) return null;
        return BuildNode(root, 0);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static LiveTreeNode? BuildNode(DependencyObject obj, int depth)
    {
        if (depth > MaxDepth) return null;

        var node = new LiveTreeNode(obj);

        int childCount = VisualTreeHelper.GetChildrenCount(obj);
        for (int i = 0; i < childCount; i++)
        {
            var child    = VisualTreeHelper.GetChild(obj, i);
            var childNode = BuildNode(child, depth + 1);
            if (childNode is not null)
                node.Children.Add(childNode);
        }

        return node;
    }
}
