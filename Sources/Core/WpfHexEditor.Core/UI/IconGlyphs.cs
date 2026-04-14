// ==========================================================
// Project      : WpfHexEditorControl
// File         : IconGlyphs.cs
// Description  : Segoe MDL2 Assets glyph reference constants for all menu
//                and toolbar icons across the app shell and editors.
//                Single source of truth — update here to track changes across
//                the solution. XAML files use these hex codes inline; this file
//                is a documentation reference, not a runtime binding target.
// Architecture : Infrastructure/UI — constants only, no logic.
// ==========================================================

namespace WpfHexEditor.Core.UI;

/// <summary>
/// Segoe MDL2 Assets glyph constants used in menus, context menus, and toolbars.
/// All values correspond to Unicode code points in the Segoe MDL2 Assets font.
/// </summary>
public static class IconGlyphs
{
    // ── File ──────────────────────────────────────────────────────────────────

    public const string New             = "\uE8A5"; // Document
    public const string NewSolution     = "\uE8F1"; // Folder + badge
    public const string NewProject      = "\uE8F1"; // Folder + badge
    public const string NewFile         = "\uEB9F"; // New document
    public const string NewWorkspace    = "\uF16A"; // Workspace grid

    public const string Open            = "\uE8E5"; // Open folder
    public const string OpenSolution    = "\uE8B5"; // Folder with link
    public const string OpenFolder      = "\uED41"; // Open folder arrow
    public const string OpenFile        = "\uEB9F"; // New document
    public const string OpenWorkspace   = "\uE8A9"; // Folder bookmark

    public const string Close           = "\uE711"; // X
    public const string CloseAll        = "\uE8BB"; // Multiple close

    public const string Save            = "\uE74E"; // Floppy disk
    public const string SaveAll         = "\uE78C"; // Multiple save (distinct from Export)
    public const string SaveAs          = "\uE792"; // Export / Save As
    public const string WriteToDisk     = "\uE9F5"; // Hard disk

    public const string Exit            = "\uE7E8"; // Power button (distinct from Close)

    public const string RecentSolutions = "\uE823"; // Clock / history
    public const string RecentFiles     = "\uE81C"; // Recent documents

    // ── Edit ──────────────────────────────────────────────────────────────────

    public const string Undo            = "\uE7A7"; // Undo arrow
    public const string Redo            = "\uE7A6"; // Redo arrow

    public const string Cut             = "\uE8C6"; // Scissors
    public const string Copy            = "\uE8C8"; // Copy documents
    public const string Paste           = "\uE77F"; // Paste clipboard
    public const string PasteOverwrite  = "\uE932"; // Paste with overwrite indicator

    public const string Delete          = "\uE74D"; // Trash

    public const string SelectAll       = "\uE8B3"; // Select all

    public const string Find            = "\uE721"; // Magnifier
    public const string FindAdvanced    = "\uE712"; // Search + filter
    public const string GoTo            = "\uE8A7"; // Go to / navigate

    public const string FormatDocument  = "\uE70F"; // Edit / format
    public const string FormatSelection = "\uEF20"; // Selection format (distinct from FormatDocument)

    // ── Bookmarks ─────────────────────────────────────────────────────────────

    public const string SetBookmark     = "\uE718"; // Bookmark add
    public const string ClearBookmarks  = "\uE74D"; // Trash (same as Delete — intentional)

    // ── Copy variants (sub-menu) ──────────────────────────────────────────────

    public const string CopyHex        = "\uE943"; // Code / hex braces
    public const string CopyAscii      = "\uE8A4"; // Text file
    public const string CopyCode       = "\uE943"; // Code braces (C#, C, etc.)
    public const string CopyTable      = "\uE9D5"; // Table / TBL
    public const string CopyFormatted  = "\uE8EF"; // Formatted text

    // ── Selection operations ──────────────────────────────────────────────────

    public const string ReverseSelection = "\uE8B1"; // Flip / mirror
    public const string InvertSelection  = "\uEA37"; // Invert / negate

    // ── Hex / binary operations ───────────────────────────────────────────────

    public const string Fill            = "\uE771"; // Fill (bucket-like)
    public const string Replace         = "\uE8AB"; // Replace / swap

    // ── Compare ───────────────────────────────────────────────────────────────

    public const string CompareFile      = "\uE8A5"; // Document compare
    public const string CompareSelection = "\uE8B7"; // Fragment compare

    // ── Build ─────────────────────────────────────────────────────────────────

    public const string Build           = "\uE768"; // Play / Run (distinct from Flip Horizontal E8B1)
    public const string Rebuild         = "\uE72C"; // Refresh / rebuild
    public const string Clean           = "\uE74D"; // Trash (same as Delete — intentional)
    public const string CancelBuild     = "\uE711"; // X / cancel

    // ── Format script toolbar ─────────────────────────────────────────────────

    public const string Script          = "\uE943"; // Code braces
    public const string Validate        = "\uE8FB"; // Checkmark / validate
    public const string ErrorGlyph      = "\uEA39"; // Error / X circle
    public const string WarningGlyph    = "\uE7BA"; // Warning triangle

    // ── Generic ───────────────────────────────────────────────────────────────

    public const string Settings        = "\uE713"; // Gear / settings
    public const string Refresh         = "\uE72C"; // Refresh arrow
    public const string Add             = "\uE710"; // Plus / add
    public const string Properties      = "\uE713"; // Gear (same as Settings — intentional)
}
