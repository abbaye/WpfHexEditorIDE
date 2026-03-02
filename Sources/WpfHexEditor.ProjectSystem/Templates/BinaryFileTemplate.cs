//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.ProjectSystem.Templates;

/// <summary>
/// Template for an empty binary file.
/// </summary>
public sealed class BinaryFileTemplate : IFileTemplate
{
    public string Name             => "Binary File";
    public string Description      => "Creates a new empty binary file (.bin).";
    public string DefaultExtension => ".bin";
    public byte[] CreateContent()  => [];
}
