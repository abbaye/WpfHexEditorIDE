//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Serialisable snapshot of an editor's per-file configuration.
/// Stored in the .whproj alongside each project item and restored when
/// the file is re-opened, so the user gets back exactly the view they left.
/// </summary>
public sealed class EditorConfigDto
{
    // ── HexEditor ────────────────────────────────────────────────────────
    public int     BytesPerLine    { get; set; }
    public string? Encoding        { get; set; }
    public string? EditMode        { get; set; }   // "Insert" | "Overwrite"
    public string? FormatId        { get; set; }   // detected/selected format name
    public double  ScrollOffset    { get; set; }
    public long    SelectionStart  { get; set; } = -1;
    public long    SelectionLength { get; set; }

    // ── TBL link ─────────────────────────────────────────────────────────
    /// <summary>Id of the IProjectItem (TBL) bound to this editor.</summary>
    public string? TblFileId { get; set; }

    // ── Extension slot — arbitrary key/value for future editors ──────────
    public Dictionary<string, string>? Extra { get; set; }
}
