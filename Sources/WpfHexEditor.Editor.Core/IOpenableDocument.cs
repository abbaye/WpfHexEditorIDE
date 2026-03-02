//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Opt-in interface for document editors that can open a file from a path.
/// The host calls <see cref="OpenAsync"/> after creating the editor via
/// <see cref="IEditorFactory.Create"/> to load the requested file.
/// Editors that work with non-file content (in-memory buffers, streams) need
/// not implement this; the host falls back to its own loading logic.
/// </summary>
public interface IOpenableDocument
{
    Task OpenAsync(string filePath, CancellationToken ct = default);
}
