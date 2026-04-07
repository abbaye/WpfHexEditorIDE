// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Models/LineChangeKind.cs
// Description:
//     Represents the change state of a line relative to the last save-point.
//     Used by GutterChangeTracker and ChangeMarkerGutterControl.
// Architecture: Plain enum — no dependencies.
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.Models;

/// <summary>
/// The change state of a code line relative to the document save-point.
/// </summary>
internal enum LineChangeKind
{
    /// <summary>Line is unchanged since the last save.</summary>
    None,

    /// <summary>Line was added after the last save (not present in saved snapshot).</summary>
    Added,

    /// <summary>Line exists in the saved snapshot but its text has changed.</summary>
    Modified,

    /// <summary>
    /// One or more lines were deleted immediately after this line since the last save.
    /// Rendered as a small deletion indicator on the predecessor line.
    /// </summary>
    Deleted,
}
