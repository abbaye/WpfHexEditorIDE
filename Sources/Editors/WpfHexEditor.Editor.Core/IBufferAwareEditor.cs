// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: IBufferAwareEditor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Opt-in contract for text editors that participate in the shared
//     document buffer. Implement alongside IDocumentEditor to enable
//     cross-editor sync and automatic LSP routing.
//
// Architecture Notes:
//     Pattern: Opt-in Extension Interface
//     - DocumentManager calls AttachBuffer() after AttachEditor() when
//       the editor implements this interface and a FilePath is known.
//     - The editor is responsible for the _suppressBufferSync guard to
//       prevent feedback loops when the buffer notifies it of a change
//       it originated itself.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Opt-in contract for text editors that participate in the shared
/// <see cref="Documents.IDocumentBuffer"/>.
/// Implement alongside <see cref="IDocumentEditor"/> to enable
/// cross-editor synchronisation and automatic LSP routing.
/// </summary>
public interface IBufferAwareEditor : IDocumentEditor
{
    /// <summary>
    /// Called by <c>DocumentManager</c> after the editor is registered.
    /// The editor must push its current content into the buffer and subscribe
    /// to <see cref="Documents.IDocumentBuffer.Changed"/> to receive external updates.
    /// </summary>
    void AttachBuffer(Documents.IDocumentBuffer buffer);

    /// <summary>
    /// Called by <c>DocumentManager</c> before the editor tab is closed.
    /// The editor must unsubscribe from the buffer and release its reference.
    /// </summary>
    void DetachBuffer();
}
