//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.Editor.TblEditor.Models;

/// <summary>
/// Result of a TBL import operation
/// </summary>
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
