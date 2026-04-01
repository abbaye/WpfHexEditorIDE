// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Input/DragDropState.cs
// Author: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Transient state machine for the text drag-and-drop feature (Feature B).
//     Tracks whether a drag is pending, in progress, or inactive, along with
//     the snapshotted selection bounds and the current drop target position.
//
// Architecture Notes:
//     Pattern: State object (encapsulates drag lifecycle).
//     Has no WPF UI dependency — only uses System.Windows.Point.
// ==========================================================

using System;
using System.Windows;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Input
{
    /// <summary>
    /// Phase of the text drag-and-drop state machine.
    /// </summary>
    internal enum DragPhase
    {
        /// <summary>No drag is active.</summary>
        None,

        /// <summary>
        /// Mouse is pressed inside a selection but has not yet moved beyond the
        /// drag-threshold — waiting to determine click vs. drag.
        /// </summary>
        Pending,

        /// <summary>Threshold crossed; drag-move is in progress.</summary>
        Dragging
    }

    /// <summary>
    /// Holds all transient state needed by the CodeEditor text drag-and-drop feature.
    /// </summary>
    internal sealed class DragDropState
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        /// <summary>
        /// Minimum pixel distance the mouse must move after button-down before a
        /// drag is recognised (same semantic as WPF SystemParameters drag distances).
        /// </summary>
        public const double DragThresholdPx = 4.0;

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        /// <summary>Current phase of the drag lifecycle.</summary>
        public DragPhase Phase { get; set; } = DragPhase.None;

        /// <summary>Pixel coordinate where the mouse button was pressed.</summary>
        public Point ClickPixel { get; set; }

        /// <summary>Text position of the click (inside the original selection).</summary>
        public TextPosition ClickedPosition { get; set; }

        /// <summary>
        /// Current hover position — the insertion point if the user releases now.
        /// Updated continuously in OnMouseMove during <see cref="DragPhase.Dragging"/>.
        /// </summary>
        public TextPosition DropPosition { get; set; }

        /// <summary>Normalised selection start captured at drag-start.</summary>
        public TextPosition SelectionStart { get; set; }

        /// <summary>Normalised selection end captured at drag-start.</summary>
        public TextPosition SelectionEnd   { get; set; }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="current"/> is at
        /// least <see cref="DragThresholdPx"/> pixels away from
        /// <see cref="ClickPixel"/> in either axis.
        /// </summary>
        public bool HasMovedBeyondThreshold(Point current)
            => Math.Abs(current.X - ClickPixel.X) >= DragThresholdPx
            || Math.Abs(current.Y - ClickPixel.Y) >= DragThresholdPx;

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="drop"/> falls inside
        /// the original selection (<paramref name="start"/>…<paramref name="end"/>).
        /// A drop landing on the selection boundary is also treated as "inside"
        /// to prevent a zero-distance move.
        /// </summary>
        public static bool IsDropInsideSelection(
            TextPosition drop,
            TextPosition start,
            TextPosition end)
            => drop >= start && drop <= end;

        /// <summary>Resets all state to <see cref="DragPhase.None"/>.</summary>
        public void Reset()
        {
            Phase            = DragPhase.None;
            ClickPixel       = default;
            ClickedPosition  = default;
            DropPosition     = default;
            SelectionStart   = default;
            SelectionEnd     = default;
        }
    }
}
