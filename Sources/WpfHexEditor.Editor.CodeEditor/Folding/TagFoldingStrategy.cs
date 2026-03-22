// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: TagFoldingStrategy.cs
// Author: WpfHexEditor Team
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-22
// Updated: 2026-03-22 — ADR-048: Added NormalizeLines() pre-processor for multi-line
//                        tag support (XAML/HTML attributes spanning several lines).
//            2026-03-22 — ADR-050: Replaced naive Contains('>') with quote-aware
//                        IsTagCloserPresent() to handle '>' inside attribute values.
//            2026-03-22 — ADR-051: NormalizeLines() now called unconditionally so
//                        multi-line root elements (<UserControl>, <Window>) always fold.
//            2026-03-22 — ADR-052: Added FindCommentRegions() to fold <!-- ... --> blocks.
// Description:
//     Tag-based folding for HTML, XML, and XAML documents.
//     Matches open and close tags across lines to build fold regions.
//     Multi-line opening tags are always normalized before matching.
//     Multi-line XML comment blocks are folded independently.
//
// Architecture Notes:
//     Strategy Pattern — implements IFoldingStrategy.
//     Data-driven: self-closing tag list + multilineTagSupport come from
//     FoldingRules deserialized from .whfmt.
// ==========================================================

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Produces foldable regions by matching XML/HTML opening and closing tags.
/// Self-closing tags (e.g. &lt;br/&gt;, &lt;img/&gt;) are excluded from region creation.
/// When <paramref name="multilineTagSupport"/> is true, attribute continuation lines
/// (lines that are part of an opening tag whose &gt; appears on a later line) are merged
/// into a single virtual line before matching, so multi-line XAML/HTML elements fold correctly.
/// </summary>
internal sealed class TagFoldingStrategy : IFoldingStrategy
{
    private static readonly Regex s_openTag  = new(@"<(?![\!/])(?<tag>[\w:.\-]+)[^>]*(?<!/)>", RegexOptions.Compiled);
    private static readonly Regex s_closeTag = new(@"</(?<tag>[\w:.\-]+)\s*>",                  RegexOptions.Compiled);

    private readonly HashSet<string> _selfClosing;
    private readonly bool            _multilineTagSupport;

    public TagFoldingStrategy(IReadOnlyList<string> selfClosingTags,
                               bool multilineTagSupport = false)
    {
        _selfClosing         = new HashSet<string>(selfClosingTags, StringComparer.OrdinalIgnoreCase);
        _multilineTagSupport = multilineTagSupport;
    }

    public IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines)
    {
        var regions = new List<FoldingRegion>();
        var stack   = new Stack<(int Line, string Tag)>();

        // Always normalize: merges multi-line opening tags so s_openTag can match them.
        // NormalizeLines() is O(n) and safe for single-line tags (they pass through unchanged).
        var virtualLines = NormalizeLines(lines);

        foreach (var (actualLine, text) in virtualLines)
        {
            foreach (Match m in s_openTag.Matches(text))
            {
                var tag = m.Groups["tag"].Value;
                if (!_selfClosing.Contains(tag))
                    stack.Push((actualLine, tag));
            }

            foreach (Match m in s_closeTag.Matches(text))
            {
                var tag = m.Groups["tag"].Value;
                if (stack.Count > 0
                 && stack.Peek().Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
                {
                    var (startLine, _) = stack.Pop();
                    if (actualLine > startLine + 1)
                        regions.Add(new FoldingRegion(startLine, actualLine, "{ \u2026 }", FoldingRegionKind.Brace));
                }
            }
        }

        // Fold multi-line comment blocks <!-- ... --> independently of tag matching.
        FindCommentRegions(lines, regions);

        return regions;
    }

    // ── Comment block folding ─────────────────────────────────────────────────

    /// <summary>
    /// Scans <paramref name="lines"/> for multi-line XML comment blocks
    /// (<c>&lt;!-- … --&gt;</c>) and appends a <see cref="FoldingRegion"/> for each
    /// block that spans 3 or more physical lines.
    /// Single-line comments (<c>&lt;!-- text --&gt;</c>) are skipped.
    /// </summary>
    private static void FindCommentRegions(IReadOnlyList<CodeLine> lines,
                                            List<FoldingRegion> regions)
    {
        int i = 0;
        while (i < lines.Count)
        {
            var text    = lines[i].Text ?? string.Empty;
            var trimmed = text.TrimStart();

            // Multi-line comment: opens with <!-- but --> not on the same line.
            if (trimmed.StartsWith("<!--") && !text.Contains("-->"))
            {
                int startLine = i;
                i++;
                while (i < lines.Count && !(lines[i].Text ?? string.Empty).Contains("-->"))
                    i++;

                // i is now at the "-->" line (or EOF if unterminated).
                if (i < lines.Count && i > startLine + 1)
                    regions.Add(new FoldingRegion(startLine, i, "<!-- \u2026 -->", FoldingRegionKind.Brace));

                i++; // advance past the "-->" line
            }
            else
            {
                i++;
            }
        }
    }

    // ── Multi-line pre-processing ─────────────────────────────────────────────

    /// <summary>
    /// Joins continuation lines belonging to the same opening tag into a single
    /// virtual line. Returns a list of (actualStartLine, joinedText) pairs.
    /// <para>
    /// A line "opens" a multi-line tag when it starts with &lt;TagName but does NOT
    /// contain a &gt; before the end of the line. Subsequent lines are accumulated
    /// until the closing &gt; is found, all mapped to the same actual start line.
    /// </para>
    /// </summary>
    private static List<(int ActualLine, string Text)> NormalizeLines(IReadOnlyList<CodeLine> lines)
    {
        var result = new List<(int, string)>(lines.Count);
        int i = 0;

        while (i < lines.Count)
        {
            var text = lines[i].Text ?? string.Empty;

            // Detect an opening tag that hasn't closed on this line yet:
            // starts with '<' (not '</' or '<!--' or '<!') and has no '>'.
            var trimmed = text.TrimStart();
            bool startsUnclosedTag = trimmed.StartsWith('<')
                                  && !trimmed.StartsWith("</")
                                  && !trimmed.StartsWith("<!--")
                                  && !trimmed.StartsWith("<!")
                                  && !IsTagCloserPresent(text);

            if (startsUnclosedTag)
            {
                int startLine = i;
                var sb = new StringBuilder(text);
                i++;

                // Accumulate continuation lines until we find the > that closes the tag.
                // Use quote-aware check so '>' inside attribute values (e.g. ToolTip="A > B")
                // does not prematurely end the merge.
                while (i < lines.Count && !IsTagCloserPresent(sb.ToString()))
                {
                    sb.Append(' ').Append((lines[i].Text ?? string.Empty).TrimStart());
                    i++;
                }

                result.Add((startLine, sb.ToString()));
            }
            else
            {
                result.Add((i, text));
                i++;
            }
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="text"/> contains a
    /// tag-closing <c>&gt;</c> that is NOT enclosed in single or double quotes —
    /// i.e. an actual XML tag delimiter, not a <c>&gt;</c> inside an attribute value
    /// such as <c>ToolTip="A &gt; B"</c>.
    /// </summary>
    private static bool IsTagCloserPresent(string text)
    {
        bool inSingle = false, inDouble = false;
        foreach (char c in text)
        {
            switch (c)
            {
                case '\'': if (!inDouble) inSingle = !inSingle; break;
                case '"':  if (!inSingle) inDouble = !inDouble; break;
                case '>':  if (!inSingle && !inDouble) return true; break;
            }
        }
        return false;
    }
}
