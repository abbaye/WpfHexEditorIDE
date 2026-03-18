// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: RegexBasedSyntaxHighlighter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     ISyntaxHighlighter adapter that drives the existing RegexSyntaxHighlighter
//     from WpfHexEditor.Editor.TextEditor using a SyntaxDefinition (.whlang file).
//     Converts ColoredSpan (ColorKey-based) to SyntaxHighlightToken (Brush-based).
//
// Architecture Notes:
//     Adapter Pattern — wraps TextEditor.RegexSyntaxHighlighter behind ISyntaxHighlighter.
//     Color resolution uses WPF TryFindResource on the host visual, falling back to a
//     static default-brush table so the highlighter works even before the control is loaded.
//     Block-comment state machine: reads blockCommentStart/blockCommentEnd from the
//     SyntaxDefinition (.whlang metadata) to highlight multi-line block comments correctly.
// ==========================================================

using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Editor.TextEditor.Highlighting;

// Alias to avoid ambiguity with WpfHexEditor.Editor.CodeEditor.Helpers.RegexSyntaxHighlighter
using TextEditorHighlighter = WpfHexEditor.Editor.TextEditor.Highlighting.RegexSyntaxHighlighter;

namespace WpfHexEditor.Editor.CodeEditor.Helpers;

/// <summary>
/// Syntax highlighter that uses a <see cref="SyntaxDefinition"/> (.whlang) and the
/// <see cref="RegexSyntaxHighlighter"/> engine from the TextEditor assembly.
/// Resolves <c>TE_*</c> color keys through the WPF resource system.
/// Tracks multi-line block comments using the <c>blockCommentStart</c> /
/// <c>blockCommentEnd</c> metadata declared in the .whlang file.
/// </summary>
public sealed class RegexBasedSyntaxHighlighter : ISyntaxHighlighter
{
    // -- Static fallback brushes for TE_* color keys ---------------------------
    private static readonly IReadOnlyDictionary<string, Brush> _fallbackBrushes =
        new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase)
        {
            ["TE_Keyword"]      = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)),
            ["TE_Comment"]      = new SolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55)),
            ["TE_String"]       = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78)),
            ["TE_Number"]       = new SolidColorBrush(Color.FromRgb(0xB5, 0xCE, 0xA8)),
            ["TE_Operator"]     = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
            ["TE_Preprocessor"] = new SolidColorBrush(Color.FromRgb(0xBD, 0x63, 0xC5)),
            ["TE_Type"]         = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
            ["TE_Label"]        = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA)),
            ["TE_Register"]     = new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)),
            ["TE_Directive"]    = new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0)),
            ["TE_RegionName"]   = new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)),  // VS-like light blue for region labels
            ["TE_Address"]      = new SolidColorBrush(Color.FromRgb(0xB5, 0xCE, 0xA8)),
            ["TE_Foreground"]   = Brushes.WhiteSmoke,
        };

    private readonly TextEditorHighlighter _inner;
    private readonly Brush                  _defaultForeground;
    private readonly FrameworkElement?      _resourceHost;

    // Block-comment state — populated from .whlang blockCommentStart/blockCommentEnd.
    private readonly string? _blockCommentStart;
    private readonly string? _blockCommentEnd;
    private bool             _inBlockComment;

    /// <summary>
    /// Creates a new <see cref="RegexBasedSyntaxHighlighter"/>.
    /// </summary>
    /// <param name="definition">Syntax definition loaded from a .whlang file.</param>
    /// <param name="resourceHost">
    ///   Optional WPF element used to resolve <c>TE_*</c> color keys via
    ///   <see cref="FrameworkElement.TryFindResource"/>. Falls back to static defaults if null.
    /// </param>
    /// <param name="defaultForeground">Brush for uncovered text spans.</param>
    public RegexBasedSyntaxHighlighter(
        SyntaxDefinition  definition,
        FrameworkElement? resourceHost    = null,
        Brush?            defaultForeground = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _inner              = new TextEditorHighlighter(definition);
        _resourceHost       = resourceHost;
        _defaultForeground  = defaultForeground ?? Brushes.WhiteSmoke;
        _blockCommentStart  = definition.BlockCommentStart;
        _blockCommentEnd    = definition.BlockCommentEnd;
    }

    /// <inheritdoc />
    public string? LanguageName => null;

    /// <inheritdoc />
    public IReadOnlyList<SyntaxHighlightToken> Highlight(string lineText, int lineIndex)
    {
        if (string.IsNullOrEmpty(lineText))
            return [];

        var commentBrush = ResolveBrush("TE_Comment");

        // --- Continuation: we are inside an open block comment ---
        if (_inBlockComment && _blockCommentEnd != null)
        {
            int closeIdx = lineText.IndexOf(_blockCommentEnd, StringComparison.Ordinal);

            if (closeIdx < 0)
            {
                // Entire line is inside the block comment.
                return [new SyntaxHighlightToken(0, lineText.Length, lineText, commentBrush)];
            }

            // Closing delimiter found on this line.
            int closeEnd = closeIdx + _blockCommentEnd.Length;
            _inBlockComment = false;

            var tokens = new List<SyntaxHighlightToken>();
            tokens.Add(new SyntaxHighlightToken(0, closeEnd, lineText[..closeEnd], commentBrush));

            if (closeEnd < lineText.Length)
                tokens.AddRange(HighlightNormalFragment(lineText[closeEnd..], closeEnd));

            return tokens;
        }

        // --- Check for a block comment opening on this line ---
        if (_blockCommentStart != null)
        {
            int openIdx = lineText.IndexOf(_blockCommentStart, StringComparison.Ordinal);

            if (openIdx >= 0)
            {
                int closeIdx = _blockCommentEnd != null
                    ? lineText.IndexOf(_blockCommentEnd, openIdx + _blockCommentStart.Length, StringComparison.Ordinal)
                    : -1;

                if (closeIdx < 0)
                {
                    // Block comment opens on this line but does not close — multi-line.
                    _inBlockComment = true;

                    var tokens = new List<SyntaxHighlightToken>();

                    if (openIdx > 0)
                        tokens.AddRange(HighlightNormalFragment(lineText[..openIdx], 0));

                    tokens.Add(new SyntaxHighlightToken(
                        openIdx, lineText.Length - openIdx, lineText[openIdx..], commentBrush));

                    return tokens;
                }

                // Both opening and closing delimiters are on the same line — the inner
                // regex rule (e.g. <!--[\s\S]*?-->) will match it correctly; fall through.
            }
        }

        // --- Normal line: delegate entirely to the inner regex highlighter ---
        return HighlightNormalFragment(lineText, 0);
    }

    /// <inheritdoc />
    public void Reset() { _inBlockComment = false; }

    // -- Private helpers -------------------------------------------------------

    /// <summary>
    /// Runs the inner (stateless) regex highlighter on <paramref name="text"/> and
    /// returns tokens with column positions offset by <paramref name="columnOffset"/>.
    /// </summary>
    private IReadOnlyList<SyntaxHighlightToken> HighlightNormalFragment(string text, int columnOffset)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var spans = _inner.Highlight(text);

        if (spans.Count == 0)
            return [new SyntaxHighlightToken(columnOffset, text.Length, text, _defaultForeground)];

        var tokens   = new List<SyntaxHighlightToken>(spans.Count + 2);
        int position = 0;

        foreach (var span in spans)
        {
            // Fill uncovered gap before this span with default colour.
            if (span.Start > position)
            {
                var gap = text.Substring(position, span.Start - position);
                tokens.Add(new SyntaxHighlightToken(columnOffset + position, gap.Length, gap, _defaultForeground));
            }

            var brush    = ResolveBrush(span.ColorKey);
            var spanText = text.Substring(span.Start, Math.Min(span.Length, text.Length - span.Start));
            tokens.Add(new SyntaxHighlightToken(columnOffset + span.Start, spanText.Length, spanText, brush));
            position = span.Start + span.Length;
        }

        // Fill trailing uncovered text.
        if (position < text.Length)
        {
            var tail = text.Substring(position);
            tokens.Add(new SyntaxHighlightToken(columnOffset + position, tail.Length, tail, _defaultForeground));
        }

        return tokens;
    }

    private Brush ResolveBrush(string colorKey)
    {
        if (_resourceHost?.TryFindResource(colorKey) is Brush themeBrush)
            return themeBrush;

        return _fallbackBrushes.TryGetValue(colorKey, out var fallback)
            ? fallback
            : _defaultForeground;
    }
}
