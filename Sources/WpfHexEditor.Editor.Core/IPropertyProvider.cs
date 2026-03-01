//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Provides a context-aware, categorised list of properties for display in
/// the Properties panel (F4).
/// <para>
/// Implementations live in the editor assemblies (HexEditor, TblEditor,
/// JsonEditor) and in the ProjectSystem assembly (for project items).
/// They are obtained either via <see cref="IPropertyProviderSource"/> on a
/// document editor or constructed directly by the host for non-editor contexts
/// (e.g. a project item selected in the Solution Explorer).
/// </para>
/// </summary>
public interface IPropertyProvider
{
    /// <summary>
    /// Short description shown in the type-selector ComboBox at the top of the
    /// Properties panel (e.g. "game.bin — Byte at 0x000000FF").
    /// </summary>
    string ContextLabel { get; }

    /// <summary>
    /// Returns the current property groups. Called every time
    /// <see cref="PropertiesChanged"/> fires and on first attach.
    /// The list may be rebuilt from scratch on each call.
    /// </summary>
    IReadOnlyList<PropertyGroup> GetProperties();

    /// <summary>Raised when the underlying selection or state changes and the panel should refresh.</summary>
    event EventHandler? PropertiesChanged;
}
