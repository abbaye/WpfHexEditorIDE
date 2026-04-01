//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.TblEditor.Models;

/// <summary>
/// Options for JSON import
/// </summary>
public class JsonImportOptions
{
    public bool AutoDetectType { get; set; } = true;
    public bool SkipInvalidEntries { get; set; } = true;
    public string HexPropertyName { get; set; } = "hex";
    public string ValuePropertyName { get; set; } = "value";
}
