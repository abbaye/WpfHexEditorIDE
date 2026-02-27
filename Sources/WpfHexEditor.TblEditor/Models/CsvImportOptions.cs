using System.Text;

namespace WpfHexEditor.TblEditor.Models;

/// <summary>Options for CSV import</summary>
public class CsvImportOptions
{
    public string Delimiter { get; set; } = ",";
    public bool HasHeader { get; set; } = true;
    public bool AutoDetectType { get; set; } = true;
    public bool SkipInvalidRows { get; set; } = true;
    public Encoding Encoding { get; set; } = Encoding.UTF8;
}
