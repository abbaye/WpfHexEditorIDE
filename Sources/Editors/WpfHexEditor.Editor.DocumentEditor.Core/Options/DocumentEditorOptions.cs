// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Options/DocumentEditorOptions.cs
// Description:
//     Serialisable settings for the Document Editor.
//     Stored in AppSettings.DocumentEditor (global default).
//     Can be overridden per-document via DocumentEditorContext.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Options;

/// <summary>
/// Configurable settings for the Document Editor.
/// </summary>
public sealed class DocumentEditorOptions
{
    // ──────────────────────────────── Sync ────────────────────────────────────

    /// <summary>Throttle delay (ms) for text↔hex selection synchronisation.</summary>
    public int SyncThrottleMs { get; set; } = 150;

    /// <summary>Synchronise text selection → hex pane highlight.</summary>
    public bool SyncTextToHex { get; set; } = true;

    /// <summary>Synchronise hex selection → text pane highlight.</summary>
    public bool SyncHexToText { get; set; } = true;

    // ──────────────────────────────── Appearance ──────────────────────────────

    /// <summary>Show the forensic gutter overlay when forensic mode is active.</summary>
    public bool ShowForensicGutter { get; set; } = true;

    /// <summary>Show the dual binary/text minimap strip.</summary>
    public bool ShowBinaryMiniMap { get; set; } = true;

    /// <summary>Default font size (pt) for the text pane.</summary>
    public double DefaultTextFontSize { get; set; } = 14.0;

    // ──────────────────────────────── Hex highlight colours ───────────────────

    /// <summary>Semi-transparent ARGB color for block hover highlight in hex pane.</summary>
    public string BlockHighlightColor { get; set; } = "#30569CD6";

    /// <summary>Semi-transparent ARGB color for the currently selected block in hex pane.</summary>
    public string SelectedBlockColor { get; set; } = "#604EC9B0";

    /// <summary>Semi-transparent ARGB color for forensic alert overlays.</summary>
    public string ForensicAlertColor { get; set; } = "#60FF4444";

    // ──────────────────────────────── Hover ───────────────────────────────────

    /// <summary>Show block info tooltip on hover.</summary>
    public bool ShowBlockHoverTooltip { get; set; } = true;

    /// <summary>Hover delay (ms) before showing block tooltip.</summary>
    public int HoverDelayMs { get; set; } = 400;

    // ──────────────────────────────── Layout ──────────────────────────────────

    /// <summary>Default view mode when opening a document.</summary>
    public DocumentViewMode DefaultViewMode { get; set; } = DocumentViewMode.TextOnly;

    /// <summary>Default render mode (Page / Draft / Outline) when opening a document.</summary>
    public DocumentRenderMode DefaultRenderMode { get; set; } = DocumentRenderMode.Page;

    /// <summary>Show the scroll marker panel over the vertical scrollbar.</summary>
    public bool ShowScrollMarkers { get; set; } = true;

    /// <summary>Show the binary mini-map strip alongside the scrollbar.</summary>
    public bool ShowMiniMap { get; set; } = true;

    // ──────────────────────────────── Editing ─────────────────────────────────

    /// <summary>Number of spaces per indent level in the document text pane.</summary>
    public int DefaultIndentWidth { get; set; } = 4;

    // ──────────────────────────────── Auto-save ───────────────────────────────

    /// <summary>Enable periodic auto-save to a temp file.</summary>
    public bool AutoSaveEnabled { get; set; } = true;

    /// <summary>Auto-save interval in seconds (minimum 30, default 60).</summary>
    public int AutoSaveIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Document editor view modes.
/// </summary>
public enum DocumentViewMode
{
    /// <summary>Rich-text pane only.</summary>
    TextOnly,
    /// <summary>Rich-text + Structure tree panes.</summary>
    Structure,
    /// <summary>Zen/focus mode: chrome hidden, text centred with generous margins.</summary>
    Focus
}

/// <summary>
/// Document render modes — how the text canvas presents content.
/// </summary>
public enum DocumentRenderMode
{
    /// <summary>Paginated view: A4/Letter page card with shadow (default).</summary>
    Page,
    /// <summary>Continuous scroll: no page breaks, compact margins.</summary>
    Draft,
    /// <summary>Outline mode: structure tree centred, text minimised.</summary>
    Outline
}
