// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: INavigableDocument.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Opt-in interface for document editors that support line/column
//     navigation (text-based editors: CodeEditor, TextEditor, etc.).
//     Binary editors (HexEditor, ImageViewer…) need not implement this.
//
// Architecture Notes:
//     Pattern: Opt-in interface (same pattern as IOpenableDocument)
//     - The host calls `editor is INavigableDocument nav && nav.NavigateTo(…)`
//     - Used by DocumentHostService.ActivateAndNavigateTo for ErrorList navigation
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Opt-in interface for document editors that support line/column navigation.
/// Implemented by text-based editors (CodeEditor, TextEditor, ScriptEditor).
/// </summary>
public interface INavigableDocument
{
    /// <summary>
    /// Scrolls the editor to the specified 1-based line and column position
    /// and places the caret there.
    /// </summary>
    /// <param name="line">1-based line number.</param>
    /// <param name="column">1-based column number. Pass 1 to jump to the start of the line.</param>
    void NavigateTo(int line, int column);
}
