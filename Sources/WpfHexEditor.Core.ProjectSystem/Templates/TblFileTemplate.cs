//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Core.ProjectSystem.Templates;

/// <summary>
/// Template for a minimal TBL (text-binary lookup) file.
/// </summary>
public sealed class TblFileTemplate : IFileTemplate
{
    public string Name             => "TBL File";
    public string Description      => "Creates a new TBL character-table file (.tbl).";
    public string DefaultExtension => ".tbl";

    public byte[] CreateContent()
    {
        // Minimal valid TBL: one sample mapping entry + an end-of-block marker
        var sb = new StringBuilder();
        sb.AppendLine("00=.");   // placeholder mapping
        sb.AppendLine("/ENDBLOCK");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
