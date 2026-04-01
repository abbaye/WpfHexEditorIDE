//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Editor.TblEditor.Models;

/// <summary>
/// Options for CSV export
/// </summary>
public class CsvExportOptions
{
    public bool IncludeType { get; set; } = true;
    public bool IncludeByteCount { get; set; } = true;
    public bool IncludeComment { get; set; } = true;
    public string Delimiter { get; set; } = ",";
    public bool QuoteStrings { get; set; } = true;
    public Encoding Encoding { get; set; } = Encoding.UTF8;
}
