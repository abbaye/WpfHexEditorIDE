namespace WpfHexEditor.TblEditor.Models;

/// <summary>Result of merging TBL entries</summary>
public class TblMergeResult
{
    public List<string> Added { get; set; } = [];
    public List<string> Skipped { get; set; } = [];
    public List<string> Overwritten { get; set; } = [];
    public List<TblMergeConflict> Conflicts { get; set; } = [];

    public string GetSummary() =>
        $"Added: {Added.Count}, Skipped: {Skipped.Count}, Overwritten: {Overwritten.Count}, Conflicts: {Conflicts.Count}";
}

/// <summary>Represents a conflict during merge</summary>
public class TblMergeConflict
{
    public string? HexKey { get; set; }
    public string? ExistingValue { get; set; }
    public string? NewValue { get; set; }
}
