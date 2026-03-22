
//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
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

using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.App.Services;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.Core.Terminal;
using WpfHexEditor.Core.Terminal.ShellSession;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Events;
using WpfHexEditor.Events.IDEEvents;
using WpfHexEditor.PluginHost;
using WpfHexEditor.PluginHost.DevTools;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.PluginHost.UI;
using WpfHexEditor.PluginHost.UI.Options;
using WpfHexEditor.Options;
using WpfHexEditor.BuildSystem;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.Terminal;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // --- Plugin system singletons ---------------------------------------
    private WpfPluginHost? _pluginHost;
    private IDEHostContext? _ideHostContext;
    private IDEEventBus? _ideEventBus;
    private DocumentHostService? _documentHostService;
    private readonly FocusContextService _focusContextService = new();

    // Service adapters (lazily set in InitializePluginSystemAsync after layout is ready)
    private DockingAdapter? _dockingAdapter;
    private HexEditorServiceImpl? _hexEditorService;
    private OutputServiceImpl? _outputService;
    private ErrorPanelServiceImpl? _errorPanelService;
    private ThemeServiceImpl? _themeService;
    private TerminalServiceImpl? _terminalService;

    private const string PluginManagerContentId   = "plugin-manager";
    private const string TerminalPanelContentId   = "panel-terminal";
    private const string PluginMonitorContentId   = "plugin-monitor";
    private const string MarketplaceContentId     = "plugin-marketplace";

    private int    _pluginFaultCount = 0;
    private string? _infoBarPluginId;   // ID of the plugin currently shown in the InfoBar

    // Panels restored from a saved layout before the plugin system was ready.
    // Their DataContext is wired up at the end of InitializePluginSystemAsync.
    private TerminalPanel? _pendingTerminalPanel;
    private WpfHexEditor.Panels.IDE.Panels.PluginMonitoringPanel? _pendingPluginMonitorPanel;
    private WpfHexEditor.PluginHost.UI.PluginManagerControl? _pendingPluginManagerControl;

    // VS solution path deferred from TryRestoreSession() because plugin loaders are not yet
    // registered at startup. Opened at the end of InitializePluginSystemAsync once loaders are live.
    private string? _pendingRestoreSolutionPath;

    // Dev tools (instantiated on first use)
    private PluginDevLoader? _pluginDevLoader;

    // StatusBar fault-blink timer (DispatcherTimer, 800 ms toggle)
    private DispatcherTimer? _statusBarBlinkTimer;
    private bool _statusBarBlinkState;
    private string? _slowPluginName;

    // --- Startup --------------------------------------------------------

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
            _terminalService = new TerminalServiceImpl();

            if (_errorPanel is not null)
                _errorPanelService.SetErrorPanel(_errorPanel);

            _dockingAdapter = new DockingAdapter(_engine, _layout, DockHost, StoreContent, _layoutWasRestoredFromFile);

            // Seed the left-side anchor with the built-in Solution Explorer so that plugin panels
            // declaring DefaultDockSide = "Left" are automatically tabbed into the same group.
            _dockingAdapter.SeedSideAnchor(DockDirection.Left, SolutionExplorerContentId);

            var dockingAdapter = _dockingAdapter;
            var menuAdapter = new MenuAdapter(MainMenuBar);
            var statusBarAdapter = new StatusBarAdapter(AppStatusBar);

            // 2. Build PluginHost singletons
            var permissionService = new PermissionService();
            var eventBus = new PluginEventBus();
            var uiRegistry = new UIRegistry(dockingAdapter, menuAdapter, statusBarAdapter);

            var solutionService = new SolutionExplorerServiceImpl(_solutionManager);
            solutionService.OpenFileHandler = path => Dispatcher.InvokeAsync(() => OpenStandaloneFileWithEditor(path, null)).Task;
            var codeEditorService = new NullCodeEditorService();
            var parsedFieldService = new NullParsedFieldService();

            // 2b. Construct IDE EventBus + Extension/Capability services
            _ideEventBus = new IDEEventBus();
            RegisterWellKnownEvents(_ideEventBus);

            var capabilityAdapter = new PluginCapabilityRegistryAdapter();
            var extensionRegistry = new ExtensionRegistry();

            // DocumentHostService bridges the high-level document API to MainWindow's
            // docking infrastructure via the openFileHandler callback.
            _documentHostService = new DocumentHostService(
                _documentManager,
                (path, editorId) => Dispatcher.InvokeAsync(() =>
                    OpenStandaloneFileWithEditor(path, editorId)).Task);

            var hostContext = new IDEHostContext(
                documentHost:        _documentHostService,
                solutionExplorer:    solutionService,
                hexEditor:           _hexEditorService,
                codeEditor:          codeEditorService,
                output:              _outputService,
                parsedField:         parsedFieldService,
                errorPanel:          _errorPanelService,
                focusContext:        _focusContextService,
                eventBus:            eventBus,
                uiRegistry:          uiRegistry,
                theme:               _themeService,
                permissions:         permissionService,
                terminal:            _terminalService,
                ideEvents:           _ideEventBus,
                capabilityRegistry:  capabilityAdapter,
                extensionRegistry:   extensionRegistry,
                solutionManager:     _solutionManager);

            // 3. Create orchestrator
            _ideHostContext = hostContext;
            _pluginHost = new WpfPluginHost(hostContext, uiRegistry, permissionService, Dispatcher,
                logger:      msg => OutputLogger.PluginInfo(msg),
                errorLogger: msg => OutputLogger.PluginError(msg));

            // 3b. Resolve circular dependency: capability registry now backed by the host's entries.
            capabilityAdapter.SetInner(_pluginHost.CapabilityRegistry);

            // 3b-2. Initialize build system (needs _ideEventBus + _outputService + _solutionManager).
            InitializeBuildSystem();

            // 3b-3. Register Plugin Development options page.
            WpfHexEditor.PluginDev.Options.PluginDevRegistrar.Register();

            // 3c. Wire IDE-level events to IDEEventBus publishers.
            if (_hexEditorService is not null)
                _hexEditorService.SelectionChanged += OnHexEditorSelectionChanged;

            // 4. Subscribe to host events
            _pluginHost.PluginCrashed       += OnPluginCrashed;
            _pluginHost.SlowPluginDetected  += OnSlowPluginDetected;
            _pluginHost.PluginLoaded        += OnPluginLoadedOrUnloaded;
            _pluginHost.PluginUnloaded      += OnPluginLoadedOrUnloaded;

            // 4b. Register Plugin System → Migration options page (requires _pluginHost instance).
            var capturedHost = _pluginHost;
            OptionsPageRegistry.RegisterDynamic(
                "Plugin System",
                "Migration",
                () => new PluginMigrationOptionsPage(capturedHost));

            // 4c. Register Plugin System → Event Bus options page.
            var capturedBus = _ideEventBus;
            OptionsPageRegistry.RegisterDynamic(
                "Plugin System",
                "Event Bus",
                () => new IDEEventBusOptionsPage(capturedBus));

            // 5. Discover + load all plugins.
            // Suspend visual tree rebuilds so that N plugins each registering a panel
            // do not trigger N full RebuildVisualTree() calls on the UI thread.
            // A single rebuild is performed by ResumeRebuild() after all plugins are loaded.
            var binDir = AppDomain.CurrentDomain.BaseDirectory;
            var bundledPluginsDir = Path.Combine(binDir, "Plugins");
            OutputLogger.PluginInfo($"[PluginSystem] Plugins dir: {bundledPluginsDir} (exists: {Directory.Exists(bundledPluginsDir)})");
            dockingAdapter.SuspendRebuild();
            try
            {
                await _pluginHost.LoadAllAsync(
                    extraDirectories: Directory.Exists(bundledPluginsDir) ? [bundledPluginsDir] : null,
                    ct: CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                // ResumeRebuild must run on the UI thread; dispatch back after ConfigureAwait(false).
                await Dispatcher.InvokeAsync(dockingAdapter.ResumeRebuild);
            }

            // Bridge IBuildAdapter extensions registered by plugins into the build system.
            // Must run after LoadAllAsync so that MSBuildPlugin has registered its adapter.
            if (_buildSystem is not null)
                foreach (var adapter in hostContext.ExtensionRegistry.GetExtensions<IBuildAdapter>())
                    _buildSystem.RegisterAdapter(adapter);

            // Register a dynamic Options page for every in-process plugin that supports IPluginWithOptions.
            foreach (var entry in _pluginHost.OptionsRegistry.GetAll())
            {
                var captured = entry;
                OptionsPageRegistry.RegisterDynamic(
                    "Plugins",
                    captured.PluginName,
                    () =>
                    {
                        captured.Plugin.LoadOptions();
                        var page = captured.Plugin.CreateOptionsPage();
                        if (page is System.Windows.Controls.UserControl uc) return uc;
                        // Wrap arbitrary FrameworkElement in a UserControl for the Options panel.
                        var wrapper = new System.Windows.Controls.UserControl { Content = page };
                        return wrapper;
                    });
            }

            // Phase 11: Register options pages for sandbox plugins that implement IPluginWithOptions.
            // Each sandbox plugin eagerly creates its options page HwndSource during init and sends
            // the HWND here. We wrap it in HwndPanelHost inside a UserControl for the Options dialog.
            foreach (var (pluginId, pluginName, hwnd) in _pluginHost.GetSandboxOptionsPages())
            {
                var capturedId   = pluginId;
                var capturedHwnd = new IntPtr(hwnd);
                OptionsPageRegistry.RegisterDynamic(
                    "Plugins",
                    pluginName,
                    () =>
                    {
                        var host    = new WpfHexEditor.PluginHost.Sandbox.HwndPanelHost(capturedHwnd, capturedId);
                        var wrapper = new System.Windows.Controls.UserControl
                        {
                            Content = host,
                            MinWidth  = 400,
                            MinHeight = 300,
                        };
                        return wrapper;
                    });
            }

            // Wire up panels that were restored from a saved layout before the plugin
            // system was ready (DataContext was null at construction time).
            await Dispatcher.InvokeAsync(() =>
            {
                if (_pendingTerminalPanel is not null)
                {
                    var vm = new TerminalPanelViewModel(hostContext);
                    _terminalService?.SetOutput(vm.GetActiveOutput());
                    _terminalService?.SetSessionManager(vm.SessionManager);
                    _pendingTerminalPanel.DataContext = vm;
                    _pendingTerminalPanel = null;
                }

                if (_pendingPluginMonitorPanel is not null && _pluginHost is not null)
                {
                    var vm = new WpfHexEditor.Panels.IDE.Panels.ViewModels.PluginMonitoringViewModel(_pluginHost, Dispatcher, _outputService);
                    _pendingPluginMonitorPanel.DataContext = vm;
                    _pendingPluginMonitorPanel = null;
                }

                if (_pendingPluginManagerControl is not null && _pluginHost is not null)
                {
                    var vm = new PluginManagerViewModel(
                        _pluginHost, 
                        Dispatcher,
                        () =>
                        {
                            var ps = AppSettingsService.Instance.Current.PluginSystem;
                            return (
                                ps.MemoryWarningThresholdMB, 
                                ps.MemoryHighThresholdMB, 
                                ps.MemoryCriticalThresholdMB, 
                                ps.EnableMemoryAlerts,
                                ps.MemoryNormalColor,
                                ps.MemoryWarningColor,
                                ps.MemoryHighColor,
                                ps.MemoryCriticalColor
                            );
                        });
                    _pendingPluginManagerControl.DataContext = vm;
                    _pendingPluginManagerControl = null;
                }

                // Initialise the status bar blink timer (fault indicator)
                _statusBarBlinkTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(800)
                };
                _statusBarBlinkTimer.Tick += OnStatusBarBlinkTick;

                UpdatePluginStatusBar();

                // Enable "Open Folder…" only when a loader for .whfolder is registered.
                // The menu item is hidden at startup (IsEnabled=false set in XAML) and revealed here.
                if (MenuOpenFolder is not null)
                {
                    MenuOpenFolder.IsEnabled = extensionRegistry
                        .GetExtensions<ISolutionLoader>()
                        .Any(l => l.SupportedExtensions
                                   .Contains("whfolder", StringComparer.OrdinalIgnoreCase));
                }

                // Restore a VS solution that was deferred in TryRestoreSession() because the
                // plugin loaders (ISolutionLoader extensions) were not yet registered at that point.
                if (!string.IsNullOrEmpty(_pendingRestoreSolutionPath))
                {
                    var deferredPath = _pendingRestoreSolutionPath;
                    _pendingRestoreSolutionPath = null;
                    _ = OpenSolutionAsync(deferredPath);
                }
            });
        }
        catch (Exception ex)
        {
            OutputLogger.PluginError($"[PluginSystem] Failed to initialize: {ex.Message}");
        }
    }

    // --- Focus context wiring --------------------------------------------

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

        // Panel tabs (tool windows) must not override the active document.
        // Focusing a tool panel does not change the active editor — VS-like behavior.
        if (activeItem.ContentId.StartsWith("panel-", StringComparison.Ordinal))
            return;

        // Resolve the actual editor content
        _contentCache.TryGetValue(activeItem.ContentId, out var content);

        IDocument? document = content is IDocument d ? d : new SimpleDockItemDocument(activeItem, content as IDocumentEditor);
        _focusContextService.SetActiveDocument(document);
    }

    // --- Plugin notification handlers ------------------------------------

    private void OnPluginLoadedOrUnloaded(object? sender, EventArgs e)
        => Dispatcher.InvokeAsync(() => UpdatePluginStatusBar());

    private void OnSlowPluginDetected(object? sender, SlowPluginDetectedEventArgs e)
    {
        _slowPluginName = e.PluginName;
        OutputLogger.PluginWarn(
            $"[PluginSystem] Plugin '{e.PluginName}' is slow " +
            $"(avg {e.AverageExecutionTime.TotalMilliseconds:F0} ms, " +
            $"threshold {e.Threshold.TotalMilliseconds:F0} ms).");

        Dispatcher.InvokeAsync(() =>
        {
            UpdatePluginStatusBar();
            ShowPluginInfoBar(
                e.PluginId,
                $"Plugin '{e.PluginName}' is slowing down the IDE " +
                $"(avg {e.AverageExecutionTime.TotalMilliseconds:F0} ms).",
                showDisable: true, showRestart: true);
        });
    }

    private void OnStatusBarBlinkTick(object? sender, EventArgs e)
    {
        _statusBarBlinkState = !_statusBarBlinkState;
        PluginWarningBadge.Opacity = _statusBarBlinkState ? 1.0 : 0.3;
    }

    // --- Plugin InfoBar (VS-style notification banner) -------------------

    private void ShowPluginInfoBar(string pluginId, string message, bool showDisable, bool showRestart)
    {
        _infoBarPluginId = pluginId;
        PluginInfoBarMessage.Text = message;
        PluginInfoBarDisable.Visibility = showDisable  ? Visibility.Visible : Visibility.Collapsed;
        PluginInfoBarRestart.Visibility  = showRestart ? Visibility.Visible : Visibility.Collapsed;
        PluginInfoBarIgnore.Visibility   = (showDisable || showRestart) ? Visibility.Visible : Visibility.Collapsed;
        PluginInfoBar.Visibility = Visibility.Visible;
    }

    private void HidePluginInfoBar()
    {
        PluginInfoBar.Visibility = Visibility.Collapsed;
        _infoBarPluginId = null;
    }

    private void OnPluginInfoBarDisable(object sender, RoutedEventArgs e)
    {
        if (_infoBarPluginId is not null)
            _ = _pluginHost?.DisablePluginAsync(_infoBarPluginId);
        HidePluginInfoBar();
    }

    private async void OnPluginInfoBarRestart(object sender, RoutedEventArgs e)
    {
        if (_infoBarPluginId is not null && _pluginHost is not null)
            await _pluginHost.ReloadPluginAsync(_infoBarPluginId).ConfigureAwait(true);
        HidePluginInfoBar();
    }

    private void OnPluginInfoBarIgnore(object sender, RoutedEventArgs e) => HidePluginInfoBar();

    private void OnPluginInfoBarClose(object sender, RoutedEventArgs e)  => HidePluginInfoBar();

    private void OnPluginCrashed(object? sender, PluginFaultedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            OutputLogger.PluginError(
                $"[PluginSystem] Plugin '{e.PluginName}' crashed during '{e.Phase}': {e.Exception.Message}");

            _pluginFaultCount++;
            UpdatePluginStatusBar();

            ShowPluginInfoBar(
                e.PluginId,
                $"Plugin '{e.PluginName}' crashed: {e.Exception.Message}",
                showDisable: false, showRestart: true);
        });
    }

    // --- Plugin status bar ----------------------------------------------------

    /// <summary>
    /// Refreshes the plugin count, fault badge, tooltip, and blink state in the status bar.
    /// Must be called on the Dispatcher thread.
    /// </summary>
    private void UpdatePluginStatusBar()
    {
        if (_pluginHost is null) return;

        // Show fault badge when one or more plugins have crashed; blink to draw attention.
        if (_pluginFaultCount > 0)
        {
            PluginWarningText.Text        = _pluginFaultCount.ToString();
            PluginWarningBadge.Visibility = Visibility.Visible;
            PluginWarningBadge.Opacity    = 1.0;
            if (_statusBarBlinkTimer is not null && !_statusBarBlinkTimer.IsEnabled)
                _statusBarBlinkTimer.Start();
        }
        else
        {
            _statusBarBlinkTimer?.Stop();
            PluginWarningBadge.Visibility = Visibility.Collapsed;
            PluginWarningBadge.Opacity    = 1.0;
        }
    }

    private void OnPluginStatusBarClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            OnOpenPluginManager(sender, new RoutedEventArgs());
        else if (e.ChangedButton == System.Windows.Input.MouseButton.Right)
            OpenPluginQuickStatusPopup();
    }

    private void OpenPluginQuickStatusPopup()
    {
        if (_pluginHost is null) return;

        var content = new WpfHexEditor.App.Controls.PluginQuickStatusPopup
        {
            DataContext = new WpfHexEditor.App.ViewModels.PluginQuickStatusViewModel(
                _pluginHost,
                () => OnOpenPluginManager(this, new RoutedEventArgs()),
                () => OnInstallPluginFromStatusBar())
        };

        var popup = new System.Windows.Controls.Primitives.Popup
        {
            Child           = content,
            PlacementTarget = PluginWarningBadge,
            Placement       = System.Windows.Controls.Primitives.PlacementMode.Top,
            StaysOpen       = false,
            AllowsTransparency = true
        };
        popup.IsOpen = true;
    }

    /// <summary>Right-click on status bar plugin indicator → quick status popup.</summary>
    private void OnPluginStatusBarRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenPluginQuickStatusPopup();
    }

    /// <summary>Plugins > Install from File... menu item handler.</summary>
    internal void OnInstallPluginFromMenu(object sender, RoutedEventArgs e)
        => OnInstallPluginFromStatusBar();

    internal void OnInstallPluginFromStatusBar()
    {
        // Reuse existing Plugin Manager VM if open, else open plugin manager then invoke install
        OnOpenPluginManager(this, new RoutedEventArgs());
    }

    internal void OnRefreshAllPlugins(object sender, RoutedEventArgs e)
    {
        if (_pluginHost is null) return;
        _ = Task.Run(async () =>
        {
            foreach (var entry in _pluginHost.GetAllPlugins()
                         .Where(p => p.State == SDK.Models.PluginState.Loaded))
                await _pluginHost.ReloadPluginAsync(entry.Manifest.Id).ConfigureAwait(false);
        });
    }

    internal void OnOpenPluginSettings(object sender, RoutedEventArgs e)
        => OpenSettingsAt("Plugin System", "General");

    // --- Plugin Manager document tab ------------------------------------

    private void OnOpenPluginManager(object sender, RoutedEventArgs e)
    {
        if (_pluginHost is null) return;

        // Reuse existing tab
        var existing = _layout?.FindItemByContentId(PluginManagerContentId);
        if (existing is not null)
        {
            if (existing.Owner is { } owner) owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
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

    /// <summary>
    /// Opens (or focuses) the Plugin Manager tab and pre-selects the plugin
    /// matching <paramref name="pluginId"/>.
    /// Called from the Plugin Monitor panel via reflection when the user
    /// chooses "Open in Plugin Manager" from the context menu.
    /// </summary>
    internal void OnOpenPluginManagerWithSelection(string pluginId)
    {
        // Open or focus the tab first.
        OnOpenPluginManager(this, new RoutedEventArgs());

        // Retrieve the control from the content cache and delegate the selection.
        if (_contentCache.TryGetValue(PluginManagerContentId, out var content) &&
            content is WpfHexEditor.PluginHost.UI.PluginManagerControl pmControl)
        {
            pmControl.SelectPlugin(pluginId);
        }
    }

    // --- Plugin Monitor docking panel -----------------------------------

    private void OnOpenPluginMonitor(object sender, RoutedEventArgs e)
    {
        if (_pluginHost is null) return;

        var existing = _layout?.FindItemByContentId(PluginMonitorContentId);
        if (existing is not null)
        {
            if (existing.Owner is { } owner) owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

        var vm      = new WpfHexEditor.Panels.IDE.Panels.ViewModels.PluginMonitoringViewModel(_pluginHost, Dispatcher, _outputService);
        var control = new WpfHexEditor.Panels.IDE.Panels.PluginMonitoringPanel { DataContext = vm };

        var item = new DockItem
        {
            ContentId = PluginMonitorContentId,
            Title     = "Plugin Monitor",
            CanClose  = true
        };

        StoreContent(PluginMonitorContentId, control);

        // Dock in the existing bottom panel group (Error List / Output area) if available,
        // otherwise split a new bottom panel from the main document host.
        var bottomGroup = _layout.FindItemByContentId(ErrorPanelContentId)?.Owner
                       ?? _layout.FindItemByContentId("panel-output")?.Owner;
        if (bottomGroup is not null)
            _engine.Dock(item, bottomGroup, DockDirection.Center);
        else
            _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Bottom);

        DockHost.RebuildVisualTree();
    }

    // --- Marketplace panel -----------------------------------------------

    private void OnOpenMarketplace(object sender, RoutedEventArgs e)
    {
        var existing = _layout?.FindItemByContentId(MarketplaceContentId);
        if (existing is not null)
        {
            if (existing.Owner is { } owner) owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

        var svc   = new MarketplaceServiceImpl();
        var vm    = new MarketplacePanelViewModel(_pluginHost!, svc, msg => OutputLogger.PluginInfo(msg));
        var panel = new MarketplacePanel();
        panel.Initialize(vm);

        var item = new DockItem
        {
            ContentId = MarketplaceContentId,
            Title     = "Plugin Marketplace",
            CanClose  = true
        };

        StoreContent(MarketplaceContentId, panel);

        var bottomGroup = _layout.FindItemByContentId(ErrorPanelContentId)?.Owner
                       ?? _layout.FindItemByContentId("panel-output")?.Owner;
        if (bottomGroup is not null)
            _engine.Dock(item, bottomGroup, DockDirection.Center);
        else
            _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Bottom);

        DockHost.RebuildVisualTree();
    }

    // --- Plugin Dev Watch ------------------------------------------------

    private void OnOpenPluginDevWatch(object sender, RoutedEventArgs e)
    {
        if (_pluginHost is null)
        {
            OutputLogger.PluginError("[DevWatch] Plugin system not initialised.");
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Plugin Build Output Directory",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true) return;

        var dir = dialog.FolderName;
        if (!Directory.Exists(dir)) return;

        // Derive plugin ID from the folder name (user can rename via the folder they pick)
        var pluginId = new DirectoryInfo(dir).Name;

        _pluginDevLoader ??= new PluginDevLoader(_pluginHost, Dispatcher, msg => OutputLogger.PluginInfo(msg));
        _pluginDevLoader.Watch(pluginId, dir);

        OutputLogger.PluginInfo($"[DevWatch] Watching '{pluginId}' → {dir}");
    }

    // --- Content factories for panels that may be restored from layout --

    /// <summary>
    /// Creates the Plugin Manager tab during layout restoration.
    /// If the plugin system is not yet initialised, the DataContext is deferred.
    /// </summary>
    private UIElement CreatePluginManagerContent()
    {
        var control = new PluginManagerControl();

        if (_pluginHost is not null)
        {
            var vm = new PluginManagerViewModel(_pluginHost, Dispatcher);
            control.DataContext = vm;
        }
        else
        {
            _pendingPluginManagerControl = control;  // wire up after InitializePluginSystemAsync
        }

        return control;
    }

    /// <summary>
    /// Creates the Terminal panel during layout restoration.
    /// If the plugin system is not yet initialised, the DataContext is deferred.
    /// </summary>
    private UIElement CreateTerminalPanelContent()
    {
        var panel = new TerminalPanel();

        if (_ideHostContext is not null)
        {
            var vm = new TerminalPanelViewModel(_ideHostContext);
            _terminalService?.SetOutput(vm.GetActiveOutput());
            _terminalService?.SetSessionManager(vm.SessionManager);
            panel.DataContext = vm;
        }
        else
        {
            _pendingTerminalPanel = panel;   // wire up after InitializePluginSystemAsync
        }

        return panel;
    }

    /// <summary>
    /// Creates the Plugin Monitor panel during layout restoration.
    /// If the plugin system is not yet initialised, the DataContext is deferred.
    /// </summary>
    private UIElement CreatePluginMonitorPanelContent()
    {
        var panel = new WpfHexEditor.Panels.IDE.Panels.PluginMonitoringPanel();

        if (_pluginHost is not null)
        {
            var vm = new WpfHexEditor.Panels.IDE.Panels.ViewModels.PluginMonitoringViewModel(_pluginHost, Dispatcher, _outputService);
            panel.DataContext = vm;
        }
        else
        {
            _pendingPluginMonitorPanel = panel;   // wire up after InitializePluginSystemAsync
        }

        return panel;
    }

    // --- Terminal panel --------------------------------------------------

    private void OnOpenTerminal(object sender, RoutedEventArgs e)
    {
        // Reuse existing panel
        var existing = _layout?.FindItemByContentId(TerminalPanelContentId);
        if (existing is not null)
        {
            if (existing.Owner is { } owner) owner.ActiveItem = existing;
            DockHost.RebuildVisualTree();
            return;
        }

        if (_ideHostContext is null) { OutputLogger.Error("[Terminal] Host context unavailable."); return; }

        var vm      = new TerminalPanelViewModel(_ideHostContext);
        _terminalService?.SetOutput(vm.GetActiveOutput());
        _terminalService?.SetSessionManager(vm.SessionManager);
        var control = new TerminalPanel { DataContext = vm };

        var item = new DockItem
        {
            ContentId = TerminalPanelContentId,
            Title     = "Terminal",
            CanClose  = true
        };

        StoreContent(TerminalPanelContentId, control);

        // Dock in the existing bottom panel group (Error List / Output area) if available,
        // otherwise split a new bottom panel from the main document host.
        var bottomGroup = _layout.FindItemByContentId(ErrorPanelContentId)?.Owner
                       ?? _layout.FindItemByContentId("panel-output")?.Owner;
        if (bottomGroup is not null)
            _engine.Dock(item, bottomGroup, DockDirection.Center);
        else
            _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Bottom);

        DockHost.RebuildVisualTree();
    }

    // --- Plugin system shutdown ------------------------------------------

    private async Task ShutdownPluginSystemAsync()
    {
        if (_pluginHost is null) return;
        _pluginHost.PluginCrashed      -= OnPluginCrashed;
        _pluginHost.SlowPluginDetected -= OnSlowPluginDetected;
        _pluginHost.PluginLoaded       -= OnPluginLoadedOrUnloaded;
        _pluginHost.PluginUnloaded     -= OnPluginLoadedOrUnloaded;
        _statusBarBlinkTimer?.Stop();
        _statusBarBlinkTimer = null;
        if (_hexEditorService is not null)
            _hexEditorService.SelectionChanged -= OnHexEditorSelectionChanged;
        await _pluginHost.DisposeAsync().ConfigureAwait(false);
        _pluginHost = null;
        _ideEventBus?.Dispose();
        _ideEventBus = null;
    }

    // --- IDE EventBus publishers -----------------------------------------

    /// <summary>
    /// Called by OpenFileDirectly (MainWindow.xaml.cs) after a file tab is created.
    /// Publishes <see cref="FileOpenedEvent"/> on the IDE EventBus.
    /// </summary>
    internal void NotifyFileOpened(string filePath)
    {
        if (_ideEventBus is null) return;
        var info = new FileInfo(filePath);
        _ideEventBus.Publish(new FileOpenedEvent
        {
            Source        = "MainWindow",
            FilePath      = filePath,
            FileExtension = Path.GetExtension(filePath),
            FileSize      = info.Exists ? info.Length : 0L
        });
    }

    private void OnHexEditorSelectionChanged(object? sender, EventArgs e)
    {
        if (_ideEventBus is null || _hexEditorService is null) return;
        var start  = _hexEditorService.SelectionStart;
        var stop   = _hexEditorService.SelectionStop;
        var length = Math.Max(0L, stop - start + 1);
        _ideEventBus.Publish(new EditorSelectionChangedEvent
        {
            Source        = "HexEditorService",
            Offset        = start,
            Length        = length,
            SelectedBytes = []   // lazy: avoid reading large selections on every keystroke
        });
    }

    /// <summary>Registers the 10 well-known IDE event types in the EventBus registry.</summary>
    private static void RegisterWellKnownEvents(IDEEventBus bus)
    {
        var reg = bus.EventRegistry;
        reg.Register(typeof(FileOpenedEvent),               "File Opened",                "MainWindow");
        reg.Register(typeof(FileClosedEvent),               "File Closed",                "MainWindow");
        reg.Register(typeof(WorkspaceChangedEvent),         "Workspace Changed",          "MainWindow");
        reg.Register(typeof(DocumentSavedEvent),            "Document Saved",             "MainWindow");
        reg.Register(typeof(EditorSelectionChangedEvent),   "Editor Selection Changed",   "HexEditorService");
        reg.Register(typeof(PluginLoadedEvent),             "Plugin Loaded",              "WpfPluginHost");
        reg.Register(typeof(PluginUnloadedEvent),           "Plugin Unloaded",            "WpfPluginHost");
        reg.Register(typeof(TerminalCommandExecutedEvent),  "Terminal Command Executed",  "TerminalPanel");
        reg.Register(typeof(BuildStartedEvent),             "Build Started",              "BuildService");
        reg.Register(typeof(BuildSucceededEvent),           "Build Succeeded",            "BuildService");
    }

    // --- Minimal helpers ------------------------------------------------

    /// <summary>
    /// Minimal IDocument adapter wrapping a DockItem for FocusContextService.
    /// </summary>
    private sealed class SimpleDockItemDocument : IDocument
    {
        private readonly DockItem           _item;
        private readonly IDocumentEditor?   _editor;

        public SimpleDockItemDocument(DockItem item, IDocumentEditor? editor = null)
        {
            _item   = item;
            _editor = editor;
        }

        public string  ContentId    => _item.ContentId;
        public string  Title        => _item.Title ?? _item.ContentId;
        public string? FilePath     => _item.Metadata?.TryGetValue("FilePath", out var fp) == true ? fp : null;
        public string  DocumentType => "DockItem";
        public bool    IsDirty      => _editor?.IsDirty ?? false;
        public bool    CanUndo      => _editor?.CanUndo ?? false;
        public bool    CanRedo      => _editor?.CanRedo ?? false;
        public void    Undo()       => _editor?.Undo();
        public void    Redo()       => _editor?.Redo();
    }
}
