//////////////////////////////////////////////
// Apache 2.0  - 2026
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
    /// <summary>Returns the current editor configuration as a serialisable DTO.</summary>
    EditorConfigDto GetEditorConfig();

    /// <summary>Restores a previously saved editor configuration.</summary>
    void ApplyEditorConfig(EditorConfigDto config);

    /// <summary>
    /// Returns a compact binary representation of all in-memory modifications
    /// not yet written to the file, or <see langword="null"/> if the buffer is clean.
    /// </summary>
    byte[]? GetUnsavedModifications();

    /// <summary>Re-applies modifications previously returned by <see cref="GetUnsavedModifications"/>.</summary>
    void ApplyUnsavedModifications(byte[] data);
}
