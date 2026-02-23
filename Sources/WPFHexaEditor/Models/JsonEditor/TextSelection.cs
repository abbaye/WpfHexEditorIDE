//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - Text Selection Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;

namespace WpfHexaEditor.Models.JsonEditor
{
    /// <summary>
    /// Represents a text selection in the editor.
    /// Handles both forward and backward selections.
    /// </summary>
    public class TextSelection
    {
        private TextPosition _start;
        private TextPosition _end;

        /// <summary>
        /// Start position of selection (anchor point where selection began)
        /// </summary>
        public TextPosition Start
        {
            get => _start;
            set
            {
                _start = value;
                UpdateNormalizedPositions();
            }
        }

        /// <summary>
        /// End position of selection (current cursor position)
        /// </summary>
        public TextPosition End
        {
            get => _end;
            set
            {
                _end = value;
                UpdateNormalizedPositions();
            }
        }

        /// <summary>
        /// Normalized start (always before end, regardless of selection direction)
        /// </summary>
        public TextPosition NormalizedStart { get; private set; }

        /// <summary>
        /// Normalized end (always after start, regardless of selection direction)
        /// </summary>
        public TextPosition NormalizedEnd { get; private set; }

        /// <summary>
        /// Check if selection is empty (start == end)
        /// </summary>
        public bool IsEmpty => _start == _end;

        /// <summary>
        /// Check if selection spans multiple lines
        /// </summary>
        public bool IsMultiLine => NormalizedStart.Line != NormalizedEnd.Line;

        /// <summary>
        /// Create empty selection at origin
        /// </summary>
        public TextSelection()
        {
            _start = TextPosition.Origin;
            _end = TextPosition.Origin;
            UpdateNormalizedPositions();
        }

        /// <summary>
        /// Create selection with specific start and end
        /// </summary>
        public TextSelection(TextPosition start, TextPosition end)
        {
            _start = start;
            _end = end;
            UpdateNormalizedPositions();
        }

        /// <summary>
        /// Clear selection (collapse to start)
        /// </summary>
        public void Clear()
        {
            _end = _start;
            UpdateNormalizedPositions();
        }

        /// <summary>
        /// Set selection range
        /// </summary>
        public void Set(TextPosition start, TextPosition end)
        {
            _start = start;
            _end = end;
            UpdateNormalizedPositions();
        }

        /// <summary>
        /// Update normalized positions (ensure start < end)
        /// </summary>
        private void UpdateNormalizedPositions()
        {
            if (_start <= _end)
            {
                NormalizedStart = _start;
                NormalizedEnd = _end;
            }
            else
            {
                NormalizedStart = _end;
                NormalizedEnd = _start;
            }
        }

        /// <summary>
        /// Check if position is within selection
        /// </summary>
        public bool Contains(TextPosition position)
        {
            return position >= NormalizedStart && position < NormalizedEnd;
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "Empty Selection";

            return $"{NormalizedStart} → {NormalizedEnd}";
        }
    }
}
