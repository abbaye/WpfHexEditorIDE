//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
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

    /// <summary>Treat nullable warnings as errors (C# 8+ projects).</summary>
    public bool TreatNullableWarningsAsErrors { get; set; }

    /// <summary>Enable implicit usings by default (C# 10+ projects).</summary>
    public bool EnableImplicitUsings { get; set; } = true;

    /// <summary>Generate XML documentation file during builds.</summary>
    public bool GenerateDocumentation { get; set; }

    /// <summary>Default warning level (0–9). MSBuild /warn:N.</summary>
    public int DefaultWarningLevel { get; set; } = 4;
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
}
