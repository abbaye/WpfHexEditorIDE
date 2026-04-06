// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: XamlDesignerPlugin.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-18 — Added BindingInspectorPanel (8th) + LiveVisualTreePanel (9th).
//                        Added Outline↔Canvas bidirectional sync (C3).
// Description:
//     Official plugin entry point for the XAML Designer.
//     Implements IWpfHexEditorPlugin + IPluginWithOptions.
//     Registers:
//       - XamlOutlinePanel          (Left, AutoHide, width=220)
//       - PropertyInspectorPanel    (Right, width=260)
//       - DesignHistoryPanel        (Right, auto-hide, width=240)
//       - BindingInspectorPanel     (Right, auto-hide, width=280)
//       - LiveVisualTreePanel       (Left, auto-hide, width=240)
//       - Status bar item           (Left, order=15)
//       - View menu items for all panels
//     Subscribes to FocusContext.FocusChanged to wire the active
//     XamlDesignerSplitHost's SelectedElementChanged event to all
//     side panels and the status bar item on each document switch.
//     On each host switch: DesignHistoryPanel.ViewModel.Manager is updated and
//     JumpRequested is wired to host.JumpToHistoryEntry.
//     C3: XamlOutlinePanel ↔ DesignCanvas bidirectional selection sync.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to IFocusContextService.FocusChanged.
//     All UI is constructed and registered on the calling thread (UI thread).
//     UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost on unload.
// ==========================================================

using System.Windows;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.XamlDesigner.Controls;
using WpfHexEditor.Plugins.XamlDesigner.Panels;
using WpfHexEditor.Plugins.XamlDesigner.ViewModels;
using WpfHexEditor.Plugins.XamlDesigner.Options;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.XamlDesigner;

/// <summary>
/// Entry point for the official XAML Designer plugin.
/// Registers the Outline and Property Inspector side panels, wires them to
/// the active <see cref="XamlDesignerSplitHost"/> via FocusContext events.
/// </summary>
public sealed class XamlDesignerPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string  Id      => "WpfHexEditor.Plugins.XamlDesigner";
    public string  Name    => "XAML Designer";
    public Version Version => new(0, 1, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = false,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = false,
        AccessSettings   = true
    };

    // ── UI ID constants ───────────────────────────────────────────────────────

    private const string OutlinePanelUiId       = "WpfHexEditor.Plugins.XamlDesigner.Panel.Outline";
    private const string PropertiesPanelUiId    = "WpfHexEditor.Plugins.XamlDesigner.Panel.Properties";
    private const string ToolboxPanelUiId       = "WpfHexEditor.Plugins.XamlDesigner.Panel.Toolbox";
    private const string ResourcePanelUiId      = "WpfHexEditor.Plugins.XamlDesigner.Panel.Resource";
    private const string DesignDataPanelUiId    = "WpfHexEditor.Plugins.XamlDesigner.Panel.DesignData";
    private const string AnimationPanelUiId     = "WpfHexEditor.Plugins.XamlDesigner.Panel.Animation";
    private const string HistoryPanelUiId       = "WpfHexEditor.Plugins.XamlDesigner.Panel.History";
    private const string BindingPanelUiId       = "WpfHexEditor.Plugins.XamlDesigner.Panel.BindingInspector";
    private const string LiveTreePanelUiId      = "WpfHexEditor.Plugins.XamlDesigner.Panel.LiveVisualTree";
    private const string StatusBarElementId     = "WpfHexEditor.Plugins.XamlDesigner.StatusBar.Element";

    // ── State ─────────────────────────────────────────────────────────────────

    private XamlOutlinePanel?                 _outlinePanel;
    private PropertyInspectorPanel?           _propertiesPanel;
    private XamlToolboxPanel?                 _toolboxPanel;
    private ResourceBrowserPanel?             _resourcePanel;
    private DesignDataPanel?                  _designDataPanel;
    private AnimationTimelinePanel?           _animationPanel;
    private AnimationTimelinePanelViewModel?  _animationVm;
    private DesignHistoryPanel?               _historyPanel;
    private BindingInspectorPanel?            _bindingPanel;
    private LiveVisualTreePanel?              _liveTreePanel;
    private bool                              _isPickModeActive;
    private IIDEHostContext?           _context;
    private XamlDesignerOptionsPage?   _optionsPage;
    private StatusBarItemDescriptor?   _sbElement;

    // Track the currently wired host to properly unwire on document switch.
    private XamlDesignerSplitHost?     _wiredHost;

    // ── Debounce state for resource rescan ────────────────────────────────────
    private System.Windows.Threading.DispatcherTimer? _resourceRescanTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // Build panels.
        _outlinePanel    = new XamlOutlinePanel();
        _propertiesPanel = new PropertyInspectorPanel();
        _toolboxPanel    = new XamlToolboxPanel();
        _resourcePanel   = new ResourceBrowserPanel();
        _designDataPanel = new DesignDataPanel();
        _animationVm     = new AnimationTimelinePanelViewModel();
        _animationPanel  = new AnimationTimelinePanel();
        _animationPanel.SetViewModel(_animationVm);
        _historyPanel    = new DesignHistoryPanel();
        _bindingPanel    = new BindingInspectorPanel();
        _liveTreePanel   = new LiveVisualTreePanel();

        // Register the XAML Outline panel (left side, auto-hide).
        context.UIRegistry.RegisterPanel(
            OutlinePanelUiId,
            _outlinePanel,
            Id,
            new PanelDescriptor
            {
                Title           = "XAML Outline",
                DefaultDockSide = "Left",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 220
            });

        // Register the Property Inspector panel (right side).
        context.UIRegistry.RegisterPanel(
            PropertiesPanelUiId,
            _propertiesPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "XAML Properties",
                DefaultDockSide = "Right",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredWidth  = 260
            });

        // Register the XAML Toolbox panel (left side, auto-hide).
        context.UIRegistry.RegisterPanel(
            ToolboxPanelUiId,
            _toolboxPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "XAML Toolbox",
                DefaultDockSide = "Left",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 220
            });

        // Register the Resource Browser panel (right side, auto-hide).
        context.UIRegistry.RegisterPanel(
            ResourcePanelUiId,
            _resourcePanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Resource Browser",
                DefaultDockSide = "Right",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 260
            });

        // Register the Design Data panel (bottom).
        context.UIRegistry.RegisterPanel(
            DesignDataPanelUiId,
            _designDataPanel,
            Id,
            new PanelDescriptor
            {
                Title            = "Design Data",
                DefaultDockSide  = "Bottom",
                DefaultAutoHide  = false,
                CanClose         = true,
                PreferredHeight  = 180
            });

        // Register the Animation Timeline panel (bottom).
        context.UIRegistry.RegisterPanel(
            AnimationPanelUiId,
            _animationPanel,
            Id,
            new PanelDescriptor
            {
                Title            = "Animation Timeline",
                DefaultDockSide  = "Bottom",
                DefaultAutoHide  = false,
                CanClose         = true,
                PreferredHeight  = 180
            });

        // Register the Design History panel (right side, auto-hide).
        context.UIRegistry.RegisterPanel(
            HistoryPanelUiId,
            _historyPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Design History",
                DefaultDockSide = "Right",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 240
            });

        // Register the Binding Inspector panel (8th panel — right side, auto-hide).
        context.UIRegistry.RegisterPanel(
            BindingPanelUiId,
            _bindingPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Binding Inspector",
                DefaultDockSide = "Right",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 280
            });

        // Register the Live Visual Tree panel (9th panel — left side, auto-hide).
        context.UIRegistry.RegisterPanel(
            LiveTreePanelUiId,
            _liveTreePanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Live Visual Tree",
                DefaultDockSide = "Left",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 240
            });

        // Register status bar item (left, order=15).
        _sbElement = new StatusBarItemDescriptor
        {
            Text      = "",
            Alignment = StatusBarAlignment.Left,
            Order     = 15,
            ToolTip   = "Selected element in the active XAML designer"
        };
        context.UIRegistry.RegisterStatusBarItem(StatusBarElementId, Id, _sbElement);

        // Register menu items.
        RegisterMenuItems(context);

        // Subscribe to document focus changes so panels sync to the active designer.
        context.FocusContext.FocusChanged += OnFocusChanged;

        // Seed panels immediately for any document that was already active when the plugin
        // loaded (e.g. XAML file open at startup — FocusChanged will never fire for it).
        SeedFromCurrentDocument(context);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Seeds all side panels from the document that is currently active at plugin load time.
    /// Without this, a XAML file already open before the plugin loaded would never trigger
    /// OnFocusChanged, leaving the Live Visual Tree and other panels permanently empty.
    /// </summary>
    private void SeedFromCurrentDocument(IIDEHostContext context)
    {
        // Defer to ApplicationIdle so AssociatedEditor is fully set before we resolve the host.
        // Calling this synchronously in InitializeAsync hits a timing window where
        // model.AssociatedEditor is still null, causing ResolveActiveHost to return null
        // and the null-null guard to silently skip wiring.
        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            () =>
            {
                if (context.FocusContext.ActiveDocument is not { } activeDoc) return;
                OnFocusChanged(this, new FocusChangedEventArgs { ActiveDocument = activeDoc });
            });
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
            _context.FocusContext.FocusChanged -= OnFocusChanged;

        UnwireCurrentHost();

        _outlinePanel    = null;
        _propertiesPanel = null;
        _toolboxPanel    = null;
        _resourcePanel   = null;
        _designDataPanel = null;
        _animationPanel  = null;
        _historyPanel    = null;
        _bindingPanel    = null;
        _liveTreePanel   = null;
        _context         = null;
        _optionsPage     = null;
        _sbElement       = null;
        _wiredHost       = null;

        return Task.CompletedTask;
    }

    // ── Focus tracking ────────────────────────────────────────────────────────

    private void OnFocusChanged(object? sender, FocusChangedEventArgs e)
    {
        // Resolve the active XamlDesignerSplitHost from the newly active document.
        var host = ResolveActiveHost(e.ActiveDocument);

        if (host is not null && ReferenceEquals(host, _wiredHost))
            return; // Same designer — no rewiring needed.

        UnwireCurrentHost();
        _wiredHost = host;

        if (host is null)
        {
            // No XAML designer active — clear the side panels.
            _outlinePanel?.ViewModel?.RebuildTree(null);
            if (_propertiesPanel?.ViewModel is not null)
                _propertiesPanel.ViewModel.SelectedObject = null;
            _propertiesPanel?.SetElementName(null);
            if (_historyPanel is not null)
                _historyPanel.ViewModel.Manager = null;
            _bindingPanel?.SetTarget(null);
            _liveTreePanel?.ViewModel.Refresh(null);
            UpdateStatusBar(null);
            return;
        }

        // Wire the new host: outline + property inspector + status bar.
        host.SelectedElementChanged        += OnSelectedElementChanged;
        host.Document.XamlChanged         += OnXamlChanged;
        host.FocusPropertiesPanelRequested += OnFocusPropertiesRequested;

        // Wire the history panel to the new host's undo manager.
        if (_historyPanel is not null)
        {
            _historyPanel.ViewModel.Manager = host.UndoManager;
            _historyPanel.JumpRequested     += OnHistoryPanelJumpRequested;
        }

        // Live Visual Tree — subscribe to the post-render event so Refresh() always
        // receives a stable, fully-laid-out DesignRoot (never a timing-race null).
        if (host.Canvas is { } canvas)
            canvas.DesignRendered += OnCanvasDesignRendered;

        // Live Visual Tree — reverse sync: tree node click → canvas highlight.
        // Also wire RefreshRequested (panel becomes visible) and NodeHovered (hover overlay).
        if (_liveTreePanel is not null)
        {
            _liveTreePanel.NodeSelected             -= OnLiveTreeNodeSelected;
            _liveTreePanel.NodeSelected             += OnLiveTreeNodeSelected;
            _liveTreePanel.RefreshRequested         -= OnLiveTreeRefreshRequested;
            _liveTreePanel.RefreshRequested         += OnLiveTreeRefreshRequested;
            _liveTreePanel.NodeHovered              -= OnLiveTreeNodeHovered;
            _liveTreePanel.NodeHovered              += OnLiveTreeNodeHovered;
            _liveTreePanel.NavigateToXamlRequested  -= OnLiveTreeNavigateToXaml;
            _liveTreePanel.NavigateToXamlRequested  += OnLiveTreeNavigateToXaml;
            _liveTreePanel.PickModeChanged          -= OnLiveTreePickModeChanged;
            _liveTreePanel.PickModeChanged          += OnLiveTreePickModeChanged;
        }

        // Seed new panels with the current canvas state.
        _bindingPanel?.SetTarget(host.Canvas?.SelectedElement as System.Windows.DependencyObject);

        // Best-effort seed for Live Tree (DesignRoot may already be valid on tab-switch).
        // OnCanvasDesignRendered will follow with the authoritative stable value after the next render.
        _liveTreePanel?.ViewModel.Refresh(host.Canvas?.DesignRoot);

        // Seed Design Data and Animation panels from current XAML source.
        _designDataPanel?.SetXamlSource(host.Document.RawXaml);
        if (_animationVm is not null)
            _animationVm.XamlSource = host.Document.RawXaml;

        // C3 — Outline → Canvas: sync XAML outline selection to the canvas.
        if (_outlinePanel is not null)
        {
            _outlinePanel.SyncRequested       -= OnOutlineSyncRequested;
            _outlinePanel.SyncRequested       += OnOutlineSyncRequested;
            _outlinePanel.DeleteRequested     -= OnOutlineDeleteRequested;
            _outlinePanel.DeleteRequested     += OnOutlineDeleteRequested;
            _outlinePanel.MoveRequested       -= OnOutlineMoveRequested;
            _outlinePanel.MoveRequested       += OnOutlineMoveRequested;
            _outlinePanel.WrapRequested       -= OnOutlineWrapRequested;
            _outlinePanel.WrapRequested       += OnOutlineWrapRequested;
        }

        // Wire new panel events.
        if (_bindingPanel is not null)
        {
            _bindingPanel.NavigateToSourceRequested -= OnBindingNavigateToSourceRequested;
            _bindingPanel.NavigateToSourceRequested += OnBindingNavigateToSourceRequested;
        }
        if (_propertiesPanel is not null)
        {
            _propertiesPanel.BindingBadgeClicked -= OnPropertyBindingBadgeClicked;
            _propertiesPanel.BindingBadgeClicked += OnPropertyBindingBadgeClicked;
        }
        if (_resourcePanel is not null)
        {
            _resourcePanel.GoToDefinitionRequested -= OnResourceGoToDefinitionRequested;
            _resourcePanel.GoToDefinitionRequested += OnResourceGoToDefinitionRequested;
        }
        if (_toolboxPanel is not null)
        {
            _toolboxPanel.InsertRequested -= OnToolboxInsertRequested;
            _toolboxPanel.InsertRequested += OnToolboxInsertRequested;
            _toolboxPanel.DropCompleted   -= OnToolboxDropCompleted;
            _toolboxPanel.DropCompleted   += OnToolboxDropCompleted;
        }

        // Phase EL: inject ErrorPanelService so OnRenderError can push diagnostics to the ErrorList.
        host.ErrorPanelService = _context?.ErrorPanel;

        _outlinePanel?.ViewModel?.RebuildTree(host.Document.ParsedRoot);
        UpdateSidePanels(host);
    }

    private void UnwireCurrentHost()
    {
        if (_wiredHost is null) return;
        _wiredHost.SelectedElementChanged        -= OnSelectedElementChanged;
        _wiredHost.Document.XamlChanged         -= OnXamlChanged;
        _wiredHost.FocusPropertiesPanelRequested -= OnFocusPropertiesRequested;

        // Detach history panel from the outgoing host.
        if (_historyPanel is not null)
        {
            _historyPanel.JumpRequested     -= OnHistoryPanelJumpRequested;
            _historyPanel.ViewModel.Manager  = null;
        }

        // Detach outline sync from the outgoing host.
        if (_outlinePanel is not null)
        {
            _outlinePanel.SyncRequested       -= OnOutlineSyncRequested;
            _outlinePanel.DeleteRequested     -= OnOutlineDeleteRequested;
            _outlinePanel.MoveRequested       -= OnOutlineMoveRequested;
            _outlinePanel.WrapRequested       -= OnOutlineWrapRequested;
        }

        // Detach Live Visual Tree post-render event, reverse-sync, refresh, hover, and navigate events.
        if (_wiredHost.Canvas is { } canvas)
            canvas.DesignRendered -= OnCanvasDesignRendered;
        if (_liveTreePanel is not null)
        {
            _liveTreePanel.NodeSelected            -= OnLiveTreeNodeSelected;
            _liveTreePanel.RefreshRequested        -= OnLiveTreeRefreshRequested;
            _liveTreePanel.NodeHovered             -= OnLiveTreeNodeHovered;
            _liveTreePanel.NavigateToXamlRequested -= OnLiveTreeNavigateToXaml;
            _liveTreePanel.PickModeChanged         -= OnLiveTreePickModeChanged;
        }

        // Detach new panel cross-wiring.
        if (_bindingPanel is not null)
            _bindingPanel.NavigateToSourceRequested -= OnBindingNavigateToSourceRequested;
        if (_propertiesPanel is not null)
            _propertiesPanel.BindingBadgeClicked -= OnPropertyBindingBadgeClicked;
        if (_resourcePanel is not null)
            _resourcePanel.GoToDefinitionRequested -= OnResourceGoToDefinitionRequested;
        if (_toolboxPanel is not null)
        {
            _toolboxPanel.InsertRequested -= OnToolboxInsertRequested;
            _toolboxPanel.DropCompleted   -= OnToolboxDropCompleted;
        }

        // Phase EL: detach ErrorPanelService so the outgoing host stops posting diagnostics.
        _wiredHost.ErrorPanelService = null;

        _wiredHost = null;
    }

    private void OnHistoryPanelJumpRequested(object? sender, JumpToEntryEventArgs e)
        => _wiredHost?.JumpToHistoryEntry(e.UndoCount, e.RedoCount);

    // Fired by DesignCanvas.DesignRendered — DesignRoot is now stable and the visual tree is walkable.
    // Respects the AutoRefresh toggle: when off the tree is frozen until the user clicks Refresh.
    private void OnCanvasDesignRendered(object? sender, System.Windows.UIElement? root)
    {
        if (_liveTreePanel?.ViewModel.AutoRefresh == true)
            _liveTreePanel.ViewModel.Refresh(root);
    }

    // Fired when the user clicks a node in the Live Visual Tree — highlights that element on the canvas
    // AND navigates the code editor to the corresponding XAML line.
    private void OnLiveTreeNodeSelected(object? sender, System.Windows.UIElement? element)
    {
        if (_wiredHost is null) return;

        // Select on canvas (this also triggers OnDesignSelectionChanged → NavigateCodeEditorToUid).
        _wiredHost.Canvas?.SelectElement(element);

        // Direct code navigation: resolve UID from the canvas selection so navigation
        // works even when the design pane is not visible (CodeOnly mode).
        int uid = _wiredHost.Canvas?.SelectedElementUid ?? -1;
        if (uid >= 0)
            _wiredHost.NavigateCodeEditorToUid(uid);
    }

    // Fired when the Live Visual Tree panel becomes visible — reseed from the current DesignRoot
    // so the panel is never empty when revealed without a prior document-switch event.
    private void OnLiveTreeRefreshRequested(object? sender, EventArgs e)
    {
        var root = _wiredHost?.Canvas?.DesignRoot;
        if (root is null) return;   // No active canvas — preserve the existing _root in the ViewModel
        _liveTreePanel?.ViewModel.Refresh(root);
    }

    // Fired when the user hovers a tree node — draws a non-selecting overlay on the canvas.
    private void OnLiveTreeNodeHovered(object? sender, System.Windows.UIElement? element)
        => _wiredHost?.Canvas?.HighlightHoverElement(element);

    // Fired when "Navigate to XAML" context menu is chosen — navigates the code editor
    // to the element's x:Name line and syncs the XAML outline panel.
    private void OnLiveTreeNavigateToXaml(object? sender, string? elementName)
    {
        if (string.IsNullOrEmpty(elementName)) return;

        // Navigate the code editor to the element's source line.
        _wiredHost?.NavigateCodeEditorToXName(elementName);

        // Also sync the outline panel for visual feedback.
        _outlinePanel?.ViewModel?.SelectNodeByPath(elementName);
    }

    // D: Pick Element mode toggled from the Live Visual Tree toolbar.
    // When active, every canvas SelectedElementChanged fires → SelectNodeByElement → TrackSelection
    // (forced to true) → the tree auto-scrolls to the clicked element.
    private void OnLiveTreePickModeChanged(object? sender, bool isActive)
    {
        _isPickModeActive = isActive;

        // Force TrackSelection on while pick mode is active so every canvas click navigates the tree.
        if (isActive && _liveTreePanel is not null)
            _liveTreePanel.ViewModel.TrackSelection = true;
    }

    /// <summary>
    /// C3 — Outline → Canvas sync: when the outline panel's "Sync to code" button is
    /// clicked, find the matching UIElement on the canvas by x:Name / element type path
    /// and select it so the canvas adorner follows the outline selection.
    /// </summary>
    private void OnOutlineSyncRequested(object? sender, XamlOutlineNode? node)
    {
        if (_wiredHost is null || node is null) return;

        // Use the outline node's ElementPath (slash-delimited tag path) to find a
        // matching element by x:Name first, then fall back to type-name search.
        var canvas = _wiredHost.Canvas;
        if (canvas is null) return;

        // Try locating by x:Name when the node exposes one.
        if (!string.IsNullOrEmpty(node.XName_))
        {
            var named = canvas.DesignRoot is System.Windows.FrameworkElement root
                ? FindByName(root, node.XName_)
                : null;
            if (named is not null)
            {
                canvas.SelectElement(named);
                return;
            }
        }

        // Fallback: select by walking the outline path on the visual tree.
        // When no x:Name is available this is a best-effort match.
        var target = FindByPath(canvas.DesignRoot, node.ElementPath);
        if (target is not null)
            canvas.SelectElement(target);
    }

    private void OnFocusPropertiesRequested(object? sender, EventArgs e)
    {
        if (_context is null) return;
        _context.UIRegistry.FocusPanel(PropertiesPanelUiId);
    }

    private void OnXamlChanged(object? sender, EventArgs e)
    {
        if (_wiredHost is null) return;
        _outlinePanel?.ViewModel?.RebuildTree(_wiredHost.Document.ParsedRoot);

        // Live Visual Tree is now refreshed via DesignCanvas.DesignRendered (fires after
        // RenderXaml() completes at DispatcherPriority.Loaded), so no Refresh() call here.

        // Feed XAML source to panels that parse it independently.
        _designDataPanel?.SetXamlSource(_wiredHost.Document.RawXaml);
        if (_animationVm is not null)
            _animationVm.XamlSource = _wiredHost.Document.RawXaml;

        // Trigger debounced resource rescan when XAML changes.
        if (XamlDesignerOptions.Instance.ResourceBrowserAutoRescan)
            ScheduleResourceRescan(_wiredHost.Document.RawXaml);
    }

    /// <summary>
    /// Schedules a 500ms debounced resource rescan after a XAML document change.
    /// Resets the timer on every call so rapid typing doesn't trigger multiple scans.
    /// </summary>
    private void ScheduleResourceRescan(string rawXaml)
    {
        if (_resourceRescanTimer is not null)
        {
            _resourceRescanTimer.Stop();
            _resourceRescanTimer = null;
        }

        _resourceRescanTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(500)
        };

        _resourceRescanTimer.Tick += (_, _) =>
        {
            _resourceRescanTimer?.Stop();
            _resourceRescanTimer = null;
            _resourcePanel?.ViewModel.ScheduleRescan();
        };

        _resourceRescanTimer.Start();
    }

    private void OnSelectedElementChanged(object? sender, EventArgs e)
    {
        if (_wiredHost is null) return;
        UpdateSidePanels(_wiredHost);
    }

    private void UpdateSidePanels(XamlDesignerSplitHost host)
    {
        var selectedUi = host.Canvas?.SelectedElement;
        // If nothing is selected yet (e.g., file just opened), fall back to the design root
        // so Properties / Binding Inspector populate immediately without requiring a click.
        if (selectedUi is null)
            selectedUi = host.Canvas?.DesignRoot;
        var dep = selectedUi as System.Windows.DependencyObject;

        if (_propertiesPanel?.ViewModel is not null)
            _propertiesPanel.ViewModel.SelectedObject = dep;

        // Update Binding Inspector with the newly selected element.
        _bindingPanel?.SetTarget(dep);

        // Live Visual Tree — highlight the node that corresponds to the canvas-selected element.
        _liveTreePanel?.ViewModel.SelectNodeByElement(selectedUi);

        // C3 — Canvas → Outline sync: move the outline selection to match the canvas.
        if (_outlinePanel is not null && selectedUi is not null)
        {
            var path = dep is System.Windows.FrameworkElement fe && !string.IsNullOrEmpty(fe.Name)
                ? fe.Name
                : null;
            if (!string.IsNullOrEmpty(path))
                _outlinePanel.ViewModel.SelectNodeByPath(path);
        }

        // Propagate selected element name to the Animation Timeline (filters tracks by TargetName).
        var frameworkName = dep is System.Windows.FrameworkElement fwEl ? fwEl.Name : string.Empty;
        _animationVm?.SetContextElement(string.IsNullOrEmpty(frameworkName) ? null : frameworkName);

        var elementName = dep?.GetType().Name ?? string.Empty;
        _propertiesPanel?.SetElementName(elementName);
        UpdateStatusBar(elementName);
    }

    private void UpdateStatusBar(string? elementName)
    {
        if (_sbElement is null) return;
        _sbElement.Text = string.IsNullOrEmpty(elementName)
            ? string.Empty
            : $"⬚ {elementName}";
    }

    // ── Helpers: visual-tree element search ───────────────────────────────────

    /// <summary>
    /// Searches <paramref name="root"/>'s visual tree for a FrameworkElement
    /// whose <see cref="System.Windows.FrameworkElement.Name"/> matches <paramref name="name"/>.
    /// Returns null when not found or the tree is empty.
    /// </summary>
    private static System.Windows.UIElement? FindByName(System.Windows.FrameworkElement root, string name)
    {
        if (root.Name == name) return root;

        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.FrameworkElement fe)
            {
                var found = FindByName(fe, name);
                if (found is not null) return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Best-effort element lookup by outline element path (slash-delimited type names).
    /// Walks the visual tree matching each segment by type name.
    /// Returns null when no match is found.
    /// </summary>
    private static System.Windows.UIElement? FindByPath(System.Windows.UIElement? root, string elementPath)
    {
        if (root is null || string.IsNullOrEmpty(elementPath)) return null;

        var segments = elementPath.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        // The first segment must match the root's type name.
        var rootTypeName = root.GetType().Name;
        if (!elementPath.StartsWith(rootTypeName, System.StringComparison.OrdinalIgnoreCase))
            return null;

        // Single-segment path — the root is the target.
        if (segments.Length == 1) return root;

        // Walk child segments recursively.
        return FindChildBySegments(root, segments, 1);
    }

    private static System.Windows.UIElement? FindChildBySegments(
        System.Windows.DependencyObject parent,
        string[] segments,
        int segmentIndex)
    {
        if (segmentIndex >= segments.Length) return parent as System.Windows.UIElement;

        var segment  = segments[segmentIndex];
        int count    = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child.GetType().Name.Equals(segment, System.StringComparison.OrdinalIgnoreCase))
            {
                var result = FindChildBySegments(child, segments, segmentIndex + 1);
                if (result is not null) return result;
            }
        }

        return null;
    }

    // ── Helper: resolve XamlDesignerSplitHost from IDocument ─────────────────

    private XamlDesignerSplitHost? ResolveActiveHost(IDocument? doc)
    {
        if (doc is null || _context is null) return null;

        var model = _context.DocumentHost.Documents.OpenDocuments
            .FirstOrDefault(d => d.ContentId == doc.ContentId);

        return model?.AssociatedEditor as XamlDesignerSplitHost;
    }

    // ── New panel event handlers ───────────────────────────────────────────────

    /// <summary>
    /// Outline panel: Delete element — routes to XamlReorderService, applies via undo entry.
    /// </summary>
    private void OnOutlineDeleteRequested(object? sender, WpfHexEditor.Plugins.XamlDesigner.Panels.DeleteRequestedEventArgs e)
    {
        if (_wiredHost?.Document is null || e.Node is null) return;

        var service = new WpfHexEditor.Editor.XamlDesigner.Services.XamlReorderService();
        var newXaml = service.DeleteElement(_wiredHost.Document.RawXaml, e.Node.ElementPath);
        if (newXaml is not null)
            _wiredHost.Document.SetXaml(newXaml);
    }

    /// <summary>
    /// Outline panel: Move element up or down among siblings.
    /// </summary>
    private void OnOutlineMoveRequested(object? sender, WpfHexEditor.Plugins.XamlDesigner.Panels.MoveRequestedEventArgs e)
    {
        if (_wiredHost?.Document is null || e.Node is null) return;

        var service = new WpfHexEditor.Editor.XamlDesigner.Services.XamlReorderService();
        // Direction: -1 = move up, +1 = move down (see MoveRequestedEventArgs).
        var newXaml = e.Direction < 0
            ? service.MoveUp(_wiredHost.Document.RawXaml, e.Node.ElementPath)
            : service.MoveDown(_wiredHost.Document.RawXaml, e.Node.ElementPath);

        if (newXaml is not null)
            _wiredHost.Document.SetXaml(newXaml);
    }

    /// <summary>
    /// Outline panel: Wrap element in a container (Grid, StackPanel, Border).
    /// </summary>
    private void OnOutlineWrapRequested(object? sender, WpfHexEditor.Plugins.XamlDesigner.Panels.WrapRequestedEventArgs e)
    {
        if (_wiredHost?.Document is null || e.Node is null) return;

        var service = new WpfHexEditor.Editor.XamlDesigner.Services.XamlReorderService();
        var newXaml = service.WrapIn(_wiredHost.Document.RawXaml, e.Node.ElementPath, e.ContainerTag);
        if (newXaml is not null)
            _wiredHost.Document.SetXaml(newXaml);
    }

    /// <summary>
    /// Binding Inspector: navigate to the source type (focuses Properties panel).
    /// </summary>
    private void OnBindingNavigateToSourceRequested(object? sender, string? sourceName)
    {
        if (_context is null || string.IsNullOrEmpty(sourceName)) return;
        _context.UIRegistry.FocusPanel(PropertiesPanelUiId);
    }

    /// <summary>
    /// Property Inspector: binding badge clicked → bring Binding Inspector to front.
    /// </summary>
    private void OnPropertyBindingBadgeClicked(object? sender, WpfHexEditor.Editor.XamlDesigner.Models.PropertyInspectorEntry entry)
    {
        if (_context is null) return;
        _context.UIRegistry.ShowPanel(BindingPanelUiId);
        _context.UIRegistry.FocusPanel(BindingPanelUiId);
    }

    /// <summary>
    /// Resource Browser: navigate code editor to the resource definition line.
    /// </summary>
    private void OnResourceGoToDefinitionRequested(object? sender, (string Key, int Line) args)
    {
        if (_wiredHost is null || args.Line <= 0) return;
        // Route to the code editor via the host's document.
        _wiredHost.NavigateToLine(args.Line);
    }

    /// <summary>
    /// Toolbox: double-click/Enter insert at current canvas selection.
    /// </summary>
    private void OnToolboxInsertRequested(object? sender, WpfHexEditor.Editor.XamlDesigner.Models.ToolboxItem item)
    {
        if (_wiredHost?.Canvas is null) return;
        _wiredHost.InsertElementAtSelection(item);
    }

    /// <summary>
    /// Toolbox: drag completed → track recent usage in options.
    /// </summary>
    private void OnToolboxDropCompleted(object? sender, WpfHexEditor.Editor.XamlDesigner.Models.ToolboxItem item)
    {
        // Recent usage is tracked by the ViewModel; persist when options are saved.
    }

    // ── Menu items ────────────────────────────────────────────────────────────

    private void RegisterMenuItems(IIDEHostContext context)
    {
        // View > XAML Outline
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.ToggleOutline",
            Id,
            new MenuItemDescriptor
            {
                Header     = "XAML _Outline",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE8A5",
                ToolTip    = "Show or hide the XAML Outline panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(OutlinePanelUiId))
            });

        // View > XAML Properties
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.ToggleProperties",
            Id,
            new MenuItemDescriptor
            {
                Header     = "XAML _Properties",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE946",
                ToolTip    = "Show or hide the XAML Property Inspector panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(PropertiesPanelUiId))
            });

        // View > XAML Toolbox
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.ToggleToolbox",
            Id,
            new MenuItemDescriptor
            {
                Header     = "XAML _Toolbox",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE7A6",
                ToolTip    = "Show or hide the XAML Toolbox panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(ToolboxPanelUiId))
            });

        // View > Resource Browser
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.ToggleResourceBrowser",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Resource Browser",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE8B9",
                ToolTip    = "Show or hide the Resource Browser panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(ResourcePanelUiId))
            });

        // View > Design Data
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.ToggleDesignData",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Design Data",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE9D9",
                ToolTip    = "Show or hide the Design Data panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(DesignDataPanelUiId))
            });

        // View > Animation Timeline
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.ToggleAnimation",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Animation Timeline",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE916",
                ToolTip    = "Show or hide the Animation Timeline panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(AnimationPanelUiId))
            });

        // View > Design History
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.ToggleHistory",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Design _History",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE81C",
                ToolTip    = "Show or hide the Design History panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(HistoryPanelUiId))
            });

        // View > Binding Inspector
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.ToggleBindingInspector",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Binding Inspector",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE8EC",
                ToolTip    = "Show or hide the Binding Inspector panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(BindingPanelUiId))
            });

        // View > Live Visual Tree
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.ToggleLiveVisualTree",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Live Visual Tree",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE8B0",
                ToolTip    = "Show or hide the Live Visual Tree panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(LiveTreePanelUiId))
            });
    }

    // ── IPluginWithOptions ────────────────────────────────────────────────────

    public string GetOptionsCategory()     => "Editors";
    public string GetOptionsCategoryIcon() => "📐";

    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new XamlDesignerOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    public void SaveOptions()
    {
        _optionsPage?.Save();
    }

    public void LoadOptions()
    {
        XamlDesignerOptions.Invalidate();
        _optionsPage?.Load();
    }
}
