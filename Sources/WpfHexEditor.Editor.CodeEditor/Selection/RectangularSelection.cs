// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Selection/RectangularSelection.cs
// Author: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Pure data model for Alt+LeftClick rectangular (block/column) selection.
//     Tracks an anchor position and an active position that together define a
//     column range × line range rectangle. Has no WPF dependencies.
//
// Architecture Notes:
//     Single Responsibility — only stores and normalises selection bounds.
//     ExtractText / ApplyDelete are pure string operations so clipboard and
//     deletion logic can be unit-tested without a running editor.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Text;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Selection
{
    /// <summary>
    /// Represents a rectangular (block/column) text selection that spans a fixed
    /// column range across a contiguous range of lines.
    /// </summary>
    internal sealed class RectangularSelection
    {
        private int _anchorLine;
        private int _anchorColumn;
        private int _activeLine;
        private int _activeColumn;

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        /// <summary>True when no selection has been started.</summary>
        public bool IsEmpty { get; private set; } = true;

        // -----------------------------------------------------------------------
        // Normalised bounds (always valid when IsEmpty == false)
        // -----------------------------------------------------------------------

        /// <summary>Top-most line index of the rectangle (inclusive).</summary>
        public int TopLine    => Math.Min(_anchorLine,   _activeLine);

        /// <summary>Bottom-most line index of the rectangle (inclusive).</summary>
        public int BottomLine => Math.Max(_anchorLine,   _activeLine);

        /// <summary>Left-most column index of the rectangle (inclusive).</summary>
        public int LeftColumn  => Math.Min(_anchorColumn, _activeColumn);

        /// <summary>Right-most column index of the rectangle (exclusive end).</summary>
        public int RightColumn => Math.Max(_anchorColumn, _activeColumn);

        // -----------------------------------------------------------------------
        // Mutation
        // -----------------------------------------------------------------------

        /// <summary>
        /// Starts a new rectangular selection anchored at <paramref name="pos"/>.
        /// </summary>
        public void Begin(TextPosition pos)
        {
            _anchorLine   = pos.Line;
            _anchorColumn = pos.Column;
            _activeLine   = pos.Line;
            _activeColumn = pos.Column;
            IsEmpty       = false;
        }

        /// <summary>
        /// Extends the active corner of the rectangle to <paramref name="pos"/>.
        /// </summary>
        public void Extend(TextPosition pos)
        {
            _activeLine   = pos.Line;
            _activeColumn = pos.Column;
            IsEmpty       = false;
        }

        /// <summary>Clears the selection.</summary>
        public void Clear()
        {
            IsEmpty = true;
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the column range shared by all lines in the rectangle.
        /// </summary>
        public (int Left, int Right) GetColumnRange() => (LeftColumn, RightColumn);

        /// <summary>
        /// Extracts the rectangular region from <paramref name="lines"/> and returns
        /// the per-line substrings joined by <c>\n</c>.  Short lines are clamped.
        /// </summary>
        public string ExtractText(IReadOnlyList<string> lines)
        {
            if (IsEmpty) return string.Empty;

            int left  = LeftColumn;
            int right = RightColumn;
            var sb    = new StringBuilder();

            for (int li = TopLine; li <= BottomLine; li++)
            {
                if (li >= lines.Count) break;
                var line   = lines[li];
                int safeL  = Math.Min(left,  line.Length);
                int safeR  = Math.Min(right, line.Length);

                if (li > TopLine) sb.Append('\n');
                if (safeR > safeL) sb.Append(line, safeL, safeR - safeL);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Deletes the rectangular region from <paramref name="lines"/> in-place
        /// (mutates each string entry).  Iterate <see cref="TopLine"/> through
        /// <see cref="BottomLine"/> bottom-to-top when calling from the editor so that
        /// line indices stay stable during batch document mutations.
        /// </summary>
        public void ApplyDelete(IList<string> lines)
        {
            if (IsEmpty) return;

            int left  = LeftColumn;
            int right = RightColumn;

            for (int li = TopLine; li <= BottomLine; li++)
            {
                if (li >= lines.Count) break;
                var line  = lines[li];
                int safeL = Math.Min(left,  line.Length);
                int safeR = Math.Min(right, line.Length);
                lines[li] = line[..safeL] + line[safeR..];
            }
        }
    }
}
