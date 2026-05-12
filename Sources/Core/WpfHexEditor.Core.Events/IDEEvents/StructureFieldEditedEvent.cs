// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/StructureFieldEditedEvent.cs
// Description:
//     Raised when the user edits a field in the StructureEditor's
//     binary-preview pane. HexEditor instances subscribe and update
//     their ByteProvider to reflect the change in real time.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>
/// Notifies that a structure-driven field edit produced new bytes at
/// <see cref="Offset"/> that should propagate to any open HexEditor
/// pointing at <see cref="BinaryFilePath"/>.
/// </summary>
public sealed class StructureFieldEditedEvent
{
    /// <summary>Absolute path of the binary file the structure interprets.</summary>
    public required string  BinaryFilePath { get; init; }

    /// <summary>Zero-based byte offset where the edit applies.</summary>
    public required long    Offset    { get; init; }

    /// <summary>New byte values written at <see cref="Offset"/>.</summary>
    public required byte[]  NewBytes  { get; init; }

    /// <summary>Optional human-readable field name (for status bar / undo entry).</summary>
    public string? FieldName { get; init; }
}
