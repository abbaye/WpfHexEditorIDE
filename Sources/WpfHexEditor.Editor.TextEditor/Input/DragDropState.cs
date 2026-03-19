// ==========================================================
// Project: WpfHexEditor.Editor.TextEditor
// File: Input/DragDropState.cs
// Author: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Transient state machine for the text drag-and-drop feature in TextEditor.
//     Mirrors the CodeEditor variant but uses raw int coordinates.
//
// Architecture Notes:
//     Pattern: State object (encapsulates drag lifecycle).
//     Only depends on System.Windows.Point — no WPF controls.
// ==========================================================

using System;
using System.Windows;

namespace WpfHexEditor.Editor.TextEditor.Input
{
    /// <summary>Phase of the text drag-and-drop state machine.</summary>
    internal enum DragPhase
    {
        /// <summary>No drag is active.</summary>
        None,

        /// <summary>
        /// Mouse is pressed inside a selection but has not yet moved beyond the
        /// threshold — waiting to determine click vs. drag.
        /// </summary>
        Pending,

        /// <summary>Threshold crossed; drag-move is in progress.</summary>
        Dragging
    }

    /// <summary>
    /// Holds all transient state needed by the TextEditor text drag-and-drop feature.
    /// </summary>
    internal sealed class DragDropState
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        /// <summary>
        /// Minimum pixel distance the mouse must move after button-down before a
        /// drag is recognised.
        /// </summary>
        public const double DragThresholdPx = 4.0;

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        /// <summary>Current phase of the drag lifecycle.</summary>
        public DragPhase Phase { get; set; } = DragPhase.None;

        /// <summary>Pixel coordinate where the mouse button was pressed.</summary>
        public Point ClickPixel { get; set; }

        /// <summary>Line of the click position (inside the original selection).</summary>
        public int ClickedLine { get; set; }

        /// <summary>Column of the click position (inside the original selection).</summary>
        public int ClickedCol { get; set; }

        /// <summary>Current drop-target line — updated on every mouse-move while dragging.</summary>
        public int DropLine { get; set; }

        /// <summary>Current drop-target column — updated on every mouse-move while dragging.</summary>
        public int DropCol { get; set; }

        /// <summary>Normalised selection start line captured at drag-start.</summary>
        public int SnapshotStartLine { get; set; }

        /// <summary>Normalised selection start column captured at drag-start.</summary>
        public int SnapshotStartCol  { get; set; }

        /// <summary>Normalised selection end line captured at drag-start.</summary>
        public int SnapshotEndLine { get; set; }

        /// <summary>Normalised selection end column captured at drag-start.</summary>
        public int SnapshotEndCol  { get; set; }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="current"/> is at
        /// least <see cref="DragThresholdPx"/> pixels away from <see cref="ClickPixel"/>.
        /// </summary>
        public bool HasMovedBeyondThreshold(Point current)
            => Math.Abs(current.X - ClickPixel.X) >= DragThresholdPx
            || Math.Abs(current.Y - ClickPixel.Y) >= DragThresholdPx;

        /// <summary>
        /// Returns <see langword="true"/> if the drop target falls inside the
        /// original selection.
        /// </summary>
        public bool IsDropInsideSnapshot(int dropLine, int dropCol)
        {
            int sl = SnapshotStartLine, sc = SnapshotStartCol;
            int el = SnapshotEndLine,   ec = SnapshotEndCol;

            if (dropLine < sl || dropLine > el)     return false;
            if (dropLine == sl && dropCol < sc)      return false;
            if (dropLine == el && dropCol > ec)      return false;
            return true;
        }

        /// <summary>Resets all state to <see cref="DragPhase.None"/>.</summary>
        public void Reset()
        {
            Phase       = DragPhase.None;
            ClickPixel  = default;
            ClickedLine = 0;
            ClickedCol  = 0;
            DropLine    = 0;
            DropCol     = 0;
            SnapshotStartLine = 0;
            SnapshotStartCol  = 0;
            SnapshotEndLine   = 0;
            SnapshotEndCol    = 0;
        }
    }
}
