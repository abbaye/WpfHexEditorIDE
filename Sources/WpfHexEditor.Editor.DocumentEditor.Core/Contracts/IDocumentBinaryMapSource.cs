// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Contracts/IDocumentBinaryMapSource.cs
// Description:
//     Optional interface for editors that expose a BinaryMap for
//     hex-selection bridging. Implemented by DocumentEditorHost.
//     Any editor or panel can query this interface to navigate
//     to a specific document block from a byte offset.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Contracts;

/// <summary>
/// Implemented by editors that expose a binary map for hex selection bridging.
/// Allows external components (e.g. HexEditor, ParsedFields) to resolve
/// byte offsets to document structure blocks and vice versa.
/// </summary>
public interface IDocumentBinaryMapSource
{
    /// <summary>
    /// The current document's binary map, or <see langword="null"/>
    /// when no document is loaded.
    /// </summary>
    BinaryMap.BinaryMap? BinaryMap { get; }

    /// <summary>
    /// Raised when the binary map is rebuilt (after a document reload or undo/redo).
    /// Consumers should re-query <see cref="BinaryMap"/> after this event.
    /// </summary>
    event EventHandler? BinaryMapRebuilt;
}
