//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.Events;
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
    /// Backed by WpfHexEditor.Events.IDEEventBus; bridged to sandbox plugins via IPC.
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
}
