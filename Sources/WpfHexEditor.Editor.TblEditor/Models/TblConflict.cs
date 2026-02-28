using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.Editor.TblEditor.Models;

/// <summary>Represents a conflict detected in TBL file</summary>
public class TblConflict
{
    public ConflictType Type { get; set; }
    public ConflictSeverity Severity { get; set; }
    public List<Dte> ConflictingEntries { get; set; } = [];
    public string? Description { get; set; }
    public string? Suggestion { get; set; }

    public string GetDetailedMessage()
    {
        if (ConflictingEntries.Count == 0)
            return Description ?? string.Empty;

        var entries = string.Join(", ", ConflictingEntries.ConvertAll(e => $"'{e.Entry}'"));
        return $"{Description} - Entries: {entries}";
    }

    public string ConflictingEntriesText =>
        string.Join(", ", ConflictingEntries.ConvertAll(e => e.Entry));
}
