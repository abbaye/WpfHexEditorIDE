//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

/// <summary>
/// Template for a new Assembly source file (.asm).
/// Creates a minimal stub with section directives and a comment header
/// ready to be opened in the TextEditor.
/// </summary>
public sealed class AsmFileTemplate : IFileTemplate
{
    public string Name             => "Assembly File";
    public string Description      => "Creates a new Assembly source file (.asm) with a minimal stub.";
    public string DefaultExtension => ".asm";
    public string Category         => "Script";
    public string IconGlyph        => "\uE943";

    public byte[] CreateContent()
    {
        const string stub =
            "; Assembly source file\n" +
            "; Created by WpfHexEditor\n" +
            "\n" +
            "    .org $0000\n" +
            "\n" +
            "main:\n" +
            "    ; TODO: add your code here\n" +
            "    nop\n";

        return Encoding.UTF8.GetBytes(stub);
    }
}
