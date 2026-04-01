// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/CodeFormattingService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Orchestrates code formatting for the active document.
//     Strategy: LSP textDocument/formatting (or rangeFormatting) first;
//     fall back to BasicIndentFormatter when no LSP provider is available.
//
//     Returns the formatted text as a string so that CodeEditor can apply it
//     as a single undoable transaction (BeginTransaction / document mutation).
//
// Architecture Notes:
//     Service (stateless) — one instance per CodeEditor.
//     The caller (CodeEditor) is responsible for applying the result to
//     the document inside a BeginTransaction block.
//
//     TextEdit application: LSP edits are applied bottom-up (reverse line/col
//     order) to avoid index drift when edits are earlier in the document.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Formats document text, preferring an LSP server and falling back to
/// <see cref="BasicIndentFormatter"/> when no server capability is available.
/// </summary>
internal sealed class CodeFormattingService
{
    // -- Public API ---------------------------------------------------------------

    /// <summary>
    /// Formats the full document.
    /// Returns the formatted text, or the original <paramref name="text"/> when
    /// no changes are needed.
    /// </summary>
    public async Task<string> FormatDocumentAsync(
        string           filePath,
        string           text,
        LanguageDefinition? language,
        ILspClient?      lspClient,
        int              tabSize,
        bool             insertSpaces,
        CancellationToken ct = default)
    {
        if (lspClient?.IsInitialized == true)
        {
            try
            {
                var edits = await lspClient
                    .FormattingAsync(filePath, tabSize, insertSpaces, ct)
                    .ConfigureAwait(false);

                if (edits.Count > 0)
                    return ApplyEdits(text, edits);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* LSP error — fall through to BasicIndentFormatter */ }
        }

        // Fallback: whitespace-level formatting from whfmt rules.
        return BasicIndentFormatter.FormatDocument(text, language?.FormattingRules);
    }

    /// <summary>
    /// Formats the selected line range [<paramref name="startLine"/>, <paramref name="endLine"/>]
    /// (0-based, inclusive).  LSP range formatting is used when available.
    /// </summary>
    public async Task<string> FormatSelectionAsync(
        string           filePath,
        string           text,
        int              startLine,
        int              startColumn,
        int              endLine,
        int              endColumn,
        LanguageDefinition? language,
        ILspClient?      lspClient,
        int              tabSize,
        bool             insertSpaces,
        CancellationToken ct = default)
    {
        if (lspClient?.IsInitialized == true)
        {
            try
            {
                var edits = await lspClient
                    .RangeFormattingAsync(
                        filePath,
                        startLine, startColumn,
                        endLine,   endColumn,
                        tabSize,   insertSpaces,
                        ct)
                    .ConfigureAwait(false);

                if (edits.Count > 0)
                    return ApplyEdits(text, edits);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* LSP error — fall through */ }
        }

        return BasicIndentFormatter.FormatSelection(text, startLine, endLine, language?.FormattingRules);
    }

    // -- Helpers ------------------------------------------------------------------

    /// <summary>
    /// Applies a list of <see cref="LspTextEdit"/> to <paramref name="text"/>.
    /// Edits are applied in reverse order (bottom-up) to avoid column/line drift.
    /// </summary>
    private static string ApplyEdits(string text, IReadOnlyList<LspTextEdit> edits)
    {
        // Sort descending by start position so we apply bottom-up.
        var sorted = edits
            .OrderByDescending(e => e.StartLine)
            .ThenByDescending(e => e.StartColumn)
            .ToList();

        var lines = new List<string>(text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
        bool crlf = text.Contains("\r\n");

        foreach (var edit in sorted)
        {
            int sl = Math.Max(0, Math.Min(edit.StartLine,   lines.Count - 1));
            int el = Math.Max(0, Math.Min(edit.EndLine,     lines.Count - 1));
            int sc = Math.Max(0, Math.Min(edit.StartColumn, lines[sl].Length));
            int ec = Math.Max(0, Math.Min(edit.EndColumn,   lines[el].Length));

            // Extract prefix (before start) and suffix (after end).
            string prefix = lines[sl][..sc];
            string suffix = lines[el][ec..];

            // Build the replacement content (newText may span multiple lines).
            var newLines = edit.NewText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Remove replaced lines and insert the new content.
            lines.RemoveRange(sl, el - sl + 1);

            if (newLines.Length == 1)
            {
                lines.Insert(sl, prefix + newLines[0] + suffix);
            }
            else
            {
                lines.Insert(sl, prefix + newLines[0]);
                for (int i = 1; i < newLines.Length - 1; i++)
                    lines.Insert(sl + i, newLines[i]);
                lines.Insert(sl + newLines.Length - 1, newLines[^1] + suffix);
            }
        }

        return string.Join(crlf ? "\r\n" : "\n", lines);
    }
}
