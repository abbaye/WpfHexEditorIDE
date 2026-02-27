using WpfHexaEditor.Core.CharacterTable;

namespace WpfHexEditor.TblEditor.Models;

/// <summary>Result of a TBL import operation</summary>
public class TblImportResult
{
    public bool Success { get; set; }
    public List<Dte> Entries { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public TblFileFormat DetectedFormat { get; set; }
}
