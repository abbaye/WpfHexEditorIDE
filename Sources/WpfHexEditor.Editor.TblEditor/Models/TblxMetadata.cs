namespace WpfHexEditor.TblEditor.Models;

/// <summary>Metadata for .tblx extended format</summary>
public class TblxMetadata
{
    public string Version { get; set; } = "1.0";
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public GameInfo? Game { get; set; }
    public string? Encoding { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, string> CustomProperties { get; set; } = [];
    public ValidationRules? Validation { get; set; }
}

/// <summary>Game information for .tblx metadata</summary>
public class GameInfo
{
    public string? Title { get; set; }
    public string? Platform { get; set; }
    public string? Region { get; set; }
    public string? Version { get; set; }
    public int? ReleaseYear { get; set; }
    public string? Developer { get; set; }
}

/// <summary>Validation rules for .tblx entries</summary>
public class ValidationRules
{
    public int? MinByteLength { get; set; }
    public int? MaxByteLength { get; set; }
    public List<string>? AllowedRanges { get; set; }
    public List<string>? ForbiddenValues { get; set; }
    public bool RequireUniqueEntries { get; set; } = true;
    public bool AllowMultiByte { get; set; } = true;
    public int MaxMultiByteLength { get; set; } = 8;
}
