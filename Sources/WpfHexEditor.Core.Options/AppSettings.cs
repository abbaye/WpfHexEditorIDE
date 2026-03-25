//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.Options;

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
    /// Serialised as "hexEditorDefaults": { … } in settings.json.
    /// </summary>
    public HexEditorDefaultSettings HexEditorDefaults { get; set; } = new();

    // -- Solution Explorer -----------------------------------------------

    /// <summary>
    /// Solution Explorer panel behaviour settings.
    /// Serialised as "solutionExplorer": { … } in settings.json.
    /// </summary>
    public SolutionExplorerSettings SolutionExplorer { get; set; } = new();

    // -- Code Editor -----------------------------------------------------

    /// <summary>
    /// CodeEditor appearance and behaviour defaults.
    /// Serialised as "codeEditor": { … } in settings.json.
    /// </summary>
    public CodeEditorDefaultSettings CodeEditorDefaults { get; set; } = new();

    // -- Text Editor -----------------------------------------------------

    /// <summary>
    /// TextEditor appearance and behaviour defaults.
    /// Serialised as "textEditor": { … } in settings.json.
    /// </summary>
    public TextEditorDefaultSettings TextEditorDefaults { get; set; } = new();

    // -- Standalone File Save ----------------------------------------------------

    /// <summary>
    /// Per-editor Ctrl+S behaviour for files opened outside any project.
    /// Serialised as "standaloneFileSave": { … } in settings.json.
    /// </summary>
    public StandaloneFileSaveSettings StandaloneFileSave { get; set; } = new();

    // -- Plugin System ----------------------------------------------------------------

    /// <summary>
    /// Plugin system behaviour and monitoring settings.
    /// Serialised as "pluginSystem": { … } in settings.json.
    /// </summary>
    public PluginSystemSettings PluginSystem { get; set; } = new();

    // -- Output Logger ----------------------------------------------------------------

    /// <summary>
    /// Log-level colour settings for the Output panel.
    /// Serialised as "outputLogger": { … } in settings.json.
    /// </summary>
    public OutputLoggerSettings OutputLogger { get; set; } = new();

    // -- Build & Run ------------------------------------------------------------------

    /// <summary>Build system general options (output verbosity, parallel builds, etc.).</summary>
    public BuildRunSettings BuildRun { get; set; } = new();

    // -- Plugin Development -----------------------------------------------------------

    /// <summary>In-IDE plugin development options.</summary>
    public PluginDevSettings PluginDev { get; set; } = new();

    // -- IDE > Keyboard Shortcuts -------------------------------------------------

    /// <summary>
    /// User-overridden keyboard gestures keyed by command ID (e.g. "File.Save" → "Ctrl+Alt+S").
    /// When a command ID is absent, the built-in default gesture applies.
    /// An empty-string value means the user explicitly unbound the gesture.
    /// </summary>
    public Dictionary<string, string> KeyBindingOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // -- Command Palette ----------------------------------------------------------

    /// <summary>Command Palette ("Fuzzy Bar") appearance and behaviour settings.</summary>
    public CommandPaletteSettings CommandPalette { get; set; } = new();

    // -- Compare Files --------------------------------------------------------

    /// <summary>Compare Files viewer preferences and recent comparison history.</summary>
    public ComparisonSettings Comparison { get; set; } = new();

    // -- Debugger -------------------------------------------------------------

    /// <summary>Integrated debugger preferences and persisted breakpoints.</summary>
    public DebuggerSettings Debugger { get; set; } = new();

    // -- Workspace ----------------------------------------------------------------

    /// <summary>Workspace system preferences.</summary>
    public WorkspaceSettings Workspace { get; set; } = new();

    // -- Tab Hover Preview --------------------------------------------------------

    /// <summary>Docking tab thumbnail hover-preview options.</summary>
    public TabPreviewAppSettings TabPreview { get; set; } = new();
}

// ─── Command Palette ──────────────────────────────────────────────────────────

/// <summary>How the command description is shown next to results.</summary>
public enum CpDescriptionMode
{
    /// <summary>No description shown.</summary>
    None,
    /// <summary>Description shown as a tooltip on hover.</summary>
    Tooltip,
    /// <summary>Description shown in a fixed panel below the results list.</summary>
    BottomPanel,
}

/// <summary>Command Palette ("Fuzzy Bar") configurable options.</summary>
public sealed class CommandPaletteSettings
{
    // Apparence
    /// <summary>Window width in pixels (400–900). Default: 680.</summary>
    public int  WindowWidth         { get; set; } = 680;
    /// <summary>Show Segoe MDL2 icon glyph next to each entry.</summary>
    public bool ShowIconGlyphs      { get; set; } = true;
    /// <summary>Show category group headers when query is empty or short.</summary>
    public bool ShowCategoryHeaders { get; set; } = true;
    /// <summary>Show keyboard shortcut hint on the right of each entry.</summary>
    public bool ShowGestureHints    { get; set; } = true;

    // Description
    /// <summary>How the description for the selected command is surfaced.</summary>
    public CpDescriptionMode DescriptionMode { get; set; } = CpDescriptionMode.Tooltip;

    // Recherche
    /// <summary>Bold + colour the matched characters inside entry names.</summary>
    public bool HighlightMatchChars  { get; set; } = true;
    /// <summary>Maximum number of results to display. Default: 50.</summary>
    public int  MaxResults           { get; set; } = 50;
    /// <summary>Milliseconds to wait before firing a search after a keystroke (0 = immediate).</summary>
    public int  SearchDebounceMs     { get; set; } = 0;

    // Modes
    /// <summary>Prefix inserted into the search box when the palette opens ("" / ">" / "@" / ":" / "#").</summary>
    public string DefaultMode        { get; set; } = "";

    // Récents + fréquence
    /// <summary>Show recently used commands at the top when the query is empty.</summary>
    public bool ShowRecentCommands   { get; set; } = true;
    /// <summary>Number of recent commands to display (3–10). Default: 5.</summary>
    public int  RecentCommandsCount  { get; set; } = 5;
    /// <summary>Boost score for frequently / recently executed commands.</summary>
    public bool FrequencyBoostEnabled { get; set; } = true;

    // Context-aware
    /// <summary>Boost commands that belong to the same category as the active editor.</summary>
    public bool ContextBoostEnabled  { get; set; } = true;

    // Grep (% mode)
    /// <summary>Maximum number of content-grep results to collect before stopping the file scan.</summary>
    public int  MaxGrepResults       { get; set; } = 100;
    /// <summary>Files larger than this value (bytes) are skipped during content grep. Default 2 MB.</summary>
    public long MaxGrepFileSizeBytes { get; set; } = 2_000_000;

    // Historique (non exposé dans la page Options — interne)
    /// <summary>Per-entry execution history: Name → (count, lastUtc). Not shown in options UI.</summary>
    public Dictionary<string, CommandExecutionRecord> CommandHistory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Single entry in the command execution history.</summary>
public sealed class CommandExecutionRecord
{
    public int      Count   { get; set; }
    public DateTime LastUtc { get; set; }
}

/// <summary>Build &amp; Run general options.</summary>
public sealed class BuildRunSettings
{
    /// <summary>Save all dirty documents before starting a build.</summary>
    public bool SaveBeforeBuilding { get; set; } = true;

    /// <summary>MSBuild output verbosity: Quiet / Minimal / Normal / Detailed / Diagnostic.</summary>
    public string OutputVerbosity { get; set; } = "Minimal";

    /// <summary>Run MSBuild in-process (faster) vs out-of-process (isolated).</summary>
    public bool RunInProcess { get; set; } = true;

    /// <summary>Max number of projects to build in parallel (MSBuild /m:N).</summary>
    public int MaxParallelProjects { get; set; } = 4;

    /// <summary>Show Output panel automatically when a build starts.</summary>
    public bool ShowOutputOnBuildStart { get; set; }

    /// <summary>Show Output panel when a build produces errors.</summary>
    public bool ShowOutputOnBuildError { get; set; } = true;

    /// <summary>Action when Ctrl+F5 is triggered and the build produced errors.</summary>
    public RunOnBuildError OnRunWhenBuildError { get; set; } = RunOnBuildError.DoNotLaunch;

    /// <summary>Show Output panel automatically when a run starts.</summary>
    public bool ShowOutputOnRunStart { get; set; }

    /// <summary>Treat nullable warnings as errors (C# 8+ projects).</summary>
    public bool TreatNullableWarningsAsErrors { get; set; }

    /// <summary>Enable implicit usings by default (C# 10+ projects).</summary>
    public bool EnableImplicitUsings { get; set; } = true;

    /// <summary>Generate XML documentation file during builds.</summary>
    public bool GenerateDocumentation { get; set; }

    /// <summary>Default warning level (0–9). MSBuild /warn:N.</summary>
    public int DefaultWarningLevel { get; set; } = 4;
}

/// <summary>Action to take when starting a run and the build produced errors.</summary>
public enum RunOnBuildError { DoNotLaunch, Launch }

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

    /// <summary>Show SmartComplete auto-complete popup while typing.</summary>
    public bool ShowSmartComplete { get; set; } = true;

    /// <summary>Show line numbers in the gutter.</summary>
    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>Highlight the current line.</summary>
    public bool HighlightCurrentLine { get; set; } = true;

    /// <summary>Default zoom factor (1.0 = 100 %).</summary>
    public double DefaultZoom { get; set; } = 1.0;

    /// <summary>
    /// Lines scrolled per mouse-wheel notch.
    /// Maps to <c>CodeEditor.MouseWheelSpeed</c> — same enum and behaviour as HexEditor.
    /// </summary>
    public MouseWheelSpeed MouseWheelSpeed { get; set; } = MouseWheelSpeed.System;

    // -- Folding -------------------------------------------------------------

    /// <summary>
    /// When true, fold regions (gutter triangle and inline label) require a double-click
    /// to toggle instead of a single click.
    /// </summary>
    public bool FoldToggleOnDoubleClick { get; set; } = true;

    /// <summary>Wrap long lines visually at the viewport edge (hides horizontal scrollbar).</summary>
    public bool WordWrap { get; set; } = false;

    // -- End-of-Block Hint ---------------------------------------------------

    /// <summary>
    /// When true, hovering over a closing token (}, #endregion, &lt;/Tag&gt;) shows
    /// a compact popup with the matching opening line(s) and navigation link.
    /// </summary>
    public bool EndOfBlockHintEnabled { get; set; } = true;

    /// <summary>
    /// Milliseconds the cursor must dwell over a closing token before the
    /// end-of-block hint popup appears. Range: 100–2000. Default: 400.
    /// </summary>
    public int EndOfBlockHintDelayMs { get; set; } = 600;

    // -- InlineHints ---------------------------------------------------------

    /// <summary>
    /// Master toggle: when false, no inline hints are rendered regardless of
    /// <see cref="InlineHintsVisibleKinds"/>.
    /// </summary>
    public bool ShowInlineHints { get; set; } = true;

    /// <summary>
    /// Bitmask of symbol kinds (integer cast of <c>InlineHintsSymbolKinds</c>) for which
    /// hints are visible. Stored as int to avoid a cross-project enum dependency in
    /// AppSettings. 4095 = InlineHintsSymbolKinds.All = (1 &lt;&lt; 12) − 1.
    /// </summary>
    public int InlineHintsVisibleKinds { get; set; } = 4095;

    // -- Changeset (.whchg) -----------------------------------------------

    /// <summary>
    /// When true, CodeEditor tracks edits in a .whchg companion file
    /// (requires save mode Tracked to be effective).
    /// </summary>
    public bool ChangesetEnabled { get; set; } = false;

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

    /// <summary>Type name token colour override (CE_Type). Empty = use theme.</summary>
    public string TypeColor { get; set; } = string.Empty;

    /// <summary>Identifier token colour override (CE_Identifier). Empty = use theme.</summary>
    public string IdentifierColor { get; set; } = string.Empty;

    /// <summary>Operator token colour override (CE_Operator). Empty = use theme.</summary>
    public string OperatorColor { get; set; } = string.Empty;

    /// <summary>Bracket token colour override (CE_Bracket). Empty = use theme.</summary>
    public string BracketColor { get; set; } = string.Empty;

    /// <summary>Attribute token colour override (CE_Attribute). Empty = use theme.</summary>
    public string AttributeColor { get; set; } = string.Empty;
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

    /// <summary>Wrap long lines visually at the viewport edge (hides horizontal scrollbar).</summary>
    public bool WordWrap { get; set; } = false;

    /// <summary>Default zoom factor (1.0 = 100 %).</summary>
    public double DefaultZoom { get; set; } = 1.0;

    /// <summary>
    /// Lines scrolled per mouse-wheel notch.
    /// Maps to <c>TextViewport.MouseWheelSpeed</c> — same enum and behaviour as HexEditor.
    /// </summary>
    public MouseWheelSpeed MouseWheelSpeed { get; set; } = MouseWheelSpeed.System;

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
// Standalone File Save Settings
// --------------------------------------------------------------------------------

/// <summary>
/// Controls whether Ctrl+S on a standalone file (not belonging to any project)
/// overwrites the original directly or prompts a Save As dialog — per editor type.
/// </summary>
public sealed class StandaloneFileSaveSettings
{
    /// <summary>When true, Ctrl+S overwrites the original binary file directly.</summary>
    public bool HexEditorDirectSave   { get; set; } = true;

    /// <summary>When true, Ctrl+S overwrites the original code/text file directly.</summary>
    public bool CodeEditorDirectSave  { get; set; } = true;

    /// <summary>When true, Ctrl+S overwrites the original text file directly.</summary>
    public bool TextEditorDirectSave  { get; set; } = true;

    /// <summary>When true, Ctrl+S overwrites the original TBL file directly.</summary>
    public bool TblEditorDirectSave   { get; set; } = true;

    /// <summary>
    /// When true, Ctrl+S overwrites the original image file with the current
    /// transform result. When false, a Save As / Export dialog is shown.
    /// </summary>
    public bool ImageViewerDirectSave { get; set; } = true;
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

    /// <summary>
    /// Interval (seconds) between continuous CPU/memory diagnostic samples
    /// recorded for each loaded plugin. Drives the Plugin Monitor charts.
    /// </summary>
    public int DiagnosticSamplingSeconds { get; set; } = 5;

    /// <summary>
    /// When true, all plugins discovered at startup are loaded automatically
    /// without prompting the user.
    /// </summary>
    public bool AutoLoadPlugins { get; set; } = true;

    // -- Memory Alert Thresholds ------------------------------------------

    /// <summary>Memory warning threshold in MB (yellow). Default: 500 MB.</summary>
    public int MemoryWarningThresholdMB { get; set; } = 500;

    /// <summary>Memory high threshold in MB (orange). Default: 750 MB.</summary>
    public int MemoryHighThresholdMB { get; set; } = 750;

    /// <summary>Memory critical threshold in MB (red). Default: 1000 MB.</summary>
    public int MemoryCriticalThresholdMB { get; set; } = 1000;

    /// <summary>Enable memory usage alerts and badges.</summary>
    public bool EnableMemoryAlerts { get; set; } = true;

    /// <summary>Show color gradation (green/yellow/orange/red) in monitors.</summary>
    public bool ShowMemoryColorGradation { get; set; } = true;

    // -- Memory Alert Colors ----------------------------------------------

    /// <summary>Color for normal memory usage (green). Default: #22C55E</summary>
    public string MemoryNormalColor { get; set; } = "#22C55E";

    /// <summary>Color for warning threshold (yellow). Default: #EAB308</summary>
    public string MemoryWarningColor { get; set; } = "#EAB308";

    /// <summary>Color for high threshold (orange). Default: #F97316</summary>
    public string MemoryHighColor { get; set; } = "#F97316";

    /// <summary>Color for critical threshold (red). Default: #EF4444</summary>
    public string MemoryCriticalColor { get; set; } = "#EF4444";
}

// --------------------------------------------------------------------------------
// Output Logger Settings
// --------------------------------------------------------------------------------

/// <summary>
/// Log-level colour settings for the Output panel.
/// Colours are stored as HTML hex strings (e.g. "#F0503C").
/// INFO level always inherits the theme foreground and is not configurable here.
/// </summary>
public sealed class OutputLoggerSettings
{
    // -- Log-level colours --------------------------------------------------------

    /// <summary>Colour for WARN messages. Default: gold #DCB432.</summary>
    public string WarnColor    { get; set; } = "#DCB432";

    /// <summary>Colour for ERROR messages. Default: red-orange #F0503C.</summary>
    public string ErrorColor   { get; set; } = "#F0503C";

    /// <summary>Colour for DEBUG messages. Default: gray #828282.</summary>
    public string DebugColor   { get; set; } = "#828282";

    /// <summary>Colour for SUCCESS/OK messages. Default: teal-green #4EC9B0.</summary>
    public string SuccessColor { get; set; } = "#4EC9B0";

    // -- Change notification ------------------------------------------------------

    /// <summary>
    /// Raised when colours have been updated so that <see cref="OutputLogger"/>
    /// can rebuild its cached brushes without restarting.
    /// </summary>
    public static event Action? ColorsChanged;

    /// <summary>
    /// Signals that colour settings have changed.
    /// Called by <c>OutputOptionsPage.Flush()</c> after writing new values.
    /// </summary>
    public static void NotifyChanged() => ColorsChanged?.Invoke();
}


/// <summary>In-IDE Plugin Development options.</summary>
public sealed class PluginDevSettings
{
    /// <summary>Rebuild plugin automatically when a source file is saved.</summary>
    public bool AutoRebuildOnSave { get; set; } = false;

    /// <summary>Copy build output to the plugin directory after a successful build.</summary>
    public bool CopyOutputOnBuild { get; set; } = true;

    /// <summary>Minimum log level to display in the Plugin Dev Log panel.</summary>
    public string LogLevel { get; set; } = "Info";

    /// <summary>Sandbox mode: "Light" (in-process catch) or "Full" (out-of-process Named Pipe).</summary>
    public string SandboxMode { get; set; } = "Light";

    /// <summary>Timeout in milliseconds for plugin lifecycle calls (Initialize / Shutdown).</summary>
    public int LifecycleTimeoutMs { get; set; } = 5000;

    /// <summary>Show "Plugin Dev Mode" indicator in the main window status bar.</summary>
    public bool ShowStatusBarIndicator { get; set; } = true;

    // -- IDE > Keyboard Shortcuts ----------------------------------------

    /// <summary>
    /// User-overridden keyboard gestures keyed by command ID (e.g. "File.Save" → "Ctrl+Alt+S").
    /// When a command ID is absent, the built-in default gesture applies.
    /// An empty-string value means the user explicitly unbound the gesture.
    /// </summary>
    public Dictionary<string, string> KeyBindingOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

// ─── Workspace ────────────────────────────────────────────────────────────────

// ─── Tab Hover Preview ────────────────────────────────────────────────────────

/// <summary>
/// Configurable options for the docking tab hover-preview thumbnail popup.
/// Serialised as "tabPreview": { … } in settings.json.
/// </summary>
public sealed class TabPreviewAppSettings
{
    /// <summary>When false, no popup is shown on tab hover.</summary>
    public bool Enabled       { get; set; } = true;

    /// <summary>Show the filename footer below the screenshot thumbnail.</summary>
    public bool ShowFileName  { get; set; } = true;

    /// <summary>Thumbnail width in pixels (100–400). Default: 200.</summary>
    public int  PreviewWidth  { get; set; } = 200;

    /// <summary>Thumbnail height in pixels (80–300). Default: 150.</summary>
    public int  PreviewHeight { get; set; } = 150;

    /// <summary>Milliseconds the mouse must hover before the popup appears (100–1000). Default: 400.</summary>
    public int  OpenDelayMs   { get; set; } = 400;

    /// <summary>Milliseconds before the popup closes after mouse leave (50–500). Default: 150.</summary>
    public int  CloseDelayMs  { get; set; } = 150;

    /// <summary>Raised by <see cref="NotifyChanged"/> so MainWindow can push new values to DockHost.</summary>
    public static event Action? Changed;

    /// <summary>Signal that settings have been updated. Called by <c>TabPreviewOptionsPage.Flush()</c>.</summary>
    public static void NotifyChanged() => Changed?.Invoke();
}

// ─── Workspace ────────────────────────────────────────────────────────────────

/// <summary>Workspace system preferences.</summary>
public sealed class WorkspaceSettings
{
    /// <summary>
    /// When true, the IDE prompts to save the active workspace before exiting
    /// or closing the workspace.
    /// </summary>
    public bool PromptSaveOnClose { get; set; } = true;

    /// <summary>
    /// When true, opening a workspace automatically restores the previously
    /// open solution recorded inside it.
    /// </summary>
    public bool RestoreSolutionOnOpen { get; set; } = true;

    /// <summary>
    /// When true, opening a workspace restores the set of open editor tabs
    /// recorded inside it.
    /// </summary>
    public bool RestoreOpenFilesOnOpen { get; set; } = true;

    /// <summary>
    /// When true, opening a workspace applies the theme stored inside it.
    /// </summary>
    public bool RestoreThemeOnOpen { get; set; } = true;

    /// <summary>
    /// Path of the most-recently used workspace file (.whidews).
    /// Populated automatically; not shown in the options UI.
    /// </summary>
    public string RecentWorkspacePath { get; set; } = string.Empty;
}
