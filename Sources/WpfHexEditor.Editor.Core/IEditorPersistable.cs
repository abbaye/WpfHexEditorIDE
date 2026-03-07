//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Optional interface for document editors that can serialise their
/// per-file state into a project item.
/// <para>
/// Editors that implement this interface allow the project system to
/// persist and restore: the editor configuration (bytes/line, encoding …)
/// and any in-memory byte-level modifications that have not been flushed
/// to the physical file yet.
/// </para>
/// <para>
/// The host checks for this interface via <c>editor is IEditorPersistable</c>
/// before calling; an editor that does not need persistence simply omits it.
/// </para>
/// </summary>
public interface IEditorPersistable
{
    /// <summary>
    /// Returns the current editor configuration as a serialisable DTO.
    /// </summary>
    EditorConfigDto GetEditorConfig();

    /// <summary>
    /// Restores a previously saved editor configuration.
    /// </summary>
    void ApplyEditorConfig(EditorConfigDto config);

    /// <summary>
    /// Returns a compact binary representation of all in-memory modifications
    /// not yet written to the file, or <see langword="null"/> if the buffer is clean.
    /// </summary>
    byte[]? GetUnsavedModifications();

    /// <summary>
    /// Re-applies modifications previously returned by <see cref="GetUnsavedModifications"/>.
    /// </summary>
    void ApplyUnsavedModifications(byte[] data);

    // -- Changeset (WHChg) --------------------------------------------------

    /// <summary>
    /// Returns an immutable snapshot of all pending edits (modify / insert / delete).
    /// Capturing the snapshot is O(e) — only iterates the edit dictionaries, never
    /// the full file content.  Returns <see cref="ChangesetSnapshot.Empty"/> when
    /// the buffer is clean.
    /// </summary>
    ChangesetSnapshot GetChangesetSnapshot();

    /// <summary>
    /// Re-applies edits previously captured with <see cref="GetChangesetSnapshot"/>
    /// and serialised to a <see cref="ChangesetDto"/>.
    /// Typically called when a project item is re-opened and a companion .whchg file
    /// is found alongside the source file.
    /// </summary>
    void ApplyChangeset(ChangesetDto changeset);

    /// <summary>
    /// Marks the current undo-history position as the "clean" baseline after a tracked save
    /// to a .whchg companion file.  The editor will report <c>IsDirty = false</c> until new
    /// byte-level edits are made.  Default implementation is a no-op.
    /// </summary>
    void MarkChangesetSaved();

    // -- Bookmarks ---------------------------------------------------------

    /// <summary>
    /// Returns the current bookmarks as serialisable DTOs, or <see langword="null"/>
    /// if the editor has no bookmark concept.
    /// </summary>
    IReadOnlyList<BookmarkDto>? GetBookmarks();

    /// <summary>
    /// Restores bookmarks previously returned by <see cref="GetBookmarks"/>.
    /// Called when a project item is re-opened.
    /// </summary>
    void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks);
}
