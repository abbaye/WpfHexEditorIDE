// ==========================================================
// Project: WpfHexEditor.SDK
// File: ICodeEditorService.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Plugin-facing service for interacting with the active CodeEditor.
//     Provides read access to content, cursor position, and language.
//
// Architecture Notes:
//     Implemented by CodeEditorServiceImpl in App/Services.
//     Requires PluginPermission.AccessCodeEditor to be granted.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Provides access to the active CodeEditor for plugins.
/// Requires <c>AccessCodeEditor</c> permission.
/// </summary>
public interface ICodeEditorService
{
    /// <summary>Gets whether a CodeEditor tab is currently active.</summary>
    bool IsActive { get; }

    /// <summary>Gets the language identifier of the active document (e.g. "json", "csharp").</summary>
    string? CurrentLanguage { get; }

    /// <summary>Gets the file path of the active code document, or null.</summary>
    string? CurrentFilePath { get; }

    /// <summary>Gets the full text content of the active code document.</summary>
    string? GetContent();

    /// <summary>Gets the text of the current selection, or empty string if no selection.</summary>
    string GetSelectedText();

    /// <summary>Gets the current caret line (1-based).</summary>
    int CaretLine { get; }

    /// <summary>Gets the current caret column (1-based).</summary>
    int CaretColumn { get; }

    /// <summary>
    /// Raised when the active code document changes (new file opened, tab switched).
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler DocumentChanged;
}
