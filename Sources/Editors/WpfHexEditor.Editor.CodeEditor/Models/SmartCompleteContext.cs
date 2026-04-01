//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Custom CodeEditor - SmartComplete Context Model (Phase 4)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.CodeEditor.Models
{
    /// <summary>
    /// Context information for SmartComplete suggestion generation.
    /// Contains document text, cursor position, and current line for analysis.
    /// </summary>
    public class SmartCompleteContext
    {
        /// <summary>
        /// Full document text
        /// </summary>
        public string DocumentText { get; set; }

        /// <summary>
        /// Current cursor position
        /// </summary>
        public TextPosition CursorPosition { get; set; }

        /// <summary>
        /// Text of current line
        /// </summary>
        public string CurrentLine { get; set; }

        /// <summary>
        /// Character that triggered this completion session (e.g. '.' for member access, null for Ctrl+Space).
        /// Forwarded to the LSP server as triggerKind/triggerCharacter.
        /// </summary>
        public char? TriggerCharacter { get; set; }

        /// <summary>
        /// Character before cursor (for trigger detection)
        /// </summary>
        public char? PreviousChar
        {
            get
            {
                if (string.IsNullOrEmpty(CurrentLine) || CursorPosition.Column == 0)
                    return null;

                int index = CursorPosition.Column - 1;
                if (index >= 0 && index < CurrentLine.Length)
                    return CurrentLine[index];

                return null;
            }
        }

        /// <summary>
        /// Word before cursor (for filtering suggestions)
        /// </summary>
        public string WordBeforeCursor
        {
            get
            {
                if (string.IsNullOrEmpty(CurrentLine) || CursorPosition.Column == 0)
                    return string.Empty;

                int start = CursorPosition.Column - 1;

                // Find word start
                while (start >= 0 && IsWordChar(CurrentLine[start]))
                {
                    start--;
                }

                start++;

                if (start >= CursorPosition.Column)
                    return string.Empty;

                return CurrentLine.Substring(start, CursorPosition.Column - start);
            }
        }

        private bool IsWordChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_';
        }
    }
}
