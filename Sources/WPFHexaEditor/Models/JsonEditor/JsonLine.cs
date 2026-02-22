//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - Line Model
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexaEditor.Models.JsonEditor
{
    /// <summary>
    /// Represents a single line of text in the JSON document.
    /// Contains text content and cached syntax highlighting tokens.
    /// </summary>
    public class JsonLine : INotifyPropertyChanged
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
        public JsonLine()
        {
        }

        /// <summary>
        /// Create line with text
        /// </summary>
        public JsonLine(string text)
        {
            _text = text ?? string.Empty;
        }

        /// <summary>
        /// Create line with text and line number
        /// </summary>
        public JsonLine(string text, int lineNumber) : this(text)
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
