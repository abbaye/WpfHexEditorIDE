//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Editor.Core.Notifications;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Complete IDE host context exposing all services available to plugins.
/// Passed to <see cref="IWpfHexEditorPlugin.InitializeAsync"/> by PluginHost.
/// </summary>
public interface IIDEHostContext
{
    // -- Document Management --------------------------------------------------

    /// <summary>
    /// High-level document host service for opening, activating and navigating to
    /// document tabs. Use this to open files from plugins or IDE panels (ErrorList, etc.)
    /// without holding a direct reference to MainWindow.
    /// </summary>
    IDocumentHostService DocumentHost { get; }

    // -- IDE Services ---------------------------------------------------------

    /// <summary>Access to the Solution Explorer for file/project navigation.</summary>
    ISolutionExplorerService SolutionExplorer { get; }

    /// <summary>
    /// Access to the WH native project/solution manager.
    /// Null when the host does not expose this service (e.g. sandboxed plugins).
    /// Use to add generated files to an open WH project via <see cref="ISolutionManager.CreateItemAsync"/>.
    /// </summary>
    ISolutionManager? SolutionManager { get; }

    /// <summary>Access to the active HexEditor content and selection state.</summary>
    IHexEditorService HexEditor { get; }

    /// <summary>Access to the active CodeEditor content and cursor state.</summary>
    ICodeEditorService CodeEditor { get; }

    /// <summary>Access to the IDE OutputPanel for plugin log messages.</summary>
    IOutputService Output { get; }

    /// <summary>Access to parsed field data from the active binary document.</summary>
    IParsedFieldService ParsedField { get; }

    /// <summary>Access to the IDE ErrorPanel for plugin diagnostics.</summary>
    IErrorPanelService ErrorPanel { get; }

    // -- Cross-Plugin Services ------------------------------------------------

    /// <summary>Focus-aware service tracking active document and panel.</summary>
    IFocusContextService FocusContext { get; }

    /// <summary>
    /// Plugin-to-plugin event bus for decoupled inter-plugin communication.
    /// For IDE-wide events (FileOpened, SelectionChanged, etc.) use <see cref="IDEEvents"/>.
    /// </summary>
    IPluginEventBus EventBus { get; }

    /// <summary>
    /// IDE-level event bus for subscribing to and publishing typed IDE events
    /// (FileOpenedEvent, EditorSelectionChangedEvent, PluginLoadedEvent, etc.).
    /// Backed by WpfHexEditor.Core.Events.IDEEventBus; bridged to sandbox plugins via IPC.
    /// </summary>
    IIDEEventBus IDEEvents { get; }

    // -- UI Integration -------------------------------------------------------

    /// <summary>
    /// Registry for contributing UI elements (panels, menus, toolbars, status bar items).
    /// All contributed elements are automatically removed on plugin unload.
    /// </summary>
    IUIRegistry UIRegistry { get; }

    // -- IDE Features ----------------------------------------------------------

    /// <summary>
    /// Theme service providing current IDE theme resources and theme change notifications.
    /// All plugin WPF controls must use this service for color/brush bindings.
    /// </summary>
    IThemeService Theme { get; }

    /// <summary>
    /// Runtime permission service for checking and responding to permission changes.
    /// Plugins must degrade gracefully when permissions are revoked.
    /// </summary>
    IPermissionService Permissions { get; }

    /// <summary>
    /// Write access to the IDE Terminal panel.
    /// Use this to emit Standard, Info, Warning, or Error lines from plugin commands or background tasks.
    /// </summary>
    ITerminalService Terminal { get; }

    // -- Plugin Discovery & Extension Points ----------------------------------

    /// <summary>
    /// Registry for querying semantic feature declarations across all known plugins.
    /// Use <see cref="SDK.Models.PluginFeature"/> constants or custom strings.
    /// Example: <c>CapabilityRegistry.FindPluginsWithFeature(PluginFeature.BinaryAnalyzer)</c>
    /// </summary>
    IPluginCapabilityRegistry CapabilityRegistry { get; }

    /// <summary>
    /// Registry for plugin extension point contributions.
    /// IDE modules call <c>GetExtensions&lt;T&gt;()</c> to obtain all contributor implementations
    /// for a given extension point without knowing which plugins provided them.
    /// </summary>
    IExtensionRegistry ExtensionRegistry { get; }

    // -- Command System -------------------------------------------------------

    /// <summary>
    /// Plugin-facing command registry. Register commands on plugin load;
    /// unregister on unload. Registered commands appear in the Command Palette
    /// and the Keyboard Shortcuts options page.
    /// Returns null when the host does not support the command system (e.g. sandboxed plugins).
    /// </summary>
    ICommandRegistry? CommandRegistry => null;

    // -- LSP (Language Server Protocol) ---------------------------------------

    /// <summary>
    /// Registry of configured LSP server executables keyed by language / extension.
    /// Use <see cref="ILspServerRegistry.CreateClient"/> to obtain an
    /// <see cref="ILspClient"/> for a specific file.
    /// Null when the LSP.Client assembly is not loaded.
    /// </summary>
    ILspServerRegistry? LspServers { get; }

    // -- Debugger -------------------------------------------------------------

    /// <summary>
    /// Integrated debugger service. Use to query session state, toggle breakpoints,
    /// and subscribe to debug lifecycle events.
    /// Null when the debugger assembly is not loaded or the host does not support debugging.
    /// </summary>
    IDebuggerService? Debugger => null;

    // -- Scripting ------------------------------------------------------------

    /// <summary>
    /// C# scripting engine. Use to run or validate scripts on behalf of the user.
    /// Null when the scripting assembly is not loaded.
    /// </summary>
    IScriptingService? Scripting => null;

    // -- Build System ---------------------------------------------------------

    /// <summary>
    /// Orchestrates compilation of solutions and individual projects.
    /// Null when no build system is configured (e.g. no solution loaded).
    /// </summary>
    IBuildSystem? BuildSystem => null;

    // -- Unit Test Runner -----------------------------------------------------

    /// <summary>
    /// Service for running unit tests inside the IDE.
    /// Null when the UnitTesting plugin is not loaded.
    /// </summary>
    ITestRunnerService? TestRunner => null;

    // -- Diff Service ---------------------------------------------------------

    /// <summary>
    /// Service for comparing files (diff) and opening the DiffHub viewer.
    /// Null when the FileComparison plugin is not loaded.
    /// </summary>
    IDiffService? DiffService => null;

    // -- Format Catalog -------------------------------------------------------

    /// <summary>
    /// Shared format definition catalog (427+ whfmt formats).
    /// Loaded once at app startup. All format detection consumers use this catalog
    /// transparently. Null only in sandboxed/standalone contexts.
    /// </summary>
    IFormatCatalogService? FormatCatalog => null;

    // -- Format Parsing -------------------------------------------------------

    /// <summary>
    /// Universal format detection and field parsing service.
    /// Decoupled from any specific editor — works with any <see cref="IBinaryDataSource"/>.
    /// Auto-attaches to the active editor on tab switch when the editor provides a data source.
    /// Null when format parsing is not available.
    /// </summary>
    IFormatParsingService? FormatParsing => null;

    // -- Workspace System -----------------------------------------------------

    /// <summary>
    /// Read-only view of the active workspace (.whidews) state and lifecycle events.
    /// Null when no workspace support is configured.
    /// </summary>
    IWorkspaceService? Workspace => null;

    // -- Notification Center --------------------------------------------------

    /// <summary>
    /// IDE-wide notification service. Post user-visible alerts, progress messages,
    /// and first-run prompts. Never null after IDE v0.8.
    /// </summary>
    INotificationService? Notifications => null;

    // -- Syntax Coloring ------------------------------------------------------

    /// <summary>
    /// Centralised syntax coloring service. Resolves languages from IDs, file paths,
    /// or common aliases and returns themed <see cref="Services.ColoredSpan"/> tokens.
    /// Replaces duplicated TokenKindToBrush / BuildHighlighter patterns across plugins.
    /// </summary>
    ISyntaxColoringService? SyntaxColoring => null;

    /// <summary>
    /// Factory for creating shared UI controls from the App layer.
    /// Plugins use this to obtain syntax-highlighted previews and other
    /// App-provided controls without referencing WpfHexEditor.App directly.
    /// </summary>
    IUIControlFactory? UIFactory => null;

    // -- Version Control ------------------------------------------------------

    /// <summary>
    /// Generic version control facade (blame, status, stage/unstage).
    /// Registered by WpfHexEditor.Plugins.Git on startup via ExtensionRegistry.
    /// Null when no VCS plugin is loaded — no breaking change for existing plugins.
    /// </summary>
    IVersionControlService? VersionControl
        => ExtensionRegistry?.GetExtensions<IVersionControlService>().FirstOrDefault();

    // -- Tab Groups -----------------------------------------------------------

    /// <summary>
    /// Document tab group management (split, move, close, focus).
    /// Null in sandboxed or non-IDE host contexts.
    /// </summary>
    ITabGroupService? TabGroups => null;
}
