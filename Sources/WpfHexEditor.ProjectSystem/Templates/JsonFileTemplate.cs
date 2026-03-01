//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.ProjectSystem.Templates;

/// <summary>Template for an empty JSON file.</summary>
public sealed class JsonFileTemplate : IFileTemplate
{
    public string Name             => "JSON File";
    public string Description      => "Creates a new empty JSON file (.json).";
    public string DefaultExtension => ".json";

    public byte[] CreateContent()
        => Encoding.UTF8.GetBytes("{\n}\n");
}
