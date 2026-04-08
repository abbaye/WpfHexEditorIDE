// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: CodeLine.cs
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Single-line model for CodeEditor. Stores text content, syntax token
//     cache, and GlyphRun cache for zero-allocation re-renders (P1-CE-05).
//
// Architecture Notes:
//     TokensCache  â€” populated by HighlightPipelineService on a bg thread.
//     GlyphRunCache â€” pre-built GlyphRuns at (startCol*charWidth, Baseline)
//                     so render can translate instead of rebuild per-frame.
// ==========================================================

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Rendering;

namespace WpfHexEditor.Editor.CodeEditor.Models
{
    /// <summary>
    /// Represents a single line of text in the JSON document.
    /// Contains text content and cached syntax highlighting tokens.
    /// </summary>
    public class CodeLine : INotifyPropertyChanged
    {
        private string _text = string.Empty;

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
                    IsCacheDirty = true;
                    IsGlyphCacheDirty = true; // P1-CE-05: invalidate GlyphRun cache
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Cached syntax highlighting tokens produced by <see cref="Services.HighlightPipelineService"/>.
        /// Null when the cache is dirty. Set from the background highlight thread.
        /// </summary>
        public List<SyntaxHighlightToken> TokensCache { get; set; }

        /// <summary>
        /// True when <see cref="TokensCache"/> is stale and must be rebuilt.
        /// Set automatically when <see cref="Text"/> changes.
        /// </summary>
        public bool IsCacheDirty { get; set; } = true;

        // ── P1-CE-05: GlyphRun segment cache ───────────────────────────────────

        /// <summary>
        /// Pre-built GlyphRun entries for each token, positioned at
        /// (StartColumn * charWidth, Baseline) so the render loop only needs
        /// a single <c>PushTransform(x, lineTopY)</c> per line (zero extra allocs).
        /// Null when dirty.
        /// </summary>
        public List<GlyphRunEntry>? GlyphRunCache { get; set; }

        /// <summary>
        /// True when <see cref="GlyphRunCache"/> is stale and must be rebuilt.
        /// Set automatically when <see cref="Text"/> changes.
        /// </summary>
        public bool IsGlyphCacheDirty { get; set; } = true;

        /// <summary>
        /// URL hit-zones registered during the last render of this line.
        /// Cached so that GlyphRun-cache hits can still repopulate the editor's
        /// click-detection list without re-running the regex overlay pass.
        /// </summary>
        public List<(int StartCol, int EndCol, string Url, bool IsEmail)>? CachedUrlZones { get; set; }

        // ──────────────────────────────────────────────────────────────────────

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
        /// Create line with text. The <paramref name="lineNumber"/> parameter is accepted for
        /// call-site compatibility but is intentionally ignored — line indices are derived
        /// from the document's <c>Lines</c> collection at render time (O(1), no stored field).
        /// </summary>
        public CodeLine(string text, int lineNumber) : this(text) { }

        /// <summary>
        /// Invalidates both the syntax token cache and the GlyphRun segment cache.
        /// Called automatically when <see cref="Text"/> changes, or externally on
        /// font/theme changes that make cached GlyphRuns stale.
        /// </summary>
        public void InvalidateCache()
        {
            IsCacheDirty      = true;
            TokensCache       = null;
            IsGlyphCacheDirty = true;   // P1-CE-05
            GlyphRunCache     = null;
            CachedUrlZones    = null;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));


        #endregion

        public override string ToString()
        {
            return $"\"{_text}\"";
        }
    }
}
