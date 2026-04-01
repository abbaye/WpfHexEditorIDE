// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: ExternalFileChangedEventArgs.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-16
// Description:
//     EventArgs raised when the file currently open in HexEditor
//     has been modified by an external process.
//
// Architecture Notes:
//     Raised on the UI thread (Dispatcher.BeginInvoke) from HexEditor.FileOperations.cs.
//     Consumers can use HasUnsavedChanges to decide whether to auto-reload or prompt.
// ==========================================================

using System;

namespace WpfHexEditor.HexEditor;

/// <summary>
/// Provides data for the <see cref="HexEditor.FileExternallyChanged"/> event.
/// Raised when the file currently open in the HexEditor is modified by an external process.
/// </summary>
public sealed class ExternalFileChangedEventArgs : EventArgs
{
    /// <summary>Gets the full path of the file that was modified.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets whether the editor has unsaved in-memory changes at the time of detection.
    /// When true the user should be prompted before reloading to avoid data loss.
    /// </summary>
    public bool HasUnsavedChanges { get; }

    internal ExternalFileChangedEventArgs(string filePath, bool hasUnsavedChanges)
    {
        FilePath          = filePath;
        HasUnsavedChanges = hasUnsavedChanges;
    }
}
