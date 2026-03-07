//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Options;

/// <summary>
/// User-configurable application settings (persisted to JSON).
/// </summary>
public sealed class AppSettings
{
    // -- Environment > General -------------------------------------------

    /// <summary>
    /// Theme file name stem (e.g. "DarkTheme", "Generic").
    /// Applied via ApplyThemeFromSettings() in the host window.
    /// Default: "DarkTheme" (preserves existing behaviour).
    /// </summary>
    public string ActiveThemeName { get; set; } = "DarkTheme";

    // -- Environment > Save ----------------------------------------------

    /// <summary>
    /// Whether Ctrl+S writes directly to the physical file (Direct)
    /// or serialises edits to a companion .whchg file (Tracked).
    /// </summary>
    public FileSaveMode DefaultFileSaveMode { get; set; } = FileSaveMode.Direct;

    /// <summary>
    /// When true, a background timer periodically re-serialises all dirty
    /// project items in Tracked mode to keep .whchg files up-to-date.
    /// </summary>
    public bool AutoSerializeEnabled { get; set; } = false;

    /// <summary>Interval between auto-serialize passes, in seconds.</summary>
    public int AutoSerializeIntervalSeconds { get; set; } = 30;

    // -- Hex Editor defaults ---------------------------------------------

    /// <summary>
    /// Applied to every newly-opened HexEditor tab.
    /// Serialised as "hexEditorDefaults": { â€¦ } in settings.json.
    /// </summary>
    public HexEditorDefaultSettings HexEditorDefaults { get; set; } = new();

    // -- Solution Explorer -----------------------------------------------

    /// <summary>
    /// Solution Explorer panel behaviour settings.
    /// Serialised as "solutionExplorer": { â€¦ } in settings.json.
    /// </summary>
    public SolutionExplorerSettings SolutionExplorer { get; set; } = new();

    // -- Code Editor -----------------------------------------------------

    /// <summary>
    /// CodeEditor appearance and behaviour defaults.
    /// Serialised as "codeEditor": { â€¦ } in settings.json.
    /// </summary>
    public CodeEditorDefaultSettings CodeEditorDefaults { get; set; } = new();

    // -- Text Editor -----------------------------------------------------

    /// <summary>
    /// TextEditor appearance and behaviour defaults.
    /// Serialised as "textEditor": { â€¦ } in settings.json.
    /// </summary>
    public TextEditorDefaultSettings TextEditorDefaults { get; set; } = new();

    // -- Plugin System ----------------------------------------------------------------

    /// <summary>
    /// Plugin system behaviour and monitoring settings.
    /// Serialised as "pluginSystem": { … } in settings.json.
    /// </summary>
    public PluginSystemSettings PluginSystem { get; set; } = new();
}

// ----------------------------------------------------------------------------
// Solution Explorer Settings
// ----------------------------------------------------------------------------

/// <summary>
/// Behaviour settings for the Solution Explorer panel.
/// </summary>
public sealed class SolutionExplorerSettings
{
    /// <summary>
    /// When true, the Solution Explorer automatically highlights and reveals
    /// the file node that corresponds to the currently active editor tab.
    /// </summary>
    public bool TrackActiveDocument { get; set; } = true;

    /// <summary>
    /// When true, the expanded / collapsed state of each tree node is saved
    /// in the .whsln file and restored on next open.
    /// </summary>
    public bool PersistCollapseState { get; set; } = true;

    /// <summary>
    /// Show contextual balloon notifications for external file changes,
    /// paste conflicts, and other panel-level events.
    /// </summary>
    public bool ShowContextualNotifications { get; set; } = true;

    /// <summary>
    /// Default sort mode applied when a solution is first opened.
    /// Valid values: "None", "Name", "Type", "DateModified", "Size".
    /// </summary>
    public string DefaultSortMode { get; set; } = "None";

    /// <summary>
    /// Default filter mode applied when a solution is first opened.
    /// Valid values: "All", "Binary", "Text", "Image", "Language".
    /// </summary>
    public string DefaultFilterMode { get; set; } = "All";
}

// ----------------------------------------------------------------------------
// Code Editor Settings
// ----------------------------------------------------------------------------

/// <summary>
/// Appearance and behaviour defaults applied to every new CodeEditor tab.
/// </summary>
public sealed class CodeEditorDefaultSettings
{
    // -- Font ------------------------------------------------------------

    /// <summary>Font family name for the editor text area.</summary>
    public string FontFamily { get; set; } = "Consolas";

    /// <summary>Font size in points.</summary>
    public double FontSize { get; set; } = 13.0;

    // -- Indentation -----------------------------------------------------

    /// <summary>Number of spaces (or tab width) for one indentation level.</summary>
    public int IndentSize { get; set; } = 4;

    /// <summary>When true, indentation inserts spaces; when false, inserts tab characters.</summary>
    public bool UseSpaces { get; set; } = true;

    // -- Features --------------------------------------------------------

    /// <summary>Show IntelliSense auto-complete popup while typing.</summary>
    public bool ShowIntelliSense { get; set; } = true;

    /// <summary>Show line numbers in the gutter.</summary>
    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>Highlight the current line.</summary>
    public bool HighlightCurrentLine { get; set; } = true;

    /// <summary>Default zoom factor (1.0 = 100 %).</summary>
    public double DefaultZoom { get; set; } = 1.0;

    // -- Changeset (.whchg) -----------------------------------------------

    /// <summary>
    /// When true, CodeEditor tracks edits in a .whchg companion file
    /// (requires save mode Tracked to be effective).
    /// </summary>
    public bool ChangesetEnabled { get; set; } = true;

    // -- Syntax colours --------------------------------------------------
    // Stored as HTML hex strings (e.g. "#FF8C00").  Empty string = use theme default.

    /// <summary>Editor background colour override. Empty = use theme.</summary>
    public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>Default foreground / plain-text colour override. Empty = use theme.</summary>
    public string ForegroundColor { get; set; } = string.Empty;

    /// <summary>Keyword token colour override. Empty = use theme.</summary>
    public string KeywordColor { get; set; } = string.Empty;

    /// <summary>String literal token colour override. Empty = use theme.</summary>
    public string StringColor { get; set; } = string.Empty;

    /// <summary>Comment token colour override. Empty = use theme.</summary>
    public string CommentColor { get; set; } = string.Empty;

    /// <summary>Number literal token colour override. Empty = use theme.</summary>
    public string NumberColor { get; set; } = string.Empty;
}

// ----------------------------------------------------------------------------
// Text Editor Settings
// ----------------------------------------------------------------------------

/// <summary>
/// Appearance and behaviour defaults applied to every new TextEditor tab.
/// </summary>
public sealed class TextEditorDefaultSettings
{
    // -- Font ------------------------------------------------------------

    /// <summary>Font family name for the editor text area.</summary>
    public string FontFamily { get; set; } = "Consolas";

    /// <summary>Font size in points.</summary>
    public double FontSize { get; set; } = 13.0;

    // -- Indentation -----------------------------------------------------

    /// <summary>Number of spaces (or tab width) for one indentation level.</summary>
    public int IndentSize { get; set; } = 4;

    /// <summary>When true, indentation inserts spaces; when false, inserts tab characters.</summary>
    public bool UseSpaces { get; set; } = true;

    // -- Features --------------------------------------------------------

    /// <summary>Show line numbers in the gutter.</summary>
    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>Default zoom factor (1.0 = 100 %).</summary>
    public double DefaultZoom { get; set; } = 1.0;

    // -- Changeset (.whchg) -----------------------------------------------

    /// <summary>
    /// When true, TextEditor tracks edits in a .whchg companion file
    /// (requires save mode Tracked to be effective).
    /// </summary>
    public bool ChangesetEnabled { get; set; } = false;

    // -- Syntax colours --------------------------------------------------

    /// <summary>Editor background colour override. Empty = use theme.</summary>
    public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>Default foreground / plain-text colour override. Empty = use theme.</summary>
    public string ForegroundColor { get; set; } = string.Empty;

    /// <summary>Keyword token colour override. Empty = use theme.</summary>
    public string KeywordColor { get; set; } = string.Empty;

    /// <summary>String literal token colour override. Empty = use theme.</summary>
    public string StringColor { get; set; } = string.Empty;

    /// <summary>Comment token colour override. Empty = use theme.</summary>
    public string CommentColor { get; set; } = string.Empty;
}

// --------------------------------------------------------------------------------
// Plugin System Settings
// --------------------------------------------------------------------------------

/// <summary>
/// Behaviour and monitoring settings for the plugin system.
/// </summary>
public sealed class PluginSystemSettings
{
    /// <summary>
    /// Additional directory scanned for user-installed plugins at startup.
    /// Empty = use default %AppData%\WpfHexEditor\Plugins\.
    /// </summary>
    public string PluginsDirectory { get; set; } = string.Empty;

    /// <summary>Interval (seconds) between slow-plugin detector scans.</summary>
    public int MonitoringIntervalSeconds { get; set; } = 5;

    /// <summary>CPU usage threshold (%) above which a plugin is flagged as slow.</summary>
    public double CpuThresholdPercent { get; set; } = 25.0;

    /// <summary>Average response-time threshold (ms) above which a plugin is flagged as slow.</summary>
    public int ResponseTimeThresholdMs { get; set; } = 500;

    /// <summary>
    /// When true, the watchdog monitors plugin calls and raises PluginNonResponsive
    /// if a call exceeds the timeout.
    /// </summary>
    public bool EnableWatchdog { get; set; } = true;

    /// <summary>Seconds before the watchdog considers an InitializeAsync call timed-out.</summary>
    public int WatchdogTimeoutSeconds { get; set; } = 5;
}
