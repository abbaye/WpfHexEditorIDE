//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.ProjectSystem.Templates;

/// <summary>
/// Contract for a file template that can create a new project item.
/// Register instances in <see cref="FileTemplateRegistry"/>.
/// </summary>
public interface IFileTemplate
{
    /// <summary>
    /// Display name shown in the New File dialog.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Brief description of what this template creates.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Default file extension (including the dot, e.g. ".bin").
    /// </summary>
    string DefaultExtension { get; }

    /// <summary>
    /// Initial content for the new file (may be empty).
    /// Returns a new array on every call.
    /// </summary>
    byte[] CreateContent();
}
