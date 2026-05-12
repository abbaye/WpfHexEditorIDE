// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Providers/ICodeActionProvider.cs
// Description: Plug-in contract for contributing inline Code Actions (Ctrl+.)
//              alongside the LSP-driven flow. Returns LspCodeAction objects so
//              the existing ApplyWorkspaceEdit pipeline can apply the result
//              unchanged.
// Architecture Notes:
//     Decouples the App layer (Code Analysis, future contributors) from the
//     editor — they produce LspCodeAction without depending on the editor's
//     internals. Registered globally via CodeActionRegistry so any number of
//     editors share the same provider list.
// ==========================================================

using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Providers;

/// <summary>
/// Contributes Code Actions (quick fixes / refactors) for the line at the caret.
/// Implementations should return an empty list quickly when they have nothing —
/// they are called on every lightbulb tick (debounced ~600ms).
/// </summary>
public interface ICodeActionProvider
{
    /// <summary>
    /// Returns the code actions available at the given caret position.
    /// Coordinates are 0-based, matching the LSP convention used by the editor.
    /// </summary>
    /// <param name="filePath">Absolute path of the active document.</param>
    /// <param name="line">0-based caret line.</param>
    /// <param name="column">0-based caret column.</param>
    Task<IReadOnlyList<LspCodeAction>> GetCodeActionsAsync(
        string filePath, int line, int column, CancellationToken ct);
}
