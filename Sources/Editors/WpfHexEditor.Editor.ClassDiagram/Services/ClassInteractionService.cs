// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/ClassInteractionService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Orchestrates drag-move interactions on class nodes, applying
//     snapping via ClassSnapEngineService and recording undo entries
//     via ClassDiagramUndoManager.
//
// Architecture Notes:
//     Pattern: Command — move operations are committed as
//     SingleClassDiagramUndoEntry with captured before/after positions.
//     Capture of start position on BeginMove enables atomic undo.
//     CancelMove restores the original position without touching the undo stack.
// ==========================================================

using System.Windows;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>
/// Manages the lifecycle of a class node drag-move interaction:
/// begin → update (with snap) → commit/cancel.
/// </summary>
public sealed class ClassInteractionService
{
    private readonly ClassDiagramUndoManager _undoManager;
    private readonly ClassSnapEngineService _snap;

    // Captured state at the start of a move operation
    private ClassNode? _movingNode;
    private double _startX;
    private double _startY;

    public ClassInteractionService(ClassDiagramUndoManager undoManager, ClassSnapEngineService snap)
    {
        _undoManager = undoManager;
        _snap = snap;
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    /// <summary>Fired after a move operation is successfully committed to the undo stack.</summary>
    public event EventHandler? OperationCommitted;

    // ---------------------------------------------------------------------------
    // Move lifecycle
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Begins tracking a move operation for <paramref name="node"/>.
    /// Captures the starting position for later undo.
    /// </summary>
    public void BeginMove(ClassNode node, double startX, double startY)
    {
        _movingNode = node;
        _startX = startX;
        _startY = startY;
    }

    /// <summary>
    /// Updates the node's position during a drag, applying snap constraints.
    /// </summary>
    /// <param name="node">Node being moved.</param>
    /// <param name="newX">Raw new X position.</param>
    /// <param name="newY">Raw new Y position.</param>
    /// <param name="others">Peer element bounds for element-edge snapping.</param>
    public void UpdateMove(
        ClassNode node,
        double newX,
        double newY,
        IEnumerable<(double, double, double, double)> others)
    {
        Point snapped = _snap.SnapPoint(newX, newY, others);
        node.X = Math.Max(0, snapped.X);
        node.Y = Math.Max(0, snapped.Y);
    }

    /// <summary>
    /// Commits the move by creating an undo entry if the position changed.
    /// </summary>
    public void CommitMove(ClassNode node)
    {
        if (_movingNode is null) return;

        double endX = node.X;
        double endY = node.Y;
        double capturedStartX = _startX;
        double capturedStartY = _startY;

        bool positionChanged = Math.Abs(endX - capturedStartX) > 0.5
                            || Math.Abs(endY - capturedStartY) > 0.5;

        if (positionChanged)
        {
            var entry = new SingleClassDiagramUndoEntry(
                Description: $"Move {node.Name}",
                UndoAction: () => { node.X = capturedStartX; node.Y = capturedStartY; },
                RedoAction: () => { node.X = endX; node.Y = endY; });

            _undoManager.Push(entry);
            OperationCommitted?.Invoke(this, EventArgs.Empty);
        }

        _movingNode = null;
    }

    /// <summary>
    /// Cancels the move, restoring the node's original position.
    /// Does not push any undo entry.
    /// </summary>
    public void CancelMove(ClassNode node)
    {
        if (_movingNode is null) return;
        node.X = _startX;
        node.Y = _startY;
        _movingNode = null;
    }
}
