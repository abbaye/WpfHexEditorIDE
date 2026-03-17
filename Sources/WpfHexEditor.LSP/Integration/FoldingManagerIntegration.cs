// ==========================================================
// Project: WpfHexEditor.LSP
// File: Integration/FoldingManagerIntegration.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Bridges LSP ParseResult data with the CodeEditor's FoldingEngine.
//     When a document is parsed, computes language-specific fold regions
//     from the token stream and forwards them to the FoldingEngine.
//
// Architecture Notes:
//     Pattern: Adapter
//     - Converts LSP tokens (block-comment start/end, indent levels) to
//       FoldingRegion objects understood by the existing FoldingEngine.
//     - Called by the LSP pipeline after each IncrementalParser update.
// ==========================================================

using WpfHexEditor.Editor.CodeEditor.Folding;
using WpfHexEditor.LSP.Parsing;

namespace WpfHexEditor.LSP.Integration;

/// <summary>
/// Computes fold regions from a <see cref="ParseResult"/> and updates a
/// <see cref="FoldingEngine"/> instance.
/// </summary>
public sealed class FoldingManagerIntegration
{
    private readonly LanguageDefinitionManager _languages;

    public FoldingManagerIntegration(LanguageDefinitionManager languages)
    {
        _languages = languages ?? throw new ArgumentNullException(nameof(languages));
    }

    /// <summary>
    /// Triggers a fold re-analysis on <paramref name="engine"/> using line text
    /// reconstructed from the parse result tokens.
    /// The engine's strategy is assumed to be set correctly by its owner (CodeEditor).
    /// </summary>
    public void UpdateFolding(
        FoldingEngine  engine,
        ParseResult    parseResult,
        string         languageId)
    {
        // FoldingEngine expects a list of CodeLine objects.
        int maxLine = parseResult.TokensByLine.Keys.DefaultIfEmpty(0).Max();
        var lines   = BuildCodeLines(parseResult, maxLine + 1);

        engine.Analyze(lines);
    }

    // -----------------------------------------------------------------------

    private static List<WpfHexEditor.Editor.CodeEditor.Models.CodeLine> BuildCodeLines(
        ParseResult result, int count)
    {
        var lines = new List<WpfHexEditor.Editor.CodeEditor.Models.CodeLine>(count);
        for (int i = 0; i < count; i++)
        {
            var tokens = result.TokensByLine.TryGetValue(i, out var t) ? t : [];
            var text   = string.Concat(tokens.Select(tk => tk.Text));
            lines.Add(new WpfHexEditor.Editor.CodeEditor.Models.CodeLine(text, i));
        }
        return lines;
    }
}
