using WpfHexaEditor.Core.CharacterTable;

namespace WpfHexEditor.TblEditor.Models;

/// <summary>Complete .tblx document structure</summary>
public class TblxDocument
{
    public string Format { get; set; } = "tblx";
    public TblxMetadata Metadata { get; set; } = new();
    public List<TblxEntry> Entries { get; set; } = [];

    public static TblxDocument FromTblStream(TblStream tbl, TblxMetadata? metadata = null) => new()
    {
        Metadata = metadata ?? new TblxMetadata(),
        Entries = tbl.GetAllEntries().Select(d => TblxEntry.FromDte(d)).ToList()
    };

    public TblStream ToTblStream()
    {
        var tbl = new TblStream();
        foreach (var entry in Entries)
            tbl.Add(entry.ToDte());
        return tbl;
    }

    public List<TblxEntry> GetEntriesByCategory(string category) =>
        Entries.Where(e => e.Category == category).ToList();

    public List<string> GetCategories() =>
        Entries.Where(e => !string.IsNullOrWhiteSpace(e.Category))
               .Select(e => e.Category!)
               .Distinct().OrderBy(c => c).ToList();
}
