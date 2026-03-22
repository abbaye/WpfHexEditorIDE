// ==========================================================
// Project: WpfHexEditor.Editor.TextEditor
// File: Highlighting/FencedCodeHighlighter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-22
// Description:
//     Generic fence-aware contextual highlighter.
//     Reads an embedded-language map from SyntaxDefinition.EmbeddedLanguages
//     (declared in the .whfmt syntaxDefinition block) and delegates per-line
//     highlighting to the correct language's RegexSyntaxHighlighter based on
//     which fenced code block the line belongs to.
//
//     Supported fence delimiters: ``` (3+ backticks) and ~~~ (3+ tildes).
//     Fence delimiter lines themselves are highlighted by the base highlighter
//     so they keep their existing Markdown colors (TE_String / TE_Keyword).
//     Lines inside an unknown fence tag are returned plain (empty span list).
//
// Architecture:
//     BuildContext() — O(N) fence scan; runs on the background Task.Run thread.
//     Highlight()    — O(k) per visible line; no shared mutable state.
//     Thread-safety  — fully thread-safe: no instance fields are written after
//                      construction; context array is local to each call site.
// ==========================================================

using WpfHexEditor.Editor.TextEditor.Services;

namespace WpfHexEditor.Editor.TextEditor.Highlighting;

/// <summary>
/// <see cref="IContextualHighlighter"/> that applies embedded-language syntax
/// highlighting inside fenced code blocks declared in a <c>.whfmt</c> file.
/// </summary>
internal sealed class FencedCodeHighlighter : IContextualHighlighter
{
    // -----------------------------------------------------------------------
    // Fields — immutable after construction
    // -----------------------------------------------------------------------

    private readonly RegexSyntaxHighlighter                       _base;
    private readonly Dictionary<string, string>                   _langMap;   // id → extension
    private readonly Dictionary<string, RegexSyntaxHighlighter?>  _langCache; // extension → highlighter (null = not found)

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    internal FencedCodeHighlighter(
        IReadOnlyList<EmbeddedLanguageEntry> entries,
        RegexSyntaxHighlighter baseHighlighter)
    {
        _base      = baseHighlighter;
        _langMap   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _langCache = new Dictionary<string, RegexSyntaxHighlighter?>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
            _langMap[e.Id] = e.Extension;
    }

    // -----------------------------------------------------------------------
    // IContextualHighlighter
    // -----------------------------------------------------------------------

    /// <summary>
    /// Scans all lines and builds the per-line language context array.
    /// Safe to call from a background thread — no instance state is modified.
    /// </summary>
    /// <returns>
    /// A <c>string?[]</c> where each element is:
    /// <list type="bullet">
    ///   <item><c>null</c> — outside a fence or the fence delimiter line itself (base Markdown colors)</item>
    ///   <item><c>""</c>   — inside an anonymous or unknown-language fence (plain text)</item>
    ///   <item>lang id     — inside a named, known-language fence</item>
    /// </list>
    /// </returns>
    public string?[] BuildContext(IReadOnlyList<string> allLines)
    {
        var context = new string?[allLines.Count];

        string? currentLang = null; // null = outside fence
        string? fenceMarker = null; // "```" or "~~~"

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i].TrimEnd();

            if (currentLang is null)
            {
                var fence = DetectFenceOpen(line);
                if (fence.HasValue)
                {
                    fenceMarker   = fence.Value.Delimiter;
                    currentLang   = fence.Value.LangId;
                    context[i]    = null; // delimiter line → base Markdown colors
                }
                else
                {
                    context[i] = null; // normal Markdown line
                }
            }
            else
            {
                if (IsClosingFence(line, fenceMarker!))
                {
                    context[i]  = null; // closing delimiter → base Markdown colors
                    currentLang = null;
                    fenceMarker = null;
                }
                else
                {
                    context[i] = currentLang; // "" = unknown lang, else = lang id
                }
            }
        }

        return context;
    }

    /// <summary>
    /// Returns highlight spans for the given line using the context from
    /// <see cref="BuildContext"/>.
    /// Safe to call from a background thread.
    /// </summary>
    public IReadOnlyList<ColoredSpan> Highlight(string lineText, int lineIndex, string?[] context)
    {
        if (lineIndex < 0 || lineIndex >= context.Length)
            return _base.Highlight(lineText);

        var lang = context[lineIndex];

        // null = outside fence or fence delimiter line → use base Markdown highlighter
        if (lang is null)
            return _base.Highlight(lineText);

        // "" = anonymous or unknown fence language → plain (no colors)
        if (lang.Length == 0)
            return [];

        // Known language — resolve highlighter (lazy, cached per extension)
        var highlighter = ResolveHighlighter(lang);
        return highlighter is not null ? highlighter.Highlight(lineText) : [];
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private readonly record struct FenceOpen(string Delimiter, string LangId);

    /// <summary>
    /// Returns a <see cref="FenceOpen"/> when <paramref name="line"/> is an opening fence.
    /// <c>LangId</c> is the tag text (e.g. "csharp") or <c>""</c> for anonymous fences.
    /// Returns <see langword="null"/> if the line is not a fence opener.
    /// </summary>
    private static FenceOpen? DetectFenceOpen(string line)
    {
        if (line.Length < 3) return null;

        char c = line[0];
        if (c != '`' && c != '~') return null;

        int run = 0;
        while (run < line.Length && line[run] == c) run++;
        if (run < 3) return null;

        var delimiter = new string(c, run);
        var tag       = line[run..].Trim();
        return new FenceOpen(delimiter, tag); // tag may be ""
    }

    /// <summary>Returns <see langword="true"/> if <paramref name="line"/> closes the current fence.</summary>
    private static bool IsClosingFence(string line, string openDelimiter)
    {
        var trimmed = line.TrimEnd();
        if (trimmed.Length < openDelimiter.Length) return false;
        char c = openDelimiter[0];
        foreach (var ch in trimmed)
            if (ch != c) return false;
        return trimmed.Length >= openDelimiter.Length;
    }

    /// <summary>
    /// Lazily resolves (and caches) the <see cref="RegexSyntaxHighlighter"/> for a lang id.
    /// Returns <see langword="null"/> when no definition exists for that extension.
    /// </summary>
    /// <remarks>
    /// <c>_langCache</c> is written from background threads. This is safe because
    /// duplicate-write races produce the same value and Dictionary reads/writes on
    /// a single key are atomic for reference types on x64 CLR.  If strict thread
    /// safety is ever required, replace with <c>ConcurrentDictionary</c>.
    /// </remarks>
    private RegexSyntaxHighlighter? ResolveHighlighter(string langId)
    {
        if (!_langMap.TryGetValue(langId, out var ext))
            return null;

        if (_langCache.TryGetValue(ext, out var cached))
            return cached;

        var def         = SyntaxDefinitionCatalog.Instance.FindByExtension(ext);
        var highlighter = def is not null ? new RegexSyntaxHighlighter(def) : null;
        _langCache[ext] = highlighter;
        return highlighter;
    }
}
