// Project      : WpfHexEditorControl
// File         : CompareFileRequestedEventArgs.cs
// Description  : EventArgs for CompareFileRequested / CompareSelectionRequested events
//                raised by HexEditor context menu items.
// Architecture : Simple POCO; consumed by MainWindow to delegate to CompareFileLaunchService.

using System;

namespace WpfHexEditor.HexEditor;

/// <summary>
/// Provides data for the <see cref="HexEditor.CompareFileRequested"/> and
/// <see cref="HexEditor.CompareSelectionRequested"/> events.
/// </summary>
public sealed class CompareFileRequestedEventArgs : EventArgs
{
    /// <summary>
    /// The file path to use as the left side of the comparison.
    /// For <c>CompareFileRequested</c> this is the currently open file path.
    /// For <c>CompareSelectionRequested</c> this is a temp file containing the selected bytes.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// When <c>true</c> the <see cref="FilePath"/> is a temporary file that should be
    /// tracked for cleanup by <c>CompareFileLaunchService.TrackTempFile()</c>.
    /// </summary>
    public bool IsTempFile { get; init; }

    public CompareFileRequestedEventArgs(string? filePath)
    {
        FilePath = filePath;
    }
}
