//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;

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
    /// Primary category shown in the left sidebar of the New File dialog
    /// (e.g. "General", "C# / .NET", "Data", "Script", "Web", "ROM Hacking").
    /// </summary>
    string Category => "General";

    /// <summary>
    /// All sidebar categories this template appears under.
    /// Override when a template should appear in multiple categories
    /// (e.g. a game script is listed in both "Script" and "ROM Hacking").
    /// Defaults to <c>[Category]</c>.
    /// </summary>
    IReadOnlyList<string> Categories => [Category];

    /// <summary>
    /// Segoe MDL2 Assets glyph character used as the template icon tile.
    /// </summary>
    string IconGlyph => "\uE8A5";
}
