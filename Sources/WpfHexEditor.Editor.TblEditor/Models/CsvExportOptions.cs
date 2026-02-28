using System.Text;

namespace WpfHexEditor.TblEditor.Models;

/// <summary>Options for CSV export</summary>
public class CsvExportOptions
{
    public bool IncludeType { get; set; } = true;
    public bool IncludeByteCount { get; set; } = true;
    public bool IncludeComment { get; set; } = true;
    public string Delimiter { get; set; } = ",";
    public bool QuoteStrings { get; set; } = true;
    public Encoding Encoding { get; set; } = Encoding.UTF8;
}
