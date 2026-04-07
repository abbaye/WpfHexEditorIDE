// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/StructuralFormatter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-02
// Description:
//     Structural code formatter driven by whfmt FormattingRules.
//     Replaces BasicIndentFormatter with full support for:
//       - Whitespace normalisation (tabs/spaces, trailing, final newline, line endings)
//       - Brace style (Allman / K&R / Stroustrup)
//       - Spacing (keywords, operators, commas, parens)
//       - Blank line management (max consecutive, before methods, after imports)
//       - Import/using organisation (sort, separate system group)
//       - Quote style normalisation (JS/Python)
//       - SQL keyword uppercasing
//     No AST required - line-by-line analysis with lightweight context tracking.
//
// Architecture Notes:
//     Stateless - all methods are static; no shared state.
//     Called by CodeFormattingService when no LSP provider is available.
//     Returns the formatted text as a string; no document mutation occurs here.
// ==========================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Structural code formatter that applies <see cref="FormattingRules"/>
/// without requiring a language server.
/// </summary>
public static class StructuralFormatter
{
    // Universal comma-space rule — no language-specific variation possible.
    private static readonly Regex s_commaNoSpace = new(@",(?!\s)", RegexOptions.Compiled);

    // Per-language regex cache keyed by "type:keyword1|keyword2|…"
    private static readonly ConcurrentDictionary<string, Regex> s_regexCache = new();

    private static Regex GetOrBuild(string cacheKey, Func<Regex> factory)
        => s_regexCache.GetOrAdd(cacheKey, _ => factory());

    private static Regex GetKeywordParenRegex(FormattingRules rules)
    {
        var kws = rules.KeywordParenKeywords ?? FormattingDefaults.KeywordParenKeywords;
        string key = "kp:" + string.Join("|", kws);
        return GetOrBuild(key, () =>
        {
            string alts = string.Join("|", kws.Select(Regex.Escape));
            return new Regex($@"\b({alts})\(", RegexOptions.Compiled);
        });
    }

    private static Regex GetBinaryOpRegex(FormattingRules rules)
    {
        var ops = rules.BinaryOperators ?? FormattingDefaults.BinaryOperators;
        string key = "bo:" + string.Join("|", ops);
        return GetOrBuild(key, () =>
        {
            string alts = string.Join("|", ops.Select(op => Regex.Escape(op)));
            return new Regex($@"(?<=\S)({alts})(?=\S)", RegexOptions.Compiled);
        });
    }

    private static Regex GetMethodDeclRegex(FormattingRules rules)
    {
        var kws = rules.MethodDeclKeywords ?? FormattingDefaults.MethodDeclKeywords;
        string key = "md:" + string.Join("|", kws);
        return GetOrBuild(key, () =>
        {
            string alts = string.Join("|", kws.Select(Regex.Escape));
            return new Regex($@"^\s*({alts})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        });
    }

    private static Regex GetImportLineRegex(FormattingRules rules)
    {
        var kws = rules.ImportKeywords ?? FormattingDefaults.ImportKeywords;
        string key = "il:" + string.Join("|", kws);
        return GetOrBuild(key, () =>
        {
            string alts = string.Join("|", kws.Select(Regex.Escape));
            return new Regex($@"^\s*({alts})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        });
    }

    private static Regex GetSqlKeywordRegex(FormattingRules rules)
    {
        var kws = rules.SqlKeywords ?? FormattingDefaults.SqlKeywords;
        string key = "sq:" + string.Join("|", kws);
        return GetOrBuild(key, () =>
        {
            string alts = string.Join("|", kws.Select(kw => Regex.Escape(kw.ToUpperInvariant())));
            return new Regex($@"\b({alts})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        });
    }

    public static string FormatDocument(string text, FormattingRules? rules)
    {
        if (rules is null || string.IsNullOrEmpty(text)) return text;
        if (rules.FormatterStrategy == FormatterStrategy.Xml)
            return XmlStructuralFormatter.FormatDocument(text, rules);
        return ApplyAll(text, 0, -1, rules);
    }

    public static string FormatSelection(string text, int startLine, int endLine, FormattingRules? rules)
    {
        if (rules is null || string.IsNullOrEmpty(text)) return text;
        if (startLine < 0 || endLine < startLine) return text;
        // XML selection formatting falls back to full-document (range context unavailable).
        if (rules.FormatterStrategy == FormatterStrategy.Xml)
            return XmlStructuralFormatter.FormatDocument(text, rules);
        return ApplyAll(text, startLine, endLine, rules);
    }

    private static string ApplyAll(string text, int startLine, int endLine, FormattingRules rules)
    {
        var lines = SplitLines(text, out string originalEnding);
        if (endLine < 0) endLine = lines.Length - 1;
        endLine = Math.Min(endLine, lines.Length - 1);

        string le = rules.LineEnding switch
        {
            LineEndingStyle.LF   => "\n",
            LineEndingStyle.CRLF => "\r\n",
            _                    => originalEnding,
        };

        // ── Pass 1: per-line transformations (spacing, trailing ws, quotes, SQL) ──
        for (int i = startLine; i <= endLine; i++)
        {
            string line = lines[i];
            if (rules.TrimTrailingWhitespace) line = line.TrimEnd();
            if (rules.SpaceAfterKeywords) line = GetKeywordParenRegex(rules).Replace(line, m => m.Value[..^1] + " (");
            if (rules.SpaceAroundBinaryOperators) line = GetBinaryOpRegex(rules).Replace(line, " $1 ");
            if (rules.SpaceAfterComma) line = s_commaNoSpace.Replace(line, ", ");
            if (rules.SqlKeywordsUppercase) line = GetSqlKeywordRegex(rules).Replace(line, m => m.Value.ToUpperInvariant());
            if (rules.QuoteStyle is not null) line = NormalizeQuotes(line, rules.QuoteStyle.Value);
            lines[i] = line;
        }

        // ── Pass 2: brace style (may add/remove lines, updates endLine) ──────────
        if (rules.BraceStyle is not null)
            lines = ApplyBraceStyle(lines, startLine, ref endLine, rules.BraceStyle.Value, rules.SpaceBeforeOpenBrace);

        // ── Pass 3: re-indent every line in range based on brace depth ───────────
        lines = ReIndent(lines, startLine, endLine, rules.UseTabs, rules.IndentSize,
                         rules.BlockOpenKeywords.Count  > 0 ? rules.BlockOpenKeywords  : null,
                         rules.BlockCloseKeywords.Count > 0 ? rules.BlockCloseKeywords : null);

        // ── Pass 4: case-label indentation ───────────────────────────────────────
        if (rules.IndentCaseLabels)
            ApplyIndentCaseLabels(lines, startLine, endLine, rules.UseTabs, rules.IndentSize);

        // ── Pass 5: blank-line rules ──────────────────────────────────────────────
        lines = ApplyBlankLineRules(lines, startLine, ref endLine, rules);

        // ── Pass 6: import organisation ───────────────────────────────────────────
        if (rules.OrganizeImports)
            OrganizeImportBlock(lines, startLine, endLine, rules.SeparateSystemImports, rules);

        var sb = new StringBuilder(text.Length + 128);
        for (int i = 0; i < lines.Length; i++)
        {
            sb.Append(lines[i]);
            if (i < lines.Length - 1) sb.Append(le);
        }
        if (rules.InsertFinalNewline && lines.Length > 0 && !sb.ToString().EndsWith(le))
            sb.Append(le);

        return sb.ToString();
    }

    /// <summary>
    /// Re-indents lines in [<paramref name="start"/>, <paramref name="end"/>].
    /// <para>
    /// When <paramref name="blockOpen"/>/<paramref name="blockClose"/> are provided
    /// (VB.NET, Ruby, Python-style languages) those keyword sets drive depth.
    /// Otherwise brace characters <c>{</c> / <c>}</c> are used (C-style languages).
    /// </para>
    /// </summary>
    private static string[] ReIndent(string[] lines, int start, int end,
                                     bool useTabs, int indentSize,
                                     IReadOnlyList<string>? blockOpen  = null,
                                     IReadOnlyList<string>? blockClose = null)
    {
        string Unit(int n) => useTabs ? new string('\t', n) : new string(' ', n * indentSize);

        bool useKeywords = blockOpen is { Count: > 0 } || blockClose is { Count: > 0 };

        // ── Keyword-based depth helpers ──────────────────────────────────────
        // Returns true if the trimmed line starts with any of the given keywords
        // (case-insensitive, whole-word match before optional trailing content).
        static bool StartsWithKeyword(string trimmed, IReadOnlyList<string> keywords)
        {
            foreach (var kw in keywords)
            {
                if (trimmed.Length < kw.Length) continue;
                if (!trimmed.StartsWith(kw, StringComparison.OrdinalIgnoreCase)) continue;
                // Accept kw at EOL or followed by whitespace / punctuation.
                if (trimmed.Length == kw.Length || !char.IsLetterOrDigit(trimmed[kw.Length]))
                    return true;
            }
            return false;
        }

        // Returns true if the trimmed line ends with any of the given keywords.
        static bool EndsWithKeyword(string trimmed, IReadOnlyList<string> keywords)
        {
            foreach (var kw in keywords)
            {
                if (trimmed.Length < kw.Length) continue;
                if (!trimmed.EndsWith(kw, StringComparison.OrdinalIgnoreCase)) continue;
                int offset = trimmed.Length - kw.Length;
                if (offset == 0 || !char.IsLetterOrDigit(trimmed[offset - 1]))
                    return true;
            }
            return false;
        }

        // Seed depth from lines before the range.
        int depth = 0;
        for (int i = 0; i < start; i++)
        {
            string t = lines[i].Trim();
            if (useKeywords)
            {
                if (blockClose is not null && StartsWithKeyword(t, blockClose)) depth--;
                if (blockOpen  is not null && EndsWithKeyword(t, blockOpen))    depth++;
            }
            else
            {
                foreach (char c in t) { if (c == '{') depth++; else if (c == '}') depth--; }
            }
        }
        depth = Math.Max(0, depth);

        for (int i = start; i <= end && i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.Length == 0) { lines[i] = string.Empty; continue; }

            if (useKeywords)
            {
                // Close keyword at line start → de-dent before writing.
                bool closes = blockClose is not null && StartsWithKeyword(trimmed, blockClose);
                if (closes) depth = Math.Max(0, depth - 1);

                lines[i] = Unit(depth) + trimmed;

                // Open keyword at line end → indent next line.
                bool opens = blockOpen is not null && EndsWithKeyword(trimmed, blockOpen);
                if (opens)  depth++;
            }
            else
            {
                // Brace-based: leading } chars de-dent before writing.
                int closingLeaders = 0;
                foreach (char c in trimmed) { if (c == '}') closingLeaders++; else break; }
                depth = Math.Max(0, depth - closingLeaders);

                lines[i] = Unit(depth) + trimmed;

                int opens  = trimmed.Count(c => c == '{');
                int closes = trimmed.Count(c => c == '}');
                depth = Math.Max(0, depth + opens - closes + closingLeaders);
            }
        }
        return lines;
    }

    private static string[] ApplyBraceStyle(string[] lines, int start, ref int end, BraceStyle style, bool spaceBeforeBrace)
    {
        var result = new List<string>(lines.Length + 32);
        int offset = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (i < start || i > end) { result.Add(lines[i]); continue; }
            string trimmed = lines[i].TrimEnd();
            string indent = GetLeadingWhitespace(lines[i]);

            if (style == BraceStyle.Allman && trimmed.Length > 1 && trimmed.EndsWith('{'))
            {
                string w = trimmed[..^1].TrimEnd();
                if (w.Length > 0) { result.Add(indent + w); result.Add(indent + "{"); offset++; continue; }
            }
            else if (style == BraceStyle.KR && trimmed == "{" && result.Count > 0)
            {
                string prev = result[^1].TrimEnd();
                if (prev.Length > 0 && !prev.EndsWith('{') && !prev.EndsWith('}'))
                { result[^1] = prev + (spaceBeforeBrace ? " " : "") + "{"; offset--; continue; }
            }
            result.Add(lines[i]);
        }
        end += offset;
        return result.ToArray();
    }

    private static void ApplyIndentCaseLabels(string[] lines, int start, int end, bool useTabs, int indentSize)
    {
        string oneIndent = useTabs ? "\t" : new string(' ', indentSize);
        bool inSwitch = false;
        for (int i = start; i <= end && i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith("switch")) inSwitch = true;
            if (inSwitch && (trimmed.StartsWith("case ") || trimmed.StartsWith("default:")))
                lines[i] = GetLeadingWhitespace(lines[i]) + oneIndent + trimmed;
            if (inSwitch && trimmed == "}") inSwitch = false;
        }
    }

    private static string[] ApplyBlankLineRules(string[] lines, int start, ref int end, FormattingRules rules)
    {
        var result = new List<string>(lines.Length);
        int blanks = 0, offset = 0;
        bool lastWasImport = false;
        for (int i = 0; i < lines.Length; i++)
        {
            bool inRange = i >= start && i <= end + offset;
            string trimmed = lines[i].Trim();
            bool isEmpty = trimmed.Length == 0;

            if (!inRange) { result.Add(lines[i]); blanks = isEmpty ? blanks + 1 : 0; lastWasImport = !isEmpty && GetImportLineRegex(rules).IsMatch(trimmed); continue; }

            bool isImport = GetImportLineRegex(rules).IsMatch(trimmed);
            bool isMethod = GetMethodDeclRegex(rules).IsMatch(trimmed);
            if (rules.BlankLineAfterImports && lastWasImport && !isImport && !isEmpty && blanks == 0) { result.Add(""); offset++; }
            if (rules.BlankLineBeforeMethod && isMethod && result.Count > 0 && blanks == 0)
            {
                string pt = result[^1].Trim();
                if (pt.Length > 0 && pt != "{") { result.Add(""); offset++; }
            }
            if (isEmpty) { blanks++; if (blanks > rules.MaxConsecutiveBlankLines) { offset--; continue; } }
            else blanks = 0;
            lastWasImport = isImport;
            result.Add(lines[i]);
        }
        end += offset;
        return result.ToArray();
    }

    private static void OrganizeImportBlock(string[] lines, int start, int end, bool separateSystem, FormattingRules rules)
    {
        var importRx = GetImportLineRegex(rules);
        int bs = -1, be = -1;
        for (int i = start; i <= end && i < lines.Length; i++)
        {
            string t = lines[i].Trim();
            if (importRx.IsMatch(t)) { if (bs < 0) bs = i; be = i; }
            else if (t.Length > 0 && bs >= 0) break;
        }
        if (bs < 0 || be <= bs) return;
        var imports = new List<string>();
        for (int i = bs; i <= be; i++) { if (lines[i].Trim().Length > 0) imports.Add(lines[i]); }
        if (imports.Count <= 1) return;

        List<string> sorted;
        if (separateSystem)
        {
            var sys = imports.Where(l => l.Contains("System")).OrderBy(l => l.Trim()).ToList();
            var oth = imports.Where(l => !l.Contains("System")).OrderBy(l => l.Trim()).ToList();
            sorted = new List<string>(sys);
            if (sys.Count > 0 && oth.Count > 0) sorted.Add("");
            sorted.AddRange(oth);
        }
        else sorted = imports.OrderBy(l => l.Trim()).ToList();

        int wi = bs;
        foreach (var l in sorted) { if (wi <= be) lines[wi++] = l; }
        while (wi <= be) lines[wi++] = "";
    }

    private static string NormalizeQuotes(string line, QuoteStyle target)
    {
        char from, to;
        switch (target)
        {
            case QuoteStyle.Single: from = '"'; to = '\''; break;
            case QuoteStyle.Double: from = '\''; to = '"'; break;
            default: return line;
        }
        var sb = new StringBuilder(line.Length);
        bool inStr = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\\' && inStr && i + 1 < line.Length) { sb.Append(c); sb.Append(line[++i]); continue; }
            if (c == from && !inStr) { sb.Append(to); inStr = true; continue; }
            if (c == from && inStr) { sb.Append(to); inStr = false; continue; }
            if (c == to && !inStr) break;
            sb.Append(c);
        }
        if (sb.Length < line.Length) sb.Append(line.AsSpan(sb.Length));
        return sb.ToString();
    }

    private static string GetLeadingWhitespace(string line)
    {
        int i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
        return line[..i];
    }

    private static string[] SplitLines(string text, out string lineEnding)
    {
        lineEnding = text.Contains("\r\n") ? "\r\n" : "\n";
        return text.Split(["\r\n", "\n"], StringSplitOptions.None);
    }
}