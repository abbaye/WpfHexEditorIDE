// ==========================================================
// Project: WpfHexEditor.Core.LSP
// File: Formatting/CodeFormatter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Language-aware code formatter.
//     Applies indentation normalisation and optional line-length wrapping
//     to a text document, guided by the active LanguageDefinition.
//
// Architecture Notes:
//     Pattern: Strategy — formatting logic is delegated to per-language rules.
//     Format operations return TextEdit records (minimal diff) rather than
//     the fully formatted string, so the CodeEditor can apply them via the
//     undo/redo stack without replacing the entire document.
// ==========================================================

using WpfHexEditor.Core.LSP.Models;

namespace WpfHexEditor.Core.LSP.Formatting;

/// <summary>
/// Produces a list of <see cref="TextEdit"/> records that, when applied,
/// normalise indentation in a document according to <paramref name="options"/>.
/// </summary>
public sealed class CodeFormatter
{
    private readonly LanguageDefinitionManager _languageManager;

    public CodeFormatter(LanguageDefinitionManager languageManager)
        => _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));

    // -----------------------------------------------------------------------
    // API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Formats the entire <paramref name="sourceText"/> document.
    /// Returns a list of <see cref="TextEdit"/> describing the minimal changes needed.
    /// </summary>
    public IReadOnlyList<TextEdit> FormatDocument(
        string         filePath,
        string         sourceText,
        FormattingOptions options)
    {
        var language  = _languageManager.GetByFilePath(filePath);
        var lines     = sourceText.Split('\n');
        var edits     = new List<TextEdit>();
        int indent    = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var original  = lines[i].TrimEnd('\r');
            var trimmed   = original.TrimStart();

            // Close brace/bracket — dedent before this line.
            if (language is not null && StartsWithCloser(language, trimmed))
                indent = Math.Max(0, indent - 1);

            var expected = BuildIndent(indent, options) + trimmed;

            if (expected != original)
            {
                edits.Add(new TextEdit(
                    StartLine:   i,
                    StartColumn: 0,
                    EndLine:     i,
                    EndColumn:   original.Length,
                    NewText:     expected));
            }

            // Open brace/bracket — indent next line.
            if (language is not null && EndsWithOpener(language, trimmed))
                indent++;
        }

        return edits;
    }

    /// <summary>
    /// Formats a specific line range (inclusive).
    /// </summary>
    public IReadOnlyList<TextEdit> FormatRange(
        string            filePath,
        string            sourceText,
        int               startLine,
        int               endLine,
        FormattingOptions options)
    {
        var language = _languageManager.GetByFilePath(filePath);
        var lines    = sourceText.Split('\n');
        var edits    = new List<TextEdit>();

        // Compute indent level at startLine by scanning prior lines.
        int indent = ComputeIndentAt(lines, startLine, language);

        for (int i = startLine; i <= endLine && i < lines.Length; i++)
        {
            var original = lines[i].TrimEnd('\r');
            var trimmed  = original.TrimStart();

            if (language is not null && StartsWithCloser(language, trimmed))
                indent = Math.Max(0, indent - 1);

            var expected = BuildIndent(indent, options) + trimmed;

            if (expected != original)
            {
                edits.Add(new TextEdit(i, 0, i, original.Length, expected));
            }

            if (language is not null && EndsWithOpener(language, trimmed))
                indent++;
        }

        return edits;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string BuildIndent(int depth, FormattingOptions options)
        => options.UseSpaces
            ? new string(' ', depth * options.IndentSize)
            : new string('\t', depth);

    private static bool StartsWithCloser(LanguageDefinition lang, string trimmed)
        => trimmed.StartsWith('}') || trimmed.StartsWith(']') || trimmed.StartsWith(')');

    private static bool EndsWithOpener(LanguageDefinition lang, string trimmed)
        => trimmed.EndsWith('{') || trimmed.EndsWith('[') || trimmed.EndsWith('(');

    private static int ComputeIndentAt(string[] lines, int targetLine, LanguageDefinition? lang)
    {
        int indent = 0;
        for (int i = 0; i < targetLine && i < lines.Length; i++)
        {
            var t = lines[i].Trim();
            if (lang != null && EndsWithOpener(lang, t)) indent++;
            if (lang != null && StartsWithCloser(lang, t)) indent = Math.Max(0, indent - 1);
        }
        return indent;
    }
}

// -----------------------------------------------------------------------
// Supporting types
// -----------------------------------------------------------------------

/// <summary>Describes a single text replacement in a document.</summary>
public sealed record TextEdit(
    int    StartLine,
    int    StartColumn,
    int    EndLine,
    int    EndColumn,
    string NewText);

/// <summary>Options controlling code formatting behaviour.</summary>
public sealed class FormattingOptions
{
    /// <summary><c>true</c> to use spaces; <c>false</c> to use tabs.</summary>
    public bool UseSpaces  { get; set; } = true;

    /// <summary>Number of spaces per indent level when <see cref="UseSpaces"/> is <c>true</c>.</summary>
    public int  IndentSize { get; set; } = 4;

    /// <summary>Maximum line length for soft-wrap suggestions (0 = disabled).</summary>
    public int  MaxLineLength { get; set; } = 120;
}
