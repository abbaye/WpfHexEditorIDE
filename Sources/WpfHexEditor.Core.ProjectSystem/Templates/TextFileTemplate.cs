//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

/// <summary>
/// Template for an empty text file.
/// </summary>
public sealed class TextFileTemplate : IFileTemplate
{
    public string Name             => "Text File";
    public string Description      => "Creates a new empty text file (.txt).";
    public string DefaultExtension => ".txt";
    public string Category         => "General";
    public string IconGlyph        => "\uE8A5";

    public byte[] CreateContent()
        => Encoding.UTF8.GetBytes("");
}
