//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - Text Position Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;

namespace WpfHexEditor.Editor.JsonEditor.Models
{
    /// <summary>
    /// Represents a position in the text document (line, column).
    /// Immutable value type for performance and safety.
    /// </summary>
    public struct TextPosition : IEquatable<TextPosition>, IComparable<TextPosition>
    {
        /// <summary>
        /// Line number (0-based)
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Column number (0-based)
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// Create a new text position
        /// </summary>
        public TextPosition(int line, int column)
        {
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Check if position is valid (non-negative coordinates)
        /// </summary>
        public bool IsValid => Line >= 0 && Column >= 0;

        /// <summary>
        /// Invalid position (used as sentinel value)
        /// </summary>
        public static readonly TextPosition Invalid = new TextPosition(-1, -1);

        /// <summary>
        /// Origin position (0, 0)
        /// </summary>
        public static readonly TextPosition Origin = new TextPosition(0, 0);

        #region Equality & Comparison

        public bool Equals(TextPosition other)
        {
            return Line == other.Line && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            return obj is TextPosition position && Equals(position);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Line * 397) ^ Column;
            }
        }

        public int CompareTo(TextPosition other)
        {
            int lineComparison = Line.CompareTo(other.Line);
            if (lineComparison != 0)
                return lineComparison;

            return Column.CompareTo(other.Column);
        }

        public static bool operator ==(TextPosition left, TextPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextPosition left, TextPosition right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(TextPosition left, TextPosition right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(TextPosition left, TextPosition right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(TextPosition left, TextPosition right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(TextPosition left, TextPosition right)
        {
            return left.CompareTo(right) >= 0;
        }

        #endregion

        public override string ToString()
        {
            return $"Ln {Line + 1}, Col {Column + 1}";
        }
    }
}
