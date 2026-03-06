//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom CodeEditor - Line Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.CodeEditor.Helpers;

namespace WpfHexEditor.Editor.CodeEditor.Models
{
    /// <summary>
    /// Represents a single line of text in the JSON document.
    /// Contains text content and cached syntax highlighting tokens.
    /// </summary>
    public class CodeLine : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private int _lineNumber;

        /// <summary>
        /// Text content of this line
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    IsCacheDirty = true; // Invalidate syntax cache
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Line number (0-based) in the document
        /// </summary>
        public int LineNumber
        {
            get => _lineNumber;
            set
            {
                if (_lineNumber != value)
                {
                    _lineNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Cached syntax highlighting tokens (performance optimization)
        /// Null if cache is dirty
        /// </summary>
        public List<SyntaxToken> TokensCache { get; set; }

        /// <summary>
        /// Flag indicating if syntax cache needs to be recalculated
        /// Set to true when text changes
        /// </summary>
        public bool IsCacheDirty { get; set; } = true;

        /// <summary>
        /// Last access time for LRU cache eviction (Phase 11.4)
        /// Updated when tokens are accessed or generated
        /// </summary>
        public System.DateTime LastAccessTime { get; set; } = System.DateTime.UtcNow;

        /// <summary>
        /// Length of text in characters
        /// </summary>
        public int Length => _text?.Length ?? 0;

        /// <summary>
        /// Check if line is empty
        /// </summary>
        public bool IsEmpty => string.IsNullOrEmpty(_text);

        /// <summary>
        /// Create empty line
        /// </summary>
        public CodeLine()
        {
        }

        /// <summary>
        /// Create line with text
        /// </summary>
        public CodeLine(string text)
        {
            _text = text ?? string.Empty;
        }

        /// <summary>
        /// Create line with text and line number
        /// </summary>
        public CodeLine(string text, int lineNumber) : this(text)
        {
            _lineNumber = lineNumber;
        }

        /// <summary>
        /// Invalidate syntax cache (force re-highlighting)
        /// </summary>
        public void InvalidateCache()
        {
            IsCacheDirty = true;
            TokensCache = null;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public override string ToString()
        {
            return $"Line {_lineNumber}: \"{_text}\"";
        }
    }
}
