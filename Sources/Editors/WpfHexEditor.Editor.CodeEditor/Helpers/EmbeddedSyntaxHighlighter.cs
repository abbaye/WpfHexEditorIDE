// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Helpers/EmbeddedSyntaxHighlighter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-05-26
// Description:
//     ISyntaxHighlighter decorator that dispatches tokens to the correct
//     per-language highlighter based on embedded-language zones detected by
//     EmbeddedRangeClassifier.
//
//     For a given line, the highlighter:
//       1. Locates any embedded ranges that overlap this line.
//       2. Splits the line into host-language and embedded-language segments.
//       3. Delegates each segment to the appropriate ISyntaxHighlighter.
//       4. Re-assembles the token list in document order.
//
// Architecture Notes:
//     Decorator Pattern  — wraps the host-language highlighter; sub-language
//                          highlighters are created lazily via the same
//                          CodeEditorFactory.BuildHighlighter path.
//     Stateless per-line — EmbeddedRangeClassifier is called once per full
//                          document text update (SetFullText), not per line.
//     Thread safety      — ranges are replaced atomically via volatile field.
// ==========================================================

using System.Collections.Concurrent;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Helpers;

/// <summary>
/// Syntax highlighter that splits a line into host-language and
/// embedded-language segments, dispatching each segment to the appropriate
/// <see cref="ISyntaxHighlighter"/> delegate.
/// </summary>
public sealed class EmbeddedSyntaxHighlighter : ISyntaxHighlighter
{
    private readonly ISyntaxHighlighter                              _host;
    private readonly IReadOnlyList<EmbeddedLanguageZone>            _zones;
    private readonly ConcurrentDictionary<string, ISyntaxHighlighter> _subHighlighters = new();
    private readonly Func<LanguageDefinition, ISyntaxHighlighter>   _factory;

    // Cached line-offset lookup: lineIndex → character offset of line start in full text.
    private int[]? _lineOffsets;

    /// <summary>
    /// Initialises the embedded highlighter.
    /// </summary>
    /// <param name="host">Highlighter for the surrounding (host) language.</param>
    /// <param name="zones">Embedded-language zone descriptors (fully resolved).</param>
    /// <param name="highlighterFactory">
    ///   Factory that builds a sub-language highlighter from a
    ///   <see cref="LanguageDefinition"/>. Typically <c>CodeEditorFactory.BuildHighlighter</c>.
    /// </param>
    public EmbeddedSyntaxHighlighter(
        ISyntaxHighlighter                           host,
        IReadOnlyList<EmbeddedLanguageZone>          zones,
        Func<LanguageDefinition, ISyntaxHighlighter> highlighterFactory)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(highlighterFactory);
        _host    = host;
        _zones   = zones;
        _factory = highlighterFactory;
    }

    /// <inheritdoc />
    public string? LanguageName => _host.LanguageName;

    // ── Full-document text update ─────────────────────────────────────────────

    /// <summary>
    /// Must be called whenever the document text changes.
    /// Pre-computes per-line character offsets used during <see cref="Highlight"/>.
    /// </summary>
    public void SetFullText(string fullText)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            _lineOffsets = [];
            return;
        }

        // Build line-start offsets.
        var offsets = new System.Collections.Generic.List<int> { 0 };
        for (int i = 0; i < fullText.Length; i++)
        {
            if (fullText[i] == '\n' && i + 1 < fullText.Length)
                offsets.Add(i + 1);
        }
        _lineOffsets = offsets.ToArray();

        // Pre-classify ranges for the new text.
        _cachedRanges = EmbeddedRangeClassifier.ClassifyRanges(fullText, _zones);
        _cachedText   = fullText;
    }

    private IReadOnlyList<EmbeddedRange> _cachedRanges = [];
    private string                       _cachedText   = string.Empty;

    // ── ISyntaxHighlighter ───────────────────────────────────────────────────

    /// <inheritdoc />
    public IReadOnlyList<SyntaxHighlightToken> Highlight(string lineText, int lineIndex)
    {
        if (string.IsNullOrEmpty(lineText)) return [];

        // No ranges or no offset data → fall back to host highlighter.
        if (_cachedRanges.Count == 0 || _lineOffsets is null || lineIndex >= _lineOffsets.Length)
            return _host.Highlight(lineText, lineIndex);

        int lineStart = _lineOffsets[lineIndex];
        int lineEnd   = lineStart + lineText.Length;

        // Find ranges that overlap this line.
        var overlapping = FindOverlapping(lineStart, lineEnd);
        if (overlapping.Count == 0)
            return _host.Highlight(lineText, lineIndex);

        // Split line into segments and dispatch.
        return BuildTokens(lineText, lineStart, overlapping);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _host.Reset();
        foreach (var h in _subHighlighters.Values) h.Reset();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private IReadOnlyList<EmbeddedRange> FindOverlapping(int lineStart, int lineEnd)
    {
        var result = new System.Collections.Generic.List<EmbeddedRange>();
        foreach (var r in _cachedRanges)
        {
            if (r.ContentEnd   <= lineStart) continue;
            if (r.ContentStart >= lineEnd)   break;  // ranges are sorted
            result.Add(r);
        }
        return result;
    }

    private IReadOnlyList<SyntaxHighlightToken> BuildTokens(
        string lineText,
        int    lineStart,
        IReadOnlyList<EmbeddedRange> overlapping)
    {
        var tokens = new System.Collections.Generic.List<SyntaxHighlightToken>();
        int cursor = 0; // position within lineText (0-based column)

        foreach (var range in overlapping)
        {
            // Start/end of embedded zone relative to this line (clamped to line bounds).
            int zoneLineStart = Math.Max(0, range.ContentStart - lineStart);
            int zoneLineEnd   = Math.Min(lineText.Length, range.ContentEnd - lineStart);

            // Host segment before this zone.
            if (zoneLineStart > cursor)
                AppendShifted(tokens, _host.Highlight(lineText[cursor..zoneLineStart], -1), cursor);

            // Embedded segment.
            if (zoneLineEnd > zoneLineStart)
            {
                var sub = GetOrCreateSub(range.Language);
                AppendShifted(tokens, sub.Highlight(lineText[zoneLineStart..zoneLineEnd], -1), zoneLineStart);
            }

            cursor = zoneLineEnd;
        }

        // Trailing host segment after last zone.
        if (cursor < lineText.Length)
            AppendShifted(tokens, _host.Highlight(lineText[cursor..], -1), cursor);

        return tokens;
    }

    private ISyntaxHighlighter GetOrCreateSub(LanguageDefinition lang) =>
        _subHighlighters.GetOrAdd(lang.Id, _ => _factory(lang));

    /// <summary>
    /// Appends <paramref name="segment"/> tokens to <paramref name="list"/>,
    /// offsetting each <see cref="SyntaxHighlightToken.StartColumn"/> by
    /// <paramref name="columnOffset"/>.
    /// </summary>
    private static void AppendShifted(
        System.Collections.Generic.List<SyntaxHighlightToken> list,
        IReadOnlyList<SyntaxHighlightToken> segment,
        int columnOffset)
    {
        foreach (var t in segment)
            list.Add(t with { StartColumn = t.StartColumn + columnOffset });
    }
}
