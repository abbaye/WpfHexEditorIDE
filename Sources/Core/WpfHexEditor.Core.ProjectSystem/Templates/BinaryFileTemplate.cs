//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.ProjectSystem.Templates;

/// <summary>
/// Template for an empty binary file.
/// </summary>
public sealed class BinaryFileTemplate : IFileTemplate
{
    public string Name             => "Binary File";
    public string Description      => "Creates a new empty binary file (.bin).";
    public string DefaultExtension => ".bin";
    public string Category         => "General";
    public string IconGlyph        => "\uE8A5";
    public byte[] CreateContent()  => [];
}
