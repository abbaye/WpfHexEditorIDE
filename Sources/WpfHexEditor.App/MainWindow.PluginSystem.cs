
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

using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.App.Services;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.Core.Terminal;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.PluginHost;
using WpfHexEditor.PluginHost.Monitoring;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.PluginHost.UI;
using WpfHexEditor.Options;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.Terminal;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // --- Plugin system singletons ---------------------------------------
    private WpfPluginHost? _pluginHost;
    private IDEHostContext? _ideHostContext;
    private readonly FocusContextService _focusContextService = new();

    // Service adapters (lazily set in InitializePluginSystemAsync after layout is ready)
    private HexEditorServiceImpl? _hexEditorService;
    private OutputServiceImpl? _outputService;
    private ErrorPanelServiceImpl? _errorPanelService;
    private ThemeServiceImpl? _themeService;
    private TerminalServiceImpl? _terminalService;

    private const string PluginManagerContentId   = "plugin-manager";
    private const string TerminalPanelContentId   = "panel-terminal";
    private const string PluginMonitorContentId   = "plugin-monitor";

    private int    _pluginFaultCount = 0;
    private string? _infoBarPluginId;   // ID of the plugin currently shown in the InfoBar

    // Panels restored from a saved layout before the plugin system was ready.
    // Their DataContext is wired up at the end of InitializePluginSystemAsync.
    private TerminalPanel? _pendingTerminalPanel;
    private WpfHexEditor.Panels.IDE.Panels.PluginMonitoringPanel? _pendingPluginMonitorPanel;
    private WpfHexEditor.PluginHost.UI.PluginManagerControl? _pendingPluginManagerControl;

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

            var dockingAdapter = new DockingAdapter(_engine, _layout, DockHost, StoreContent);
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
                permissions: permissionService,
                terminal: _terminalService);

            // 3. Create orchestrator
            _ideHostContext = hostContext;
            _pluginHost = new WpfPluginHost(hostContext, uiRegistry, permissionService, Dispatcher);

            // 4. Subscribe to host events
            _pluginHost.PluginCrashed       += OnPluginCrashed;
            _pluginHost.SlowPluginDetected  += OnSlowPluginDetected;
            _pluginHost.PluginLoaded        += OnPluginLoadedOrUnloaded;
            _pluginHost.PluginUnloaded      += OnPluginLoadedOrUnloaded;

            // 5. Discover + load all plugins
            var binDir = AppDomain.CurrentDomain.BaseDirectory;
            var bundledPluginsDir = Path.Combine(binDir, "Plugins");
            await _pluginHost.LoadAllAsync(
                extraDirectories: Directory.Exists(bundledPluginsDir) ? [bundledPluginsDir] : null,
                ct: CancellationToken.None).ConfigureAwait(false);

            // Subscribe: CustomParser template applied via plugin → populate ParsedFieldsPanel in MainWindow
            eventBus.Subscribe<TemplateApplyRequestedEvent>(OnTemplateAppliedViaPlugin);

            // Register a dynamic Options page for every plugin that supports IPluginWithOptions.
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

            // Wire up panels that were restored from a saved layout before the plugin
            // system was ready (DataContext was null at construction time).
            await Dispatcher.InvokeAsync(() =>
            {
                if (_pendingTerminalPanel is not null)
                {
                    var vm = new TerminalPanelViewModel(hostContext);
                    _terminalService?.SetOutput(vm);
                    _pendingTerminalPanel.DataContext = vm;
                    _pendingTerminalPanel = null;
                }

                if (_pendingPluginMonitorPanel is not null && _pluginHost is not null)
                {
                    var vm = new WpfHexEditor.Panels.IDE.Panels.ViewModels.PluginMonitoringViewModel(_pluginHost, Dispatcher);
                    _pendingPluginMonitorPanel.DataContext = vm;
                    _pendingPluginMonitorPanel = null;
                }

                if (_pendingPluginManagerControl is not null && _pluginHost is not null)
                {
                    var vm = new PluginManagerViewModel(_pluginHost, Dispatcher);
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
            });
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"[PluginSystem] Failed to initialize: {ex.Message}");
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
        OutputLogger.Info(
            $"[PluginSystem] Warning: Plugin '{e.PluginName}' is slow " +
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
        if (PluginStatusIcon is null) return;
        _statusBarBlinkState = !_statusBarBlinkState;
        PluginStatusIcon.Opacity = _statusBarBlinkState ? 1.0 : 0.3;
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
            OutputLogger.Error(
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
        if (_pluginHost is null || PluginStatusText is null) return;

        var all    = _pluginHost.GetAllPlugins();
        var loaded = all.Count(e => e.State == SDK.Models.PluginState.Loaded);
        var total  = all.Count;

        PluginStatusText.Text = total == 0
            ? "No plugins"
            : loaded == total
                ? $"{loaded} plugin{(loaded == 1 ? "" : "s")}"
                : $"{loaded}/{total} plugins";

        // Aggregate metrics for tooltip
        double totalCpu = 0;
        long   totalMem = 0;
        string lastLoaded = "—";
        DateTime? lastTime = null;

        foreach (var entry in all.Where(e => e.State == SDK.Models.PluginState.Loaded))
        {
            var snap = entry.Diagnostics.GetLatest();
            if (snap is not null) { totalCpu += snap.CpuPercent; totalMem += snap.MemoryBytes; }
            if (entry.LoadedAt.HasValue && (lastTime is null || entry.LoadedAt > lastTime))
            {
                lastTime   = entry.LoadedAt;
                lastLoaded = $"{entry.Manifest.Name} ({entry.InitDuration.TotalMilliseconds:F0} ms)";
            }
        }

        // Build tooltip
        var tooltipLines = new System.Text.StringBuilder();
        tooltipLines.AppendLine($"Plugins: {loaded}/{total} loaded");
        if (loaded > 0)
        {
            tooltipLines.AppendLine($"CPU avg: {totalCpu / loaded:F1}%   RAM: {totalMem / 1024 / 1024} MB");
            tooltipLines.AppendLine($"Last loaded: {lastLoaded}");
        }
        if (_slowPluginName is not null)
            tooltipLines.AppendLine($"⚠ Slow plugin: {_slowPluginName}");
        tooltipLines.Append(_pluginFaultCount > 0
            ? $"⛔ {_pluginFaultCount} fault(s) — click to open Plugin Manager"
            : "Click to open Plugin Manager");

        PluginStatusItem.ToolTip = tooltipLines.ToString().TrimEnd();

        // Icon color: red = faults, amber = slow or partial, green = healthy
        if (_pluginFaultCount > 0)
        {
            PluginStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            // Start blink
            if (_statusBarBlinkTimer is not null && !_statusBarBlinkTimer.IsEnabled)
                _statusBarBlinkTimer.Start();
        }
        else if (_slowPluginName is not null || loaded < total)
        {
            PluginStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            _statusBarBlinkTimer?.Stop();
            PluginStatusIcon.Opacity = 1.0;
        }
        else
        {
            PluginStatusIcon.Foreground = (Brush?)TryFindResource("DockTabActiveTextBrush")
                                          ?? Brushes.White;
            _statusBarBlinkTimer?.Stop();
            PluginStatusIcon.Opacity = 1.0;
        }

        // Fault badge
        if (_pluginFaultCount > 0)
        {
            PluginWarningText.Text        = _pluginFaultCount.ToString();
            PluginWarningBadge.Visibility = Visibility.Visible;
        }
        else
        {
            PluginWarningBadge.Visibility = Visibility.Collapsed;
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
        if (_pluginHost is null || PluginStatusItem is null) return;

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
            PlacementTarget = PluginStatusItem,
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
    {
        // Open Options dialog at the Plugins page
        OnSettings(sender, e);
    }

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

        var vm      = new WpfHexEditor.Panels.IDE.Panels.ViewModels.PluginMonitoringViewModel(_pluginHost, Dispatcher);
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
            _terminalService?.SetOutput(vm);
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
            var vm = new WpfHexEditor.Panels.IDE.Panels.ViewModels.PluginMonitoringViewModel(_pluginHost, Dispatcher);
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
        _terminalService?.SetOutput(vm);
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

    // --- EventBus handlers -----------------------------------------------

    /// <summary>
    /// Called when the CustomParserTemplate plugin applies a template.
    /// Routes parsed blocks to the MainWindow-managed ParsedFieldsPanel.
    /// Must be called on the UI thread (PluginEventBus dispatches synchronously).
    /// </summary>
    private void OnTemplateAppliedViaPlugin(TemplateApplyRequestedEvent evt)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (ActiveHexEditor is not { IsFileOrStreamLoaded: true } hex)
                return;

            ShowOrCreatePanel("Parsed Fields", ParsedFieldsPanelContentId, DockDirection.Right);
            if (_parsedFieldsPanel is null) return;

            var allBytes = hex.GetAllBytes();

            _parsedFieldsPanel.Clear();
            _parsedFieldsPanel.TotalFileSize = allBytes?.LongLength ?? 0;
            _parsedFieldsPanel.FormatInfo = new WpfHexEditor.Core.Interfaces.FormatInfo
            {
                IsDetected  = true,
                Name        = evt.TemplateName,
                Description = string.Empty,
                Category    = "Custom Template"
            };

            foreach (var block in evt.Blocks)
            {
                var offset = (int)block.Offset;
                if (allBytes is null || offset < 0 || offset + block.Length > allBytes.Length)
                    continue;

                var data = new byte[block.Length];
                Array.Copy(allBytes, offset, data, 0, block.Length);
                var (rawVal, fmtVal) = InterpretTemplateBytes(data, block.TypeHint);

                _parsedFieldsPanel.ParsedFields.Add(
                    new WpfHexEditor.Core.ViewModels.ParsedFieldViewModel
                    {
                        Name           = block.Name,
                        Offset         = offset,
                        Length         = block.Length,
                        RawValue       = rawVal,
                        FormattedValue = fmtVal,
                        ValueType      = block.TypeHint,
                        Description    = block.DisplayValue,
                        IsValid        = true
                    });
            }

            _parsedFieldsPanel.RefreshView();
            if (RefreshText is not null)
                RefreshText.Text = $"Template '{evt.TemplateName}' applied — {evt.Blocks.Count} fields.";
        });
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
        await _pluginHost.DisposeAsync().ConfigureAwait(false);
        _pluginHost = null;
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
