// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/XmlStructuralFormatter.cs
// Description:
//     Tag-aware structural formatter for XML/XAML markup languages.
//     Activated when FormattingRules.FormatterStrategy == FormatterStrategy.Xml,
//     which is auto-derived from foldingRules.tagBased in the whfmt file.
//
// Architecture Notes:
//     Stateless — all methods are static.
//     Only whitespace-safe passes are applied (trimming, line endings, blank
//     lines, indentation). Passes that mutate code semantics (brace style,
//     binary operators, comma spacing, quote normalization) are skipped entirely
//     to avoid corrupting attribute values and markup extensions.
//
//     Multi-line tag support: a state machine tracks whether we are inside
//     an incomplete opening tag (attributes spanning several lines). While in
//     InsideOpenTag state the depth counter is NOT yet incremented — it is
//     incremented only when the closing ">" is found, or left unchanged when
//     "/>" is found (self-closing). This prevents depth drift on long attribute
//     lists.
//
//     XmlAttributeIndentLevels (default 2): controls how many indent levels
//     are added to attribute continuation lines relative to the element's depth.
//     Default = 2 matches VS XAML formatting (double-indent).
//
//     XmlOneAttributePerLine (default false): when enabled, each attribute in
//     a multi-attribute opening tag is placed on its own line (first attribute
//     stays on the tag line). Markup extensions {…} are treated as atomic tokens.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Structural formatter for tag-based (XML / XAML / HTML) languages.
/// </summary>
internal static class XmlStructuralFormatter
{
    // Matches a line that both opens AND closes: <Foo attr="x">content</Foo>
    private static readonly Regex s_openAndClose =
        new(@"^\s*<([A-Za-z_][A-Za-z0-9_.:]*)[^>]*>.*</\1\s*>\s*$", RegexOptions.Compiled);

    private enum TagState
    {
        Normal,
        /// <summary>
        /// Inside a multi-line opening tag whose closing "&gt;" has not been seen yet.
        /// depth has NOT been incremented for this tag yet.
        /// </summary>
        InsideOpenTag,
    }

    public static string FormatDocument(string text, FormattingRules rules)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var lines = SplitLines(text, out string originalEnding);

        string le = rules.LineEnding switch
        {
            LineEndingStyle.LF   => "\n",
            LineEndingStyle.CRLF => "\r\n",
            _                    => originalEnding,
        };

        // ── Pass 0 : one attribute per line (expands before re-indent) ──────
        if (rules.XmlOneAttributePerLine)
            lines = ExpandAttributes(lines, rules);

        // ── Pass 1 : trim trailing whitespace ──────────────────────────────
        if (rules.TrimTrailingWhitespace)
        {
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].TrimEnd();
        }

        // ── Pass 2 : blank-line cap ────────────────────────────────────────
        lines = CapBlankLines(lines, rules.MaxConsecutiveBlankLines);

        // ── Pass 3 : tag-based re-indentation ─────────────────────────────
        lines = ReIndent(lines, rules);

        // ── Assemble ───────────────────────────────────────────────────────
        var sb = new StringBuilder(text.Length + 64);
        for (int i = 0; i < lines.Length; i++)
        {
            sb.Append(lines[i]);
            if (i < lines.Length - 1) sb.Append(le);
        }
        if (rules.InsertFinalNewline && lines.Length > 0 && !sb.ToString().EndsWith(le))
            sb.Append(le);

        return sb.ToString();
    }

    // ── Re-indentation ────────────────────────────────────────────────────────

    private static string[] ReIndent(string[] lines, FormattingRules rules)
    {
        bool useTabs   = rules.UseTabs;
        int  indentSize = rules.IndentSize;
        int  attrLevels = Math.Max(1, rules.XmlAttributeIndentLevels);

        string Unit(int depth)         => useTabs ? new string('\t', depth)             : new string(' ', depth * indentSize);
        string Continuation(int depth) => useTabs ? new string('\t', depth + attrLevels) : new string(' ', (depth + attrLevels) * indentSize);

        int      depth = 0;
        TagState state = TagState.Normal;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.Length == 0) { lines[i] = string.Empty; continue; }

            // ── Inside a multi-line opening tag ───────────────────────────
            if (state == TagState.InsideOpenTag)
            {
                lines[i] = Continuation(depth) + trimmed;

                if (trimmed.EndsWith("/>"))
                {
                    // Self-closing: tag done, depth stays unchanged.
                    state = TagState.Normal;
                }
                else if (EndsTagOpen(trimmed))
                {
                    // Opening ">" found: children will be at depth+1.
                    depth++;
                    state = TagState.Normal;
                }
                // else: more attribute lines — stay in InsideOpenTag.
                continue;
            }

            // ── Normal state ──────────────────────────────────────────────

            // Comments, PI, DOCTYPE — neutral, no depth change.
            if (trimmed.StartsWith("<!--") || trimmed.StartsWith("<?") || trimmed.StartsWith("<!"))
            {
                lines[i] = Unit(depth) + trimmed;
                continue;
            }

            // Closing tag </Foo>
            if (trimmed.StartsWith("</"))
            {
                depth = Math.Max(0, depth - 1);
                lines[i] = Unit(depth) + trimmed;
                continue;
            }

            // Any line starting with "<" is an opening or self-closing tag.
            if (trimmed.StartsWith("<"))
            {
                lines[i] = Unit(depth) + trimmed;

                if (trimmed.EndsWith("/>"))
                {
                    // Self-closing on one line — no depth change.
                }
                else if (s_openAndClose.IsMatch(trimmed))
                {
                    // <Foo>...</Foo> on one line — no depth change.
                }
                else if (EndsTagOpen(trimmed))
                {
                    // Complete opening tag ending with ">" → indent children.
                    depth++;
                }
                else
                {
                    // Multi-line tag: closing ">" is on a future line.
                    // Do NOT increment depth yet.
                    state = TagState.InsideOpenTag;
                }
                continue;
            }

            // Text content.
            lines[i] = Unit(depth) + trimmed;
        }

        return lines;
    }

    /// <summary>
    /// Returns true when <paramref name="trimmed"/> ends with a tag-closing "&gt;"
    /// (excludes "-->" comment endings).
    /// </summary>
    private static bool EndsTagOpen(string trimmed)
        => trimmed.EndsWith(">") && !trimmed.EndsWith("-->");

    // ── One-attribute-per-line expansion ─────────────────────────────────────

    /// <summary>
    /// Splits each opening tag that has 2+ attributes into multiple lines,
    /// keeping the first attribute on the tag line and placing each subsequent
    /// attribute on its own indented line. Markup extensions <c>{…}</c> are
    /// treated as atomic tokens (spaces inside braces are not split points).
    /// Comments, PIs, and closing tags are left untouched.
    /// </summary>
    private static string[] ExpandAttributes(string[] lines, FormattingRules rules)
    {
        bool useTabs    = rules.UseTabs;
        int  indentSize = rules.IndentSize;
        int  attrLevels = Math.Max(1, rules.XmlAttributeIndentLevels);

        var result = new List<string>(lines.Length + 16);

        foreach (var line in lines)
        {
            string trimmed = line.TrimStart();

            // Only expand proper opening tags (not comments, PI, DOCTYPE, closing tags).
            if (!trimmed.StartsWith("<") || trimmed.StartsWith("</")
                || trimmed.StartsWith("<!--") || trimmed.StartsWith("<?")
                || trimmed.StartsWith("<!"))
            {
                result.Add(line);
                continue;
            }

            string leadingWhitespace = line[..^trimmed.Length];

            // Determine the indent depth from the current leading whitespace.
            int currentDepth = useTabs
                ? leadingWhitespace.Length        // tab chars = depth
                : leadingWhitespace.Length / Math.Max(1, indentSize);

            string attrIndent = useTabs
                ? new string('\t', currentDepth + attrLevels)
                : new string(' ', (currentDepth + attrLevels) * indentSize);

            // Parse tag name and attributes.
            if (!TryParseOpenTag(trimmed, out string tagPrefix, out List<string> attrs, out string tagSuffix))
            {
                result.Add(line);
                continue;
            }

            // Single attribute or no attributes — leave on one line.
            if (attrs.Count <= 1)
            {
                result.Add(line);
                continue;
            }

            // First attribute stays on tag line: <TagName firstAttr="val"
            result.Add(leadingWhitespace + tagPrefix + attrs[0]);

            // Remaining attributes on their own lines.
            for (int i = 1; i < attrs.Count; i++)
                result.Add(attrIndent + attrs[i]);

            // Closing > or /> on the last attribute line (or alone if suffix is just whitespace).
            if (!string.IsNullOrWhiteSpace(tagSuffix))
                result[^1] += tagSuffix;
        }

        return result.ToArray();
    }

    /// <summary>
    /// Parses an opening tag line into its prefix (<c>&lt;TagName </c>), attribute list,
    /// and closing suffix (<c>&gt;</c> or <c>/&gt;</c>).
    /// Returns false when the line is not a parseable opening tag with attributes.
    /// </summary>
    private static bool TryParseOpenTag(
        string trimmed,
        out string tagPrefix,
        out List<string> attrs,
        out string tagSuffix)
    {
        tagPrefix = string.Empty;
        attrs     = [];
        tagSuffix = string.Empty;
        var attrList = attrs; // local alias — avoids CS1628 (out param in local function)

        // Must start with < followed by a tag name character.
        if (trimmed.Length < 2 || trimmed[0] != '<' || trimmed[1] == '/' || trimmed[1] == '!')
            return false;

        // Find end of tag name (space or >)
        int nameEnd = 1;
        while (nameEnd < trimmed.Length && trimmed[nameEnd] != ' ' && trimmed[nameEnd] != '>' && trimmed[nameEnd] != '/')
            nameEnd++;

        tagPrefix = trimmed[..(nameEnd + 1)]; // includes the trailing space or first delimiter

        // If there are no attributes (tag closes immediately), bail out.
        if (nameEnd >= trimmed.Length || (trimmed[nameEnd] is '>' or '/'))
        {
            tagPrefix = trimmed[..nameEnd];
            tagSuffix = trimmed[nameEnd..];
            return false;
        }

        tagPrefix = trimmed[..nameEnd] + " "; // "<TagName "

        // Scan attributes using a mini state machine respecting quotes and {…}.
        int    pos         = nameEnd + 1; // skip the space after tag name
        int    braceDepth  = 0;
        bool   inDouble    = false;
        bool   inSingle    = false;
        var    current     = new StringBuilder();

        void FlushAttr()
        {
            var a = current.ToString().Trim();
            if (a.Length > 0) attrList.Add(a);
            current.Clear();
        }

        while (pos < trimmed.Length)
        {
            char c = trimmed[pos];

            if (inDouble)
            {
                current.Append(c);
                if (c == '"') inDouble = false;
                pos++;
                continue;
            }
            if (inSingle)
            {
                current.Append(c);
                if (c == '\'') inSingle = false;
                pos++;
                continue;
            }
            if (braceDepth > 0)
            {
                current.Append(c);
                if      (c == '{') braceDepth++;
                else if (c == '}') braceDepth--;
                pos++;
                continue;
            }

            switch (c)
            {
                case '"':  inDouble = true;  current.Append(c); break;
                case '\'': inSingle = true;  current.Append(c); break;
                case '{':  braceDepth = 1;   current.Append(c); break;

                case ' ':
                case '\t':
                    FlushAttr();
                    // skip additional whitespace
                    while (pos + 1 < trimmed.Length && trimmed[pos + 1] is ' ' or '\t') pos++;
                    break;

                case '/':
                    if (pos + 1 < trimmed.Length && trimmed[pos + 1] == '>')
                    {
                        FlushAttr();
                        tagSuffix = "/>";
                        pos = trimmed.Length; // done
                        continue;
                    }
                    current.Append(c);
                    break;

                case '>':
                    FlushAttr();
                    tagSuffix = ">";
                    pos = trimmed.Length; // done
                    continue;

                default:
                    current.Append(c);
                    break;
            }
            pos++;
        }

        FlushAttr();

        // If tagSuffix is still empty the tag spans multiple lines — leave as-is.
        if (tagSuffix.Length == 0 && current.Length == 0)
        {
            // Multi-line tag opened: not a candidate for single-pass expansion.
            return false;
        }

        return attrs.Count > 0;
    }

    // ── Blank-line cap ────────────────────────────────────────────────────────

    private static string[] CapBlankLines(string[] lines, int max)
    {
        if (max < 0) return lines;
        var result = new List<string>(lines.Length);
        int blanks = 0;
        foreach (var line in lines)
        {
            if (line.Trim().Length == 0) { blanks++; if (blanks > max) continue; }
            else blanks = 0;
            result.Add(line);
        }
        return result.ToArray();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string[] SplitLines(string text, out string lineEnding)
    {
        lineEnding = text.Contains("\r\n") ? "\r\n" : "\n";
        return text.Split(["\r\n", "\n"], StringSplitOptions.None);
    }
}
