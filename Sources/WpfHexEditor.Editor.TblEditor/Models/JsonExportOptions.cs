namespace WpfHexEditor.TblEditor.Models;

/// <summary>Options for JSON export</summary>
public class JsonExportOptions
{
    public bool IncludeType { get; set; } = true;
    public bool IncludeByteCount { get; set; } = true;
    public bool IncludeComment { get; set; } = true;
    public bool Indented { get; set; } = true;
    public string HexPropertyName { get; set; } = "hex";
    public string ValuePropertyName { get; set; } = "value";
    public bool IncludeMetadata { get; set; } = false;
    public JsonMetadata? Metadata { get; set; }
}

/// <summary>Metadata for JSON export</summary>
public class JsonMetadata
{
    public string Version { get; set; } = "1.0";
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? CreatedDate { get; set; }
}
