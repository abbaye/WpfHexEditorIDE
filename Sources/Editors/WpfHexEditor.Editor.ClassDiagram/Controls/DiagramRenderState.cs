// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramRenderState.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Immutable snapshot of diagram canvas state passed to
//     DiagramVisualLayer for render coordination.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Immutable render state snapshot for <see cref="DiagramVisualLayer"/>.
/// </summary>
public sealed record DiagramRenderState
{
    /// <summary>The diagram document being rendered.</summary>
    public DiagramDocument? Document { get; init; }

    /// <summary>ID of the currently selected node, or null.</summary>
    public string? SelectedNodeId { get; init; }

    /// <summary>ID of the currently hovered node, or null.</summary>
    public string? HoveredNodeId { get; init; }

    /// <summary>
    /// Set of node IDs that need partial repaint.
    /// When null the entire layer is repainted.
    /// </summary>
    public IReadOnlySet<string>? DirtyNodeIds { get; init; }

    public static DiagramRenderState Empty { get; } = new();
}
