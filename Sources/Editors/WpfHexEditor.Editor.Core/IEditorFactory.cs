//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Factory for registering an editor in the <see cref="IEditorRegistry"/>
/// for plug-in integration with the docking system.
///
/// <para>Optional usage: an editor can be instantiated directly
/// without going through this interface (<c>new TblEditor()</c>). The factory is
/// only needed if the editor must be discoverable via the registry (automatic opening
/// by extension, "Open with…" menu, etc.).</para>
///
/// <para>CONTRACT: instances returned by <see cref="Create"/> must also
/// be <c>System.Windows.FrameworkElement</c> to be embeddable in the
/// WPF docking system. The host casts after creation.</para>
/// </summary>
public interface IEditorFactory
{
    /// <summary>
    /// Editor metadata (id, name, extensions).
    /// </summary>
    IEditorDescriptor Descriptor { get; }

    /// <summary>
    /// Returns <c>true</c> if this editor can open <paramref name="filePath"/>.
    /// Based on the file extension and/or a quick file inspection.
    /// </summary>
    bool CanOpen(string filePath);

    /// <summary>
    /// Creates a new blank editor instance.
    /// Call <see cref="IDocumentEditor"/> members on the result to load a file.
    /// </summary>
    IDocumentEditor Create();
}
