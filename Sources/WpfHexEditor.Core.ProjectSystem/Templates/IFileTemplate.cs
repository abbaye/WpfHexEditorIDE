//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.ProjectSystem.Templates;

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

    /// <summary>
    /// Category shown in the left sidebar of the New File dialog
    /// (e.g. "General", "C# / .NET", "Data", "Script", "Web").
    /// </summary>
    string Category => "General";

    /// <summary>
    /// Segoe MDL2 Assets glyph character used as the template icon tile.
    /// </summary>
    string IconGlyph => "\uE8A5";
}
