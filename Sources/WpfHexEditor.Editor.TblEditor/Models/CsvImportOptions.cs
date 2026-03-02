//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;

namespace WpfHexEditor.Editor.TblEditor.Models;

/// <summary>
/// Options for CSV import
/// </summary>
public class CsvImportOptions
{
    public string Delimiter { get; set; } = ",";
    public bool HasHeader { get; set; } = true;
    public bool AutoDetectType { get; set; } = true;
    public bool SkipInvalidRows { get; set; } = true;
    public Encoding Encoding { get; set; } = Encoding.UTF8;
}
