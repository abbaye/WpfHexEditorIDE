using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.TblEditor.Models;

/// <summary>Extended TBL entry with additional metadata for .tblx format</summary>
public class TblxEntry
{
    public string? Entry { get; set; }
    public string? Value { get; set; }
    public string? Type { get; set; }
    public int ByteCount { get; set; }
    public string? Category { get; set; }
    public string? Comment { get; set; }
    public int? Frequency { get; set; }
    public bool IsFavorite { get; set; }

    public static TblxEntry FromDte(Dte dte, string? category = null) => new()
    {
        Entry = dte.Entry,
        Value = dte.Value,
        Type = dte.Type.ToString(),
        ByteCount = dte.Entry.Length / 2,
        Comment = dte.Comment,
        Category = category
    };

    public Dte ToDte()
    {
        var dte = new Dte(Entry ?? string.Empty, Value ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(Comment))
            dte.Comment = Comment;
        return dte;
    }
}
