// ==========================================================
// Project: WpfHexEditor.Events
// File: IDEEvents/CodeEditorEvents.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     IDE-level events published by EditorEventAdapter when the CodeEditor
//     (or any text-based IDocumentEditor) fires its internal events.
//     These complement the existing hex-editor events (EditorSelectionChangedEvent,
//     FileOpenedEvent, FileClosedEvent, DocumentSavedEvent) with text-editor
//     specific payloads (line/column, text selection, diagnostics summary).
//
// Architecture Notes:
//     Pattern: Domain Event (record types, immutable)
//     All records inherit IDEEventBase for IDEEventBus compatibility.
// ==========================================================

namespace WpfHexEditor.Events.IDEEvents;

// ── Cursor / selection ─────────────────────────────────────────────────────

/// <summary>
/// Published when the caret moves in a text-based editor (CodeEditor, TextEditor).
/// </summary>
public sealed record CodeEditorCursorMovedEvent : IDEEventBase
{
    /// <summary>Absolute path of the open file. Empty when no file is open.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>1-based line number of the new caret position.</summary>
    public int Line { get; init; } = 1;

    /// <summary>1-based column number of the new caret position.</summary>
    public int Column { get; init; } = 1;
}

/// <summary>
/// Published when the text selection changes in a text-based editor.
/// </summary>
public sealed record CodeEditorTextSelectionChangedEvent : IDEEventBase
{
    /// <summary>Absolute path of the open file. Empty when no file is open.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Currently selected text (bounded to 4 096 characters to avoid large bus payloads).
    /// Empty string when nothing is selected.
    /// </summary>
    public string SelectedText { get; init; } = string.Empty;

    /// <summary>Character offset of the selection start (0-based).</summary>
    public int SelectionStart { get; init; }

    /// <summary>Length of the selection in characters.</summary>
    public int SelectionLength { get; init; }
}

// ── Document lifecycle ─────────────────────────────────────────────────────

/// <summary>
/// Published when a document tab is opened in the CodeEditor.
/// </summary>
public sealed record CodeEditorDocumentOpenedEvent : IDEEventBase
{
    /// <summary>Absolute path of the opened file.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Language ID resolved by the syntax highlighter (e.g. "csharp", "json").</summary>
    public string LanguageId { get; init; } = string.Empty;
}

/// <summary>
/// Published when a document tab is closed in the CodeEditor.
/// </summary>
public sealed record CodeEditorDocumentClosedEvent : IDEEventBase
{
    /// <summary>Absolute path of the closed file.</summary>
    public string FilePath { get; init; } = string.Empty;
}

// ── Diagnostics ────────────────────────────────────────────────────────────

/// <summary>
/// Published when the CodeEditor's diagnostic engine completes a validation pass.
/// Consumers (ErrorList, StatusBar) should re-query diagnostics from the editor.
/// </summary>
public sealed record CodeEditorDiagnosticsUpdatedEvent : IDEEventBase
{
    /// <summary>Absolute path of the validated file.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Total number of error-level diagnostics.</summary>
    public int ErrorCount { get; init; }

    /// <summary>Total number of warning-level diagnostics.</summary>
    public int WarningCount { get; init; }
}

// ── Folding ────────────────────────────────────────────────────────────────

/// <summary>
/// Published when the folding state of the document changes (fold/unfold/collapse all).
/// </summary>
public sealed record CodeEditorFoldingChangedEvent : IDEEventBase
{
    /// <summary>Absolute path of the file whose folding state changed.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Number of currently collapsed fold regions.</summary>
    public int CollapsedCount { get; init; }
}
