// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Layout/ILayoutStrategy.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Strategy interface for pluggable class diagram layout algorithms.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Layout;

/// <summary>
/// Computes canvas positions for all nodes in a diagram document.
/// Implementations write X/Y/Width/Height directly onto each <see cref="ClassNode"/>.
/// </summary>
public interface ILayoutStrategy
{
    /// <summary>
    /// Applies the layout algorithm to all nodes in <paramref name="doc"/> in-place.
    /// </summary>
    void Layout(DiagramDocument doc, LayoutOptions? options = null);
}
