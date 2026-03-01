//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.ProjectSystem.Templates;

/// <summary>
/// Template for a minimal WpfHexEditor format-definition file (.whjson).
/// The generated boilerplate is a valid (but empty) format definition that
/// the user can edit in the JSON editor.
/// </summary>
public sealed class WhjsonFileTemplate : IFileTemplate
{
    public string Name             => "Format Definition";
    public string Description      => "Creates a new WpfHexEditor format-definition file (.whjson).";
    public string DefaultExtension => ".whjson";

    public byte[] CreateContent()
    {
        var boilerplate =
            """
            {
              "formatName": "MyFormat",
              "version": "1.0",
              "extensions": [ ".bin" ],
              "description": "",
              "author": "",
              "category": "Custom",
              "detection": {
                "signatures": []
              },
              "blocks": []
            }
            """;
        return Encoding.UTF8.GetBytes(boilerplate);
    }
}
