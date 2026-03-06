// ==========================================================
// Project: WpfHexEditor.SDK
// File: IDocument.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Abstraction representing the currently active document in the IDE.
//     Provides plugin-safe access without exposing WPF internals.
//
// Architecture Notes:
//     Used by IFocusContextService to notify plugins of document changes.
//     Implemented by the App layer (MainWindow) adapting the concrete editor.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Focus;

/// <summary>
/// Represents an active document visible to plugins.
/// </summary>
public interface IDocument
{
    /// <summary>Gets the document unique content identifier.</summary>
    string ContentId { get; }

    /// <summary>Gets the document title displayed in the tab.</summary>
    string Title { get; }

    /// <summary>Gets the file path if the document is backed by a file; otherwise null.</summary>
    string? FilePath { get; }

    /// <summary>Gets the document type category (e.g. "hex", "code", "image").</summary>
    string DocumentType { get; }

    /// <summary>Gets whether the document is currently dirty (has unsaved changes).</summary>
    bool IsDirty { get; }
}
