// ==========================================================
// Project: WpfHexEditor.Editor.TextEditor
// File: Selection/RectangularSelection.cs
// Author: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Pure data model for Alt+LeftClick rectangular (block/column) selection
//     for the TextEditor control.  Mirrors the CodeEditor variant but uses raw
//     int coordinates to avoid a cross-project dependency.
//
// Architecture Notes:
//     Single Responsibility — only stores and normalises selection bounds.
//     ExtractText / ApplyDelete are pure string operations.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace WpfHexEditor.Editor.TextEditor.Selection
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
        /// Starts a new rectangular selection anchored at
        /// (<paramref name="line"/>, <paramref name="col"/>).
        /// </summary>
        public void Begin(int line, int col)
        {
            _anchorLine   = line;
            _anchorColumn = col;
            _activeLine   = line;
            _activeColumn = col;
            IsEmpty       = false;
        }

        /// <summary>
        /// Extends the active corner to (<paramref name="line"/>, <paramref name="col"/>).
        /// </summary>
        public void Extend(int line, int col)
        {
            _activeLine   = line;
            _activeColumn = col;
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

        /// <summary>Returns the column range shared by all lines in the rectangle.</summary>
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
                var line  = lines[li];
                int safeL = Math.Min(left,  line.Length);
                int safeR = Math.Min(right, line.Length);

                if (li > TopLine) sb.Append('\n');
                if (safeR > safeL) sb.Append(line, safeL, safeR - safeL);
            }

            return sb.ToString();
        }
    }
}
