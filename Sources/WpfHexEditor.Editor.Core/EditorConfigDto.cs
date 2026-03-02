//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
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
    /// <summary>
    /// Id of the IProjectItem (TBL) bound to this editor.
    /// </summary>
    public string? TblFileId { get; set; }

    // ── TextEditor ───────────────────────────────────────────────────────

    /// <summary>
    /// Override for the syntax language id (e.g. <c>"lua"</c>, <c>"markdown"</c>, <c>"asm_6502"</c>).
    /// <see langword="null"/> = auto-detect by file extension.
    /// </summary>
    public string? SyntaxLanguageId { get; set; }

    /// <summary>
    /// Saved caret line (1-based) in text editors. <c>0</c> = not saved.
    /// </summary>
    public int CaretLine { get; set; }

    /// <summary>
    /// Saved caret column (1-based) in text editors. <c>0</c> = not saved.
    /// </summary>
    public int CaretColumn { get; set; }

    /// <summary>
    /// First visible line number in the text editor viewport (scroll position). <c>0</c> = not saved.
    /// </summary>
    public int FirstVisibleLine { get; set; }

    // ── Extension slot — arbitrary key/value for future editors ──────────
    public Dictionary<string, string>? Extra { get; set; }
}
