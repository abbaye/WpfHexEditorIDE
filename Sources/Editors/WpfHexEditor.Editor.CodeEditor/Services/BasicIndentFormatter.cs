// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/BasicIndentFormatter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Fallback formatter used when no LSP formatting provider is available.
//     Reads FormattingRules from the active LanguageDefinition (whfmt-driven):
//       - TrimTrailingWhitespace: strips trailing spaces/tabs on each line
//       - InsertFinalNewline: ensures the document ends with exactly one newline
//       - IndentSize / UseTabs: normalises leading indent — tabs ↔ spaces
//     Does NOT re-indent (no AST → cannot infer correct indent level).
//     The formatted text is returned as a string; no mutation occurs here.
//
// Architecture Notes:
//     Stateless — all methods are static; no shared state.
//     Called by CodeFormattingService when Capabilities.HasFormattingProvider is false.
// ==========================================================

using System;
using System.Text;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Basic whitespace-level formatter that applies <see cref="FormattingRules"/>
/// without requiring a language server.
/// </summary>
internal static class BasicIndentFormatter
{
    /// <summary>
    /// Formats the entire document text according to <paramref name="rules"/>.
    /// Returns the original text unchanged when <paramref name="rules"/> is null.
    /// </summary>
    public static string FormatDocument(string text, FormattingRules? rules)
    {
        if (rules is null || string.IsNullOrEmpty(text)) return text;
        return Apply(text.AsSpan(), 0, -1, rules);
    }

    /// <summary>
    /// Formats only the lines in [<paramref name="startLine"/>, <paramref name="endLine"/>]
    /// (0-based, inclusive) and splices the result back into the full text.
    /// </summary>
    public static string FormatSelection(
        string text, int startLine, int endLine, FormattingRules? rules)
    {
        if (rules is null || string.IsNullOrEmpty(text)) return text;
        if (startLine < 0 || endLine < startLine)        return text;

        var lines = SplitLines(text, out string lineEnding);
        if (startLine >= lines.Length) return text;
        endLine = Math.Min(endLine, lines.Length - 1);

        // Format only the selected slice.
        var sb      = new StringBuilder();
        string indentUnit = rules.UseTabs ? "\t" : new string(' ', rules.IndentSize);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = i >= startLine && i <= endLine
                ? NormalizeLine(lines[i], indentUnit, rules.UseTabs, rules.IndentSize, rules.TrimTrailingWhitespace)
                : lines[i];

            sb.Append(line);
            if (i < lines.Length - 1) sb.Append(lineEnding);
        }

        if (rules.InsertFinalNewline && !text.EndsWith('\n'))
            sb.Append(lineEnding);

        return sb.ToString();
    }

    // -- Helpers -----------------------------------------------------------------

    private static string Apply(ReadOnlySpan<char> text, int startLine, int endLine, FormattingRules rules)
    {
        var lines = SplitLines(text.ToString(), out string lineEnding);
        string indentUnit = rules.UseTabs ? "\t" : new string(' ', rules.IndentSize);

        var sb = new StringBuilder(text.Length + 64);

        for (int i = 0; i < lines.Length; i++)
        {
            bool inRange = endLine < 0 || (i >= startLine && i <= endLine);
            string line  = inRange
                ? NormalizeLine(lines[i], indentUnit, rules.UseTabs, rules.IndentSize, rules.TrimTrailingWhitespace)
                : lines[i];

            sb.Append(line);
            if (i < lines.Length - 1) sb.Append(lineEnding);
        }

        if (rules.InsertFinalNewline && (lines.Length == 0 || !text.ToString().EndsWith('\n')))
            sb.Append(lineEnding);

        return sb.ToString();
    }

    /// <summary>
    /// Normalises leading whitespace and optionally trims trailing whitespace.
    /// Leading whitespace is converted to the target indent character sequence
    /// while preserving the logical indent level.
    /// </summary>
    private static string NormalizeLine(
        string line,
        string indentUnit,
        bool   useTabs,
        int    indentSize,
        bool   trimTrailing)
    {
        if (line.Length == 0) return line;

        // Count leading whitespace columns.
        int leadingCols = 0;
        for (int j = 0; j < line.Length; j++)
        {
            if      (line[j] == '\t') leadingCols += indentSize;
            else if (line[j] == ' ')  leadingCols++;
            else break;
        }

        int contentStart = line.Length;
        for (int j = 0; j < line.Length; j++)
        {
            if (line[j] != ' ' && line[j] != '\t') { contentStart = j; break; }
        }

        string content = line[contentStart..];
        if (trimTrailing)
            content = content.TrimEnd();

        // Re-emit indent.
        int    levels     = leadingCols / Math.Max(1, indentSize);
        string newIndent  = useTabs
            ? new string('\t', levels)
            : new string(' ', levels * indentSize);

        // If content is blank (only whitespace), return empty line.
        if (content.Length == 0 && trimTrailing) return string.Empty;

        return newIndent + content;
    }

    private static string[] SplitLines(string text, out string lineEnding)
    {
        lineEnding = text.Contains("\r\n") ? "\r\n" : "\n";
        return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    }
}
