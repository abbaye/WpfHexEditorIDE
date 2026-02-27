using WpfHexaEditor.Core.CharacterTable;

namespace WpfHexEditor.TblEditor.Models;

/// <summary>Template for pre-defined TBL character tables</summary>
public class TblTemplate
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string Author { get; set; } = "Built-in";
    public string? Category { get; set; }
    public DefaultCharacterTableType? DefaultType { get; set; }
    public string? TblContent { get; set; }
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>Load template into a new TblStream</summary>
    public TblStream? Load()
    {
        if (DefaultType.HasValue)
            return TblStream.CreateDefaultTbl(DefaultType.Value);

        if (!string.IsNullOrEmpty(TblContent))
        {
            var tbl = new TblStream();
            tbl.Load(TblContent);
            return tbl;
        }

        return null;
    }
}
