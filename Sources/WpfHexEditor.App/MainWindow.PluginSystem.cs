
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// MainWindow — Plugin System Integration
// Partial class responsible for:
//   - Constructing all service adapters (DockingAdapter, MenuAdapter, etc.)
//   - Building IDEHostContext and WpfPluginHost
//   - Calling DiscoverPluginsAsync + LoadAllAsync on startup
//   - Wiring FocusContextService on ActiveItemChanged
//   - Surfacing SlowPlugin / PluginCrash messages to OutputPanel
//   - Opening the Plugin Manager document tab (Tools > Plugin Manager)
//
// Pattern: Adapter — all App internals are wrapped in adapter interfaces
//          so PluginHost never references WpfHexEditor.App directly.

using System.Threading;
using System.Windows;
using WpfHexEditor.App.Services;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.PluginHost;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.PluginHost.UI;
using WpfHexEditor.SDK.Contracts.Focus;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // ─── Plugin system singletons ───────────────────────────────────────
    private WpfPluginHost? _pluginHost;
    private readonly FocusContextService _focusContextService = new();

    // Service adapters (lazily set in InitializePluginSystemAsync after layout is ready)
    private HexEditorServiceImpl? _hexEditorService;
    private OutputServiceImpl? _outputService;
    private ErrorPanelServiceImpl? _errorPanelService;
    private ThemeServiceImpl? _themeService;

    private const string PluginManagerContentId = "plugin-manager";

    // ─── Startup ────────────────────────────────────────────────────────

    /// <summary>
    /// Called from OnLoaded (after layout is ready) to bootstrap the full plugin system.
    /// </summary>
    private async Task InitializePluginSystemAsync()
    {
        try
        {
            // 1. Build service adapters
            _hexEditorService = new HexEditorServiceImpl();
            _outputService = new OutputServiceImpl();
            _errorPanelService = new ErrorPanelServiceImpl();
            _themeService = new ThemeServiceImpl();

            if (_outputPanel is not null)
                _outputService.SetOutputPanel(_outputPanel);
            if (_errorPanel is not null)
                _errorPanelService.SetErrorPanel(_errorPanel);

            var dockingAdapter = new DockingAdapter(_engine, _layout, DockHost, StoreContent);
            var menuAdapter = new MenuAdapter(MainMenuBar);
            var statusBarAdapter = new StatusBarAdapter(AppStatusBar);

            // 2. Build PluginHost singletons
            var permissionService = new PermissionService();
            var eventBus = new PluginEventBus();
            var uiRegistry = new UIRegistry(dockingAdapter, menuAdapter, statusBarAdapter);

            var solutionService = new SolutionExplorerServiceImpl(_solutionManager);
            var codeEditorService = new NullCodeEditorService();
            var parsedFieldService = new NullParsedFieldService();

            var hostContext = new IDEHostContext(
                solutionExplorer: solutionService,
                hexEditor: _hexEditorService,
                codeEditor: codeEditorService,
                output: _outputService,
                parsedField: parsedFieldService,
                errorPanel: _errorPanelService,
                focusContext: _focusContextService,
                eventBus: eventBus,
                uiRegistry: uiRegistry,
                theme: _themeService,
                permissions: permissionService);

            // 3. Create orchestrator
            _pluginHost = new WpfPluginHost(hostContext, uiRegistry, permissionService, Dispatcher);

            // 4. Subscribe to host events
            _pluginHost.PluginCrashed += OnPluginCrashed;
            _pluginHost.SlowPluginDetected += OnSlowPluginDetected;

            // 5. Discover + load all plugins
            var binDir = AppDomain.CurrentDomain.BaseDirectory;
            var bundledPluginsDir = Path.Combine(binDir, "Plugins");
            await _pluginHost.LoadAllAsync(
                extraDirectories: Directory.Exists(bundledPluginsDir) ? [bundledPluginsDir] : null,
                ct: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _outputPanel?.Error($"[PluginSystem] Failed to initialize: {ex.Message}");
        }
    }

    // ─── Focus context wiring ────────────────────────────────────────────

    /// <summary>
    /// Called by OnActiveDocumentChanged to push focus changes into FocusContextService.
    /// </summary>
    private void UpdatePluginFocusContext(DockItem? activeItem)
    {
        if (activeItem is null)
        {
            _focusContextService.SetActiveDocument(null);
            return;
        }

        // Resolve the actual editor content
        _contentCache.TryGetValue(activeItem.ContentId, out var content);

        IDocument? document = content is IDocument d ? d : new SimpleDockItemDocument(activeItem);
        _focusContextService.SetActiveDocument(document);
    }

    // ─── Plugin notification handlers ────────────────────────────────────

    private void OnSlowPluginDetected(object? sender, SlowPluginDetectedEventArgs e)
    {
        _outputPanel?.Info(
            $"[PluginSystem] Warning: Plugin '{e.PluginName}' is slow " +
            $"(avg {e.AverageExecutionTime.TotalMilliseconds:F0} ms, " +
            $"threshold {e.Threshold.TotalMilliseconds:F0} ms).");
    }

    private void OnPluginCrashed(object? sender, PluginFaultedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _outputPanel?.Error(
                $"[PluginSystem] Plugin '{e.PluginName}' crashed during '{e.Phase}': {e.Exception.Message}");
        });
    }

    // ─── Plugin Manager document tab ────────────────────────────────────

    private void OnOpenPluginManager(object sender, RoutedEventArgs e)
    {
        if (_pluginHost is null) return;

        // Reuse existing tab
        var existing = _layout?.FindItemByContentId(PluginManagerContentId);
        if (existing is not null)
        {
            _engine.Activate(existing);
            return;
        }

        var vm = new PluginManagerViewModel(_pluginHost, Dispatcher);
        var control = new PluginManagerControl(vm);

        var item = new DockItem
        {
            ContentId = PluginManagerContentId,
            Title = "Plugin Manager",
            CanClose = true
        };

        StoreContent(PluginManagerContentId, control);
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
    }

    // ─── Plugin system shutdown ──────────────────────────────────────────

    private async Task ShutdownPluginSystemAsync()
    {
        if (_pluginHost is null) return;
        _pluginHost.PluginCrashed -= OnPluginCrashed;
        _pluginHost.SlowPluginDetected -= OnSlowPluginDetected;
        await _pluginHost.DisposeAsync().ConfigureAwait(false);
        _pluginHost = null;
    }

    // ─── Minimal helpers ────────────────────────────────────────────────

    /// <summary>
    /// Minimal IDocument adapter wrapping a DockItem for FocusContextService.
    /// </summary>
    private sealed class SimpleDockItemDocument : IDocument
    {
        private readonly DockItem _item;
        public SimpleDockItemDocument(DockItem item) => _item = item;
        public string ContentId => _item.ContentId;
        public string Title => _item.Title ?? _item.ContentId;
        public string? FilePath => _item.Metadata?.TryGetValue("FilePath", out var fp) == true ? fp : null;
        public string DocumentType => "DockItem";
        public bool IsDirty => false;
    }
}
