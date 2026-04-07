//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.Options;

/// <summary>
/// User-configurable application settings (persisted to JSON).
/// </summary>
public sealed class AppSettings
{
    // -- Settings Version ------------------------------------------------

    /// <summary>
    /// Schema version for migration. Increment when adding/renaming/removing fields.
    /// AppSettingsService checks this on load and applies migration transforms.
    /// </summary>
    public int SettingsVersion { get; set; } = 1;

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

    // -- View Menu Organization ---------------------------------------------------

    /// <summary>Dynamic View menu organization preferences.</summary>
    public ViewMenuSettings ViewMenu { get; set; } = new();

    // -- Environment > Documents --------------------------------------------------

    /// <summary>External file change detection and auto-reload behaviour.</summary>
    public DocumentSettings Documents { get; set; } = new();

    // -- Document Editor ----------------------------------------------------------

    /// <summary>Document Editor (RTF/DOCX/ODT) preferences.</summary>
    public WpfHexEditor.Editor.DocumentEditor.Core.Options.DocumentEditorOptions DocumentEditor { get; set; } = new();

    // -- Marketplace --------------------------------------------------------------

    /// <summary>Plugin Marketplace preferences (GitHub token, auto-update check).</summary>
    public MarketplaceSettings Marketplace { get; set; } = new();

    // -- Document Structure -------------------------------------------------------

    /// <summary>Document Structure panel preferences (max depth, etc.).</summary>
    public DocumentStructureSettings DocumentStructure { get; set; } = new();

    // -- Lazy Plugin Persistence --------------------------------------------------

    /// <summary>
    /// IDs of lazy (Dormant→Loaded) plugins whose panels were visible at last shutdown.
    /// Restored eagerly at next startup so the docking layout can re-anchor their panels.
    /// </summary>
    public List<string> LazyPluginsToRestore { get; set; } = [];

    // -- Layout Customization -----------------------------------------------------

    /// <summary>Layout visibility, positions, and mode preferences (Customize Layout popup).</summary>
    public LayoutSettings Layout { get; set; } = new();

    // -- Docking — Auto-Hide Timing -----------------------------------------------

    /// <summary>Delay in ms before auto-hide flyout opens on hover (default 400).</summary>
    public int AutoHideOpenDelayMs { get; set; } = 400;

    /// <summary>Delay in ms before auto-hide flyout closes after mouse leaves (default 300).</summary>
    public int AutoHideCloseDelayMs { get; set; } = 300;

    /// <summary>Duration in ms for auto-hide slide animation (default 120).</summary>
    public int AutoHideSlideAnimationMs { get; set; } = 120;

    // -- Docking — Animations -----------------------------------------------------

    /// <summary>Duration in ms for dock overlay fade-in (default 150). 0 = instant.</summary>
    public int DockOverlayFadeInMs { get; set; } = 150;

    /// <summary>Duration in ms for dock overlay fade-out (default 100). 0 = instant.</summary>
    public int DockOverlayFadeOutMs { get; set; } = 100;

    /// <summary>Duration in ms for floating window fade-in (default 120). 0 = instant.</summary>
    public int FloatingWindowFadeInMs { get; set; } = 120;

    /// <summary>When false, all docking animations are disabled (instant transitions).</summary>
    public bool DockAnimationsEnabled { get; set; } = true;

    // -- Docking — Panel Sizing ---------------------------------------------------

    /// <summary>Maximum display width in px for a tab header title (default 180). Controls ellipsis truncation.</summary>
    public int MaxTabWidth { get; set; } = 180;

    /// <summary>Minimum pane size in px during splitter resize (default 60).</summary>
    public int MinPaneSize { get; set; } = 60;

    /// <summary>Default width in px for new floating windows (default 400).</summary>
    public int FloatingWindowDefaultWidth { get; set; } = 400;

    /// <summary>Default height in px for new floating windows (default 300).</summary>
    public int FloatingWindowDefaultHeight { get; set; } = 300;

    // -- Docking — Layout Profiles ------------------------------------------------

    /// <summary>Directory for storing layout profile files. Null = AppData default.</summary>
    public string? LayoutProfileDirectory { get; set; }

    // -- Environment > UI ---------------------------------------------------------

    /// <summary>IDE UI appearance options (active panel highlight mode, etc.).</summary>
    public UiSettings UI { get; set; } = new();

    // -- Environment > Tab Groups -------------------------------------------------

    /// <summary>Document tab group layout preferences.</summary>
    public TabGroupSettings TabGroups { get; set; } = new();
}

/// <summary>IDE UI appearance settings.</summary>
public sealed class UiSettings
{
    /// <summary>
    /// Visual highlight mode for the active panel/document container.
    /// Default: TopBar (2px accent bar on top edge — VS Code style).
    /// </summary>
    public ActivePanelHighlightMode ActivePanelHighlight { get; set; } = ActivePanelHighlightMode.TopBar;
}

// ─── Tab Groups ───────────────────────────────────────────────────────────────

/// <summary>Document tab group layout preferences.</summary>
public sealed class TabGroupSettings
{
    /// <summary>Minimum width (px) of each pane before a vertical split is refused.</summary>
    public int MinGroupWidthPx { get; set; } = 200;

    /// <summary>Minimum height (px) of each pane before a horizontal split is refused.</summary>
    public int MinGroupHeightPx { get; set; } = 150;

    /// <summary>When true, both panes are sized equally (50/50) after a split. Default: true.</summary>
    public bool EnforceEqualSize { get; set; } = true;

    /// <summary>When true, the tab group layout is included in workspace saves. Default: true.</summary>
    public bool PersistTabGroupLayout { get; set; } = true;

    /// <summary>
    /// When true, a "Group N" badge is shown on each document tab bar
    /// only while multiple groups are open. Default: true.
    /// </summary>
    public bool ShowGroupNumberBadge { get; set; } = true;
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

    // -- Quick File Open Ctrl+P (#179) -----------------------------------------

    /// <summary>Settings for the Quick File Open (Ctrl+P) feature.</summary>
    public QuickOpenSettings QuickOpen { get; set; } = new();
}

/// <summary>Single entry in the command execution history.</summary>
public sealed class CommandExecutionRecord
{
    public int      Count   { get; set; }
    public DateTime LastUtc { get; set; }
}

// ----------------------------------------------------------------------------
// Quick File Open settings (#179)
// ----------------------------------------------------------------------------

/// <summary>Settings for the Quick File Open (Ctrl+P) feature.</summary>
public sealed class QuickOpenSettings
{
    /// <summary>Exclude files matching patterns in .gitignore when searching. Default: true.</summary>
    public bool RespectGitignore { get; set; } = true;

    /// <summary>Include hidden files (dot-files) in search results. Default: false.</summary>
    public bool ShowHiddenFiles { get; set; } = false;

    /// <summary>Maximum number of file results to display. Default: 50.</summary>
    public int MaxResults { get; set; } = 50;

    /// <summary>Show a read-only preview of the file when hovering a result. Default: true.</summary>
    public bool PreviewOnHover { get; set; } = true;

    /// <summary>
    /// When true, search matches against the full relative path; when false, matches file name only.
    /// Default: false (file name only).
    /// </summary>
    public bool SearchInFullPath { get; set; } = false;

    /// <summary>Number of recently opened files shown at the top when the query is empty. Default: 10.</summary>
    public int RecentFilesCount { get; set; } = 10;

    /// <summary>Support "filename:42" syntax to open a file at a specific line. Default: true.</summary>
    public bool OpenAtLineEnabled { get; set; } = true;
}

// ----------------------------------------------------------------------------
// Find All References settings (#157)
// ----------------------------------------------------------------------------

/// <summary>Describes where Find All References results are displayed.</summary>
public enum ReferencesResultsMode
{
    /// <summary>Show results in a docked panel (default).</summary>
    DockedPanel,

    /// <summary>Show results in a compact inline popup.</summary>
    InlinePopup,
}

/// <summary>Settings for the Find All References (Shift+F12) feature.</summary>
public sealed class ReferencesSettings
{
    /// <summary>Where results are displayed. Default: DockedPanel.</summary>
    public ReferencesResultsMode ResultsMode { get; set; } = ReferencesResultsMode.DockedPanel;

    /// <summary>Group results by source file in the results panel. Default: true.</summary>
    public bool GroupByFile { get; set; } = true;

    /// <summary>Include matches found inside comments. Default: true.</summary>
    public bool IncludeInComments { get; set; } = true;

    /// <summary>Include matches found inside string literals. Default: false.</summary>
    public bool IncludeInStrings { get; set; } = false;

    /// <summary>Maximum number of reference results to collect before stopping. Default: 500.</summary>
    public int MaxResults { get; set; } = 500;

    /// <summary>Show a preview of the matching line when hovering a result in the panel. Default: true.</summary>
    public bool PreviewOnHover { get; set; } = true;
}

// ----------------------------------------------------------------------------
// Sticky Scroll settings (#160)
// ----------------------------------------------------------------------------

/// <summary>Settings for the Sticky Scroll feature in the Code Editor.</summary>
public sealed class StickyScrollSettings
{
    /// <summary>Show sticky scope headers at the top of the editor while scrolling. Default: true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum number of scope headers to display simultaneously (1–10). Default: 4.</summary>
    public int MaxLines { get; set; } = 4;

    /// <summary>Apply the same syntax highlighting to sticky headers as to the code body. Default: true.</summary>
    public bool SyntaxHighlight { get; set; } = true;

    /// <summary>Click a sticky header to scroll to the start of that scope. Default: true.</summary>
    public bool ClickToNavigate { get; set; } = true;

    /// <summary>Opacity of the sticky header panel (0.5–1.0). Default: 0.95.</summary>
    public double Opacity { get; set; } = 0.95;

    /// <summary>
    /// Scopes shorter than this many lines are not shown in the sticky header.
    /// Avoids cluttering the header with trivial one-liner blocks. Default: 5.
    /// </summary>
    public int MinScopeLines { get; set; } = 5;
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

    /// <summary>Show render frame time ("Refresh: N ms") in the IDE right-side status bar.</summary>
    public bool ShowRefreshRateInStatusBar { get; set; } = true;

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

    /// <summary>Show vertical column ruler guide lines defined by the active language's whfmt.</summary>
    public bool ShowColumnRulers { get; set; } = true;

    // -- End-of-Block Hint ---------------------------------------------------

    /// <summary>
    /// When true, hovering over a closing token (}, #endregion, &lt;/Tag&gt;) shows
    /// a compact popup with the matching opening line(s) and navigation link.
    /// </summary>
    public bool EndOfBlockHintEnabled { get; set; } = true;

    /// <summary>
    /// When true, breakpoint lines are highlighted with a semi-transparent background
    /// tint (red=active, orange=conditional, gray=disabled).
    /// </summary>
    public bool BreakpointLineHighlightEnabled { get; set; } = true;

    /// <summary>
    /// Milliseconds the cursor must dwell over a closing token before the
    /// end-of-block hint popup appears. Range: 100–2000. Default: 400.
    /// </summary>
    public int EndOfBlockHintDelayMs { get; set; } = 600;

    // -- Minimap --------------------------------------------------------------

    /// <summary>Show a VS Code-style code overview minimap on the right side of the editor.</summary>
    public bool ShowMinimap { get; set; } = true;

    /// <summary>Render individual syntax tokens as colored blocks (true) or single rect per line (false).</summary>
    public bool MinimapRenderCharacters { get; set; } = true;

    /// <summary>Vertical sizing mode: 0=Proportional, 1=Fill, 2=Fit.</summary>
    public int MinimapVerticalSize { get; set; }

    /// <summary>Viewport slider visibility: 0=Always, 1=MouseOver.</summary>
    public int MinimapSliderMode { get; set; }

    /// <summary>Minimap placement: 0=Left, 1=Right.</summary>
    public int MinimapSide { get; set; } = 1;

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

    /// <summary>
    /// Reference-count source strategy: 0=Auto (Roslyn when available, regex fallback),
    /// 1=RoslynOnly (no hint for non-C# files), 2=RegexAlways.
    /// Stored as int to avoid cross-project enum dependency.
    /// </summary>
    public int InlineHintsSource { get; set; } = 0;

    /// <summary>Show inferred type hints for <c>var</c> declarations (e.g. <c>var x = 1;</c> → <c>: int</c>).</summary>
    public bool ShowVarTypeHints { get; set; } = true;

    /// <summary>Show return-type hints for lambda and anonymous function expressions.</summary>
    public bool ShowLambdaReturnTypeHints { get; set; } = true;

    // -- Changeset (.whchg) -----------------------------------------------

    /// <summary>
    /// When true, CodeEditor tracks edits in a .whchg companion file
    /// (requires save mode Tracked to be effective).
    /// </summary>
    public bool ChangesetEnabled { get; set; } = false;

    // -- Clickable links --------------------------------------------------

    /// <summary>When true, HTTP/HTTPS URLs are detected and Ctrl+Click opens them in the browser.</summary>
    public bool ClickableLinksEnabled  { get; set; } = true;

    /// <summary>When true, email addresses are detected and Ctrl+Click opens the mail client.</summary>
    public bool ClickableEmailsEnabled { get; set; } = true;

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

    // -- Auto-close brackets & quotes (#163) ------------------------------------

    /// <summary>Auto-insert matching closing bracket when an opening bracket is typed.</summary>
    public bool AutoClosingBrackets { get; set; } = true;

    /// <summary>Auto-insert matching closing quote when an opening quote is typed.</summary>
    public bool AutoClosingQuotes { get; set; } = true;

    /// <summary>
    /// When typing a closing character that already follows the cursor, advance the cursor
    /// instead of inserting a duplicate.
    /// </summary>
    public bool SkipOverClosingChar { get; set; } = true;

    /// <summary>
    /// When text is selected and an opening bracket or quote is typed, wrap the selection
    /// with the matching pair instead of replacing it.
    /// </summary>
    public bool WrapSelectionInPairs { get; set; } = true;

    // -- Coloring (#162/#168/#159) -----------------------------------------------

    /// <summary>Color bracket pairs with CE_Bracket_1/2/3/4 based on nesting depth.</summary>
    public bool BracketPairColorization { get; set; } = true;

    /// <summary>Color scope guide lines with CE_Bracket_1/2/3/4 matching bracket depth (requires BracketPairColorization).</summary>
    public bool RainbowScopeGuides { get; set; } = true;

    /// <summary>Show a 12×12 color swatch next to color literals (e.g. #FF5733) in CSS/XAML/C# files.</summary>
    public bool ColorSwatchPreview { get; set; } = true;

    /// <summary>Automatically format the document on Ctrl+S save.</summary>
    public bool FormatOnSave { get; set; } = false;

    // -- Formatting overrides (null = use .whfmt language default) ----------------

    /// <summary>Override whfmt trimTrailingWhitespace. Null = inherit language default.</summary>
    public bool? TrimTrailingWhitespace { get; set; }

    /// <summary>Override whfmt insertFinalNewline. Null = inherit language default.</summary>
    public bool? InsertFinalNewline { get; set; }

    /// <summary>Override whfmt spaceAfterKeywords. Null = inherit language default.</summary>
    public bool? SpaceAfterKeywords { get; set; }

    /// <summary>Override whfmt spaceAroundBinaryOperators. Null = inherit.</summary>
    public bool? SpaceAroundBinaryOperators { get; set; }

    /// <summary>Override whfmt spaceAfterComma. Null = inherit.</summary>
    public bool? SpaceAfterComma { get; set; }

    /// <summary>Override whfmt indentCaseLabels. Null = inherit.</summary>
    public bool? IndentCaseLabels { get; set; }

    /// <summary>Override whfmt organizeImports. Null = inherit.</summary>
    public bool? OrganizeImports { get; set; }

    // -- XML / XAML formatting --------------------------------------------------

    /// <summary>
    /// Attribute continuation indent multiplier for XML/XAML/HTML.
    /// Default = 2 (double-indent, VS-style).
    /// </summary>
    public int XmlAttributeIndentLevels { get; set; } = 2;

    /// <summary>
    /// When true, each XML/XAML attribute is placed on its own line.
    /// Default = false.
    /// </summary>
    public bool XmlOneAttributePerLine { get; set; } = false;

    // -- Column Rulers (#165) ---------------------------------------------------

    /// <summary>
    /// Fallback column ruler positions used when a language has no explicit
    /// <c>columnRulers</c> in its whfmt. Empty = no rulers by default.
    /// </summary>
    public int[] DefaultColumnRulers { get; set; } = [];

    // -- Find All References (#157) ----------------------------------------------

    /// <summary>Settings for the Find All References (Shift+F12) feature.</summary>
    public ReferencesSettings References { get; set; } = new();

    // -- Sticky Scroll (#160) ---------------------------------------------------

    /// <summary>Settings for the Sticky Scroll feature.</summary>
    public StickyScrollSettings StickyScroll { get; set; } = new();

    // -- Per-language formatting overrides -----------------------------------

    /// <summary>
    /// Per-language formatting overrides keyed by language ID (e.g. "csharp", "shell").
    /// Each entry only overrides explicitly set properties; null = inherit .whfmt default.
    /// </summary>
    public Dictionary<string, LanguageFormattingOverrides> PerLanguageOverrides { get; set; } = new();
}

// ── LanguageFormattingOverrides ─────────────────────────────────────────────

/// <summary>
/// Per-language formatting preference overrides stored in IDE settings.
/// All properties are nullable: <see langword="null"/> = inherit the .whfmt default.
/// Serialised as a child object of <see cref="CodeEditorDefaultSettings.PerLanguageOverrides"/>.
/// </summary>
public sealed class LanguageFormattingOverrides
{
    public bool? TrimTrailingWhitespace     { get; set; }
    public bool? InsertFinalNewline         { get; set; }
    public bool? SpaceAfterKeywords         { get; set; }
    public bool? SpaceAroundBinaryOperators { get; set; }
    public bool? SpaceAfterComma            { get; set; }
    public bool? IndentCaseLabels           { get; set; }
    public bool? OrganizeImports            { get; set; }
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

    /// <summary>Show render frame time ("Refresh: N ms") in the IDE right-side status bar.</summary>
    public bool ShowRefreshRateInStatusBar { get; set; } = true;

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

    // -- Clickable links --------------------------------------------------

    /// <summary>When true, HTTP/HTTPS URLs are detected and Ctrl+Click opens them in the browser.</summary>
    public bool ClickableLinksEnabled  { get; set; } = true;

    /// <summary>When true, email addresses are detected and Ctrl+Click opens the mail client.</summary>
    public bool ClickableEmailsEnabled { get; set; } = true;

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

// ─── Auto-Hide Settings Notification ──────────────────────────────────────────

/// <summary>
/// Static notification for auto-hide settings changes, mirroring
/// <see cref="TabPreviewAppSettings.Changed"/> pattern.
/// </summary>
public static class AutoHideAppSettings
{
    /// <summary>Raised when auto-hide timing settings are flushed.</summary>
    public static event Action? Changed;

    /// <summary>Signal that settings have been updated. Called by DockingOptionsPage.Flush().</summary>
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

// ----------------------------------------------------------------------------
// Layout Customization Settings (Customize Layout popup)
// ----------------------------------------------------------------------------

/// <summary>
/// User preferences for IDE layout visibility, element positions, and layout modes.
/// Persisted to JSON and restored on app startup.
/// </summary>
public sealed class LayoutSettings
{
    // -- Visibility --------------------------------------------------------

    /// <summary>Show the main menu bar. Default: true.</summary>
    public bool ShowMenuBar { get; set; } = true;

    /// <summary>Show the main toolbar row. Default: true.</summary>
    public bool ShowToolbar { get; set; } = true;

    /// <summary>Show the status bar. Default: true.</summary>
    public bool ShowStatusBar { get; set; } = true;

    // -- Positions ---------------------------------------------------------

    /// <summary>Toolbar position: "Top" or "Bottom". Default: "Top".</summary>
    public string ToolbarPosition { get; set; } = "Top";

    /// <summary>Default dock side for new panels: "Left", "Right", or "Bottom". Default: "Right".</summary>
    public string DefaultPanelDockSide { get; set; } = "Right";

    /// <summary>Tab bar position: "Top" or "Bottom". Default: "Top".</summary>
    public string TabBarPosition { get; set; } = "Top";

    // -- Layout Modes ------------------------------------------------------

    /// <summary>Whether Zen Mode is active. Default: false.</summary>
    public bool IsZenMode { get; set; }

    /// <summary>Whether Focused Mode is active. Default: false.</summary>
    public bool IsFocusedMode { get; set; }

    /// <summary>Whether Presentation Mode is active. Default: false.</summary>
    public bool IsPresentationMode { get; set; }

    // -- Presentation Mode -------------------------------------------------

    /// <summary>Font scale multiplier for Presentation Mode (1.0–3.0). Default: 1.5.</summary>
    public double PresentationFontScale { get; set; } = 1.5;

    // -- Zen Mode Configuration -------------------------------------------

    /// <summary>Hide menu bar when entering Zen Mode. Default: true.</summary>
    public bool ZenHideMenuBar { get; set; } = true;

    /// <summary>Hide toolbar when entering Zen Mode. Default: true.</summary>
    public bool ZenHideToolbar { get; set; } = true;

    /// <summary>Hide status bar when entering Zen Mode. Default: true.</summary>
    public bool ZenHideStatusBar { get; set; } = true;

    /// <summary>Hide all dock panels when entering Zen Mode. Default: true.</summary>
    public bool ZenHidePanels { get; set; } = true;
}

// ── Marketplace settings ──────────────────────────────────────────────────────

/// <summary>
/// Plugin Marketplace preferences — GitHub API authentication and auto-update behavior.
/// </summary>
public sealed class MarketplaceSettings
{
    /// <summary>
    /// Optional GitHub Personal Access Token (PAT) used to raise the API rate limit
    /// from 60 to 5 000 requests per hour. Leave empty for anonymous access.
    /// </summary>
    public string GitHubToken { get; set; } = string.Empty;

    /// <summary>
    /// When true, the marketplace checks for plugin updates automatically at startup.
    /// Default: true.
    /// </summary>
    public bool AutoCheckUpdates { get; set; } = true;

    /// <summary>
    /// Interval in hours between automatic update checks.
    /// Minimum enforced value: 1 hour. Default: 24.
    /// </summary>
    public int UpdateCheckIntervalHours { get; set; } = 24;
}

// ----------------------------------------------------------------------------
// Document Settings (External File Changes)
// ----------------------------------------------------------------------------

/// <summary>
/// Static notification hub for document settings changes.
/// </summary>
public static class DocumentsAppSettings
{
    /// <summary>Raised when document settings are flushed from the Options dialog.</summary>
    public static event Action? Changed;

    /// <summary>Signal that document settings have been updated.</summary>
    public static void NotifyChanged() => Changed?.Invoke();
}

/// <summary>
/// Settings for external file change detection, auto-reload, and ignored directories.
/// Presented in the Options dialog under Environment > Documents.
/// </summary>
public sealed class DocumentSettings
{
    /// <summary>
    /// When true, the IDE monitors project directories for external file changes
    /// and shows a warning badge on modified files in the Solution Explorer.
    /// </summary>
    public bool DetectExternalFileChanges { get; set; } = true;

    /// <summary>
    /// When true, externally modified files are silently reloaded instead of
    /// showing a warning badge. Only effective when <see cref="DetectExternalFileChanges"/> is true.
    /// </summary>
    public bool AutoReloadExternalChanges { get; set; }

    /// <summary>
    /// Semicolon-separated list of directory names to exclude from file watching.
    /// Changes inside these directories never trigger external-modification warnings.
    /// </summary>
    public string IgnoredDirectories { get; set; } = "bin;obj;.vs;.git;node_modules";
}

/// <summary>
/// Document Structure panel preferences.
/// </summary>
public sealed class DocumentStructureSettings
{
    /// <summary>
    /// Index into the max-depth dropdown (0 = unlimited, 1 = 2, 2 = 3, 3 = 5, 4 = 10).
    /// Controls how many levels deep the structure tree is rendered.
    /// Default = 0 (unlimited).
    /// </summary>
    public int MaxDepthIndex { get; set; } = 0;
}
