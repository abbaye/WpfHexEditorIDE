//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

/// <summary>
/// Template for a minimal WpfHexEditor format-definition file (.whfmt).
/// The generated boilerplate is a valid (but empty) format definition that
/// the user can edit in the JSON editor.
/// </summary>
public sealed class WhfmtFileTemplate : IFileTemplate
{
    public string Name             => "Format Definition";
    public string Description      => "Creates a new WpfHexEditor format-definition file (.whfmt).";
    public string DefaultExtension => ".whfmt";
    public string Category         => "General";
    public string IconGlyph        => "\uE8F4";

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
