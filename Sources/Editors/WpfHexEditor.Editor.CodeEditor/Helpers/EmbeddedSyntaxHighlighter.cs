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

using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Helpers;

/// <summary>
/// Syntax highlighter that splits a line into host-language and
/// embedded-language segments, dispatching each segment to the appropriate
/// <see cref="ISyntaxHighlighter"/> delegate.
/// </summary>
public sealed class EmbeddedSyntaxHighlighter : ISyntaxHighlighter
{
    // _host      — used by the background highlight pipeline (thread: pipeline Task.Run)
    // _uiHost    — separate instance for the UI-thread render path (state-tracking calls in
    //              OnRender / fast-path continuations). Two instances share no mutable state,
    //              so _inBlockComment advances independently and there is no data race.
    private readonly ISyntaxHighlighter                              _host;
    private readonly ISyntaxHighlighter                              _uiHost;
    private readonly IReadOnlyList<EmbeddedLanguageZone>            _zones;
    private readonly Dictionary<string, ISyntaxHighlighter>         _subHighlighters = [];
    private readonly Func<LanguageDefinition, ISyntaxHighlighter>   _factory;
    // Guards _subHighlighters — pipeline bg thread and potential concurrent Highlight() calls.
    private readonly object _subLock = new();

    // Immutable snapshot of the full-text analysis result.
    // SetFullText (UI thread) builds a new instance and publishes it via Volatile.Write.
    // Highlight / FindOverlapping (pipeline bg thread) reads it via Volatile.Read.
    // Single-reference swap is the lightest thread-safe pattern — no lock on the hot path.
    private sealed class TextSnapshot(int[] lineOffsets, int fullTextLength, IReadOnlyList<EmbeddedRange> ranges)
    {
        public readonly int[]                       LineOffsets    = lineOffsets;
        public readonly int                         FullTextLength = fullTextLength;
        public readonly IReadOnlyList<EmbeddedRange> Ranges        = ranges;
    }

    private static readonly TextSnapshot _emptySnapshot = new([], 0, []);
    private TextSnapshot _snapshot = _emptySnapshot;

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
        ISyntaxHighlighter                           uiHost,
        IReadOnlyList<EmbeddedLanguageZone>          zones,
        Func<LanguageDefinition, ISyntaxHighlighter> highlighterFactory)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(uiHost);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(highlighterFactory);
        _host    = host;
        _uiHost  = uiHost;
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
    internal void SetFullText(string fullText)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            Volatile.Write(ref _snapshot, _emptySnapshot);
            return;
        }

        // Build line-start offsets (full-text coords, including line endings).
        var offsets = new System.Collections.Generic.List<int> { 0 };
        for (int i = 0; i < fullText.Length; i++)
        {
            if (fullText[i] == '\n' && i + 1 < fullText.Length)
                offsets.Add(i + 1);
        }

        var ranges = EmbeddedRangeClassifier.ClassifyRanges(fullText, _zones);

        // Publish atomically — bg thread always sees a consistent (offsets, length, ranges) triple.
        Volatile.Write(ref _snapshot, new TextSnapshot(offsets.ToArray(), fullText.Length, ranges));
    }

    // ── UI-thread render path ────────────────────────────────────────────────
    // The OnRender path calls Highlight() for state-tracking continuity across lines.
    // These must use _uiHost (not _host) to avoid racing with the pipeline bg thread.

    /// <summary>Resets the UI-thread block-comment state (called at render-frame start).</summary>
    internal void ResetForUiThread() => _uiHost.Reset();

    /// <summary>
    /// Highlights <paramref name="lineText"/> on the UI thread.
    /// Uses the UI-thread-exclusive <c>_uiHost</c> so block-comment state never races
    /// with the background pipeline's <c>_host</c>.
    /// </summary>
    internal IReadOnlyList<SyntaxHighlightToken> HighlightForUiThread(string lineText, int lineIndex)
    {
        var snap = Volatile.Read(ref _snapshot);
        if (snap.Ranges.Count == 0 || lineIndex >= snap.LineOffsets.Length)
            return _uiHost.Highlight(lineText, lineIndex);

        int lineStart = snap.LineOffsets[lineIndex];
        int lineEnd   = lineIndex + 1 < snap.LineOffsets.Length
            ? snap.LineOffsets[lineIndex + 1]
            : snap.FullTextLength;

        var overlapping = FindOverlapping(snap, lineStart, lineEnd);
        return overlapping.Count == 0
            ? _uiHost.Highlight(lineText, lineIndex)
            : BuildTokens(lineText, lineStart, overlapping, _uiHost);
    }

    // ── ISyntaxHighlighter ───────────────────────────────────────────────────

    /// <inheritdoc />
    public IReadOnlyList<SyntaxHighlightToken> Highlight(string lineText, int lineIndex)
    {
        if (string.IsNullOrEmpty(lineText)) return [];

        // Single volatile read — guaranteed consistent (offsets + ranges built together).
        var snap = Volatile.Read(ref _snapshot);

        // No ranges or line index out of range → fall back to host highlighter.
        if (snap.Ranges.Count == 0 || lineIndex >= snap.LineOffsets.Length)
            return _host.Highlight(lineText, lineIndex);

        int lineStart = snap.LineOffsets[lineIndex];
        // Full-text lineEnd: start of next line (includes \r\n), or EOF.
        int lineEnd = lineIndex + 1 < snap.LineOffsets.Length
            ? snap.LineOffsets[lineIndex + 1]
            : snap.FullTextLength;

        // Find ranges that overlap this line.
        var overlapping = FindOverlapping(snap, lineStart, lineEnd);
        if (overlapping.Count == 0)
            return _host.Highlight(lineText, lineIndex);

        // Split line into segments and dispatch (pipeline path uses _host).
        return BuildTokens(lineText, lineStart, overlapping, _host);
    }

    /// <inheritdoc />
    public void Reset()
    {
        // Reset only the host highlighter — it tracks block-comment state across full document lines.
        // Sub-highlighters are reset immediately before each Highlight() call in BuildTokens(),
        // because they receive isolated line slices (not the full document) and must start
        // from a clean state each time.
        _host.Reset();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IReadOnlyList<EmbeddedRange> FindOverlapping(TextSnapshot snap, int lineStart, int lineEnd)
    {
        var ranges = snap.Ranges;
        // Binary search to skip ranges that end before this line.
        int lo = 0, hi = ranges.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (ranges[mid].ContentEnd <= lineStart) lo = mid + 1;
            else hi = mid;
        }

        var result = new System.Collections.Generic.List<EmbeddedRange>();
        for (int i = lo; i < ranges.Count; i++)
        {
            var r = ranges[i];
            if (r.ContentStart >= lineEnd) break;
            result.Add(r);
        }
        return result;
    }

    private IReadOnlyList<SyntaxHighlightToken> BuildTokens(
        string lineText,
        int    lineStart,
        IReadOnlyList<EmbeddedRange> overlapping,
        ISyntaxHighlighter hostToUse)
    {
        var tokens = new System.Collections.Generic.List<SyntaxHighlightToken>();
        int cursor = 0; // position within lineText (0-based column)

        foreach (var range in overlapping)
        {
            // Start/end of embedded zone relative to this line (clamped to [0, lineText.Length]).
            // Both bounds must be clamped: ContentStart can be before lineStart (zone opened on a
            // previous line) and ContentEnd can be beyond lineText.Length (zone closes on a future
            // line, or the full-text coord includes \r\n chars absent from lineText).
            int zoneLineStart = Math.Clamp(range.ContentStart - lineStart, 0, lineText.Length);
            int zoneLineEnd   = Math.Clamp(range.ContentEnd   - lineStart, 0, lineText.Length);

            // Host segment before this zone.
            if (zoneLineStart > cursor)
                AppendShifted(tokens, hostToUse.Highlight(lineText[cursor..zoneLineStart], -1), cursor);

            // Embedded segment.
            if (zoneLineEnd > zoneLineStart)
            {
                var sub = GetOrCreateSub(range.Language);
                // Reset before each slice: sub-highlighters see isolated segments, not the full
                // document, so _inBlockComment must not carry over from a previous line's slice.
                sub.Reset();
                AppendShifted(tokens, sub.Highlight(lineText[zoneLineStart..zoneLineEnd], -1), zoneLineStart);
            }

            cursor = zoneLineEnd;
        }

        // Trailing host segment after last zone.
        if (cursor < lineText.Length)
            AppendShifted(tokens, hostToUse.Highlight(lineText[cursor..], -1), cursor);

        return tokens;
    }

    private ISyntaxHighlighter GetOrCreateSub(LanguageDefinition lang)
    {
        lock (_subLock)
        {
            if (!_subHighlighters.TryGetValue(lang.Id, out var sub))
                _subHighlighters[lang.Id] = sub = _factory(lang);
            return sub;
        }
    }

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
