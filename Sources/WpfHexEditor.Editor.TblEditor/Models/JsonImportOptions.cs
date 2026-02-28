namespace WpfHexEditor.Editor.TblEditor.Models;

/// <summary>Options for JSON import</summary>
public class JsonImportOptions
{
    public bool AutoDetectType { get; set; } = true;
    public bool SkipInvalidEntries { get; set; } = true;
    public string HexPropertyName { get; set; } = "hex";
    public string ValuePropertyName { get; set; } = "value";
}
