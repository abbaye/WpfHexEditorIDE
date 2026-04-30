// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: ClassDiagramPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Official plugin entry point for the Class Diagram editor.
//     Implements IWpfHexEditorPlugin + IPluginWithOptions.
//     Registers:
//       - ClassOutlinePanel        (Left,  auto-hide, width=220)
//       - ClassPropertiesPanel     (Right, width=260)
//       - ClassToolboxPanel        (Left,  auto-hide, width=220)
//       - RelationshipsPanel       (Right, auto-hide, width=240)
//       - ClassHistoryPanel        (Right, auto-hide, width=240)
//       - DiagramSearchPanel       (Bottom, height=160)
//       - Status bar item          (Left, order=20)
//       - View menu items for all panels
//       - Tools menu items: "View Class Diagram" (on .cs files)
//                           "Generate Class Diagram for Project"
//     Subscribes to IFocusContextService.FocusChanged to wire the
//     active ClassDiagramSplitHost's events to all side panels.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to IFocusContextService.FocusChanged.
//     All UI is constructed and registered on the calling thread (UI thread).
//     UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost
//     on unload — no manual cleanup needed for registered elements.
//     Panel instances are long-lived and reused across document switches.
//     DispatcherPriority.ApplicationIdle for SeedFromCurrentDocument ensures
//     the AssociatedEditor is fully set before resolving the active host.
// ==========================================================

using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.ClassDiagram.Controls;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Options;
using WpfHexEditor.Editor.ClassDiagram.Core.Serializer;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;
using WpfHexEditor.Plugins.ClassDiagram.Analysis;
using WpfHexEditor.Plugins.ClassDiagram.Options;
using WpfHexEditor.Plugins.ClassDiagram.Panels;
using WpfHexEditor.Plugins.ClassDiagram.Properties;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.ClassDiagram;

/// <summary>
/// Entry point for the Class Diagram plugin.
/// Registers all side panels and wires them to the active
/// <see cref="ClassDiagramSplitHost"/> on document-focus changes.
/// </summary>
public sealed class ClassDiagramPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string  Id      => "WpfHexEditor.Plugins.ClassDiagram";
    public string  Name    => ClassDiagramResources.ClassDiagram_PluginName;
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true,
        AccessSettings   = true
    };

    // ── UI ID constants ───────────────────────────────────────────────────────

    public const string MetricsPanelUiId        = "WpfHexEditor.Plugins.ClassDiagram.Panel.Metrics";
    public const string OutlinePanelUiId        = "WpfHexEditor.Plugins.ClassDiagram.Panel.Outline";
    public const string PropertiesPanelUiId    = "WpfHexEditor.Plugins.ClassDiagram.Panel.Properties";
    public const string ToolboxPanelUiId       = "WpfHexEditor.Plugins.ClassDiagram.Panel.Toolbox";
    public const string RelationshipsPanelUiId = "WpfHexEditor.Plugins.ClassDiagram.Panel.Relationships";
    public const string HistoryPanelUiId       = "WpfHexEditor.Plugins.ClassDiagram.Panel.History";
    public const string SearchPanelUiId        = "WpfHexEditor.Plugins.ClassDiagram.Panel.Search";
    private const string StatusBarNodeId       = "WpfHexEditor.Plugins.ClassDiagram.StatusBar.Node";

    // ── Panel instances (long-lived, reused across document switches) ─────────

    private ClassOutlinePanel?      _outlinePanel;
    private ClassPropertiesPanel?   _propertiesPanel;
    private ClassToolboxPanel?      _toolboxPanel;
    private RelationshipsPanel?     _relPanel;
    private ClassHistoryPanel?      _historyPanel;
    private DiagramSearchPanel?     _searchPanel;
    private MetricsDashboardPanel?  _metricsPanel;

    // ── State ─────────────────────────────────────────────────────────────────

    private IIDEHostContext?          _context;
    private ClassDiagramOptionsPage?  _optionsPage;
    private ClassDiagramOptions       _options = new();
    private StatusBarItemDescriptor?  _sbNode;
    private ClassDiagramSplitHost?    _wiredHost;

    // Open diagram tabs keyed by source key (file/folder path or "solution").
    // Used to reactivate an already-open tab instead of creating a duplicate.
    private readonly Dictionary<string, string> _openTabs =
        new(StringComparer.OrdinalIgnoreCase);

    // Open diagram hosts keyed by uiId for status bar and cleanup.
    private readonly Dictionary<string, ClassDiagramSplitHost> _openHosts =
        new(StringComparer.Ordinal);

    // Live-sync services keyed by uiId; disposed when the plugin unloads.
    private readonly Dictionary<string, DiagramLiveSyncService> _liveSyncServices =
        new(StringComparer.Ordinal);

    private bool _liveSyncEnabled = true;

    // Debounce timer for .whcd auto-save (avoids rapid writes during drag/resize)
    private System.Threading.Timer? _whcdSaveDebounce;
    private const int WhcdSaveDelayMs = 1500;

    // Last successfully analyzed document — used by the PlantUML/Mermaid export commands.
    private DiagramDocument? _lastAnalyzedDocument;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // Build panel instances.
        _outlinePanel    = new ClassOutlinePanel();
        _propertiesPanel = new ClassPropertiesPanel();
        _toolboxPanel    = new ClassToolboxPanel();
        _relPanel        = new RelationshipsPanel();
        _historyPanel    = new ClassHistoryPanel();
        _searchPanel     = new DiagramSearchPanel();
        _metricsPanel    = new MetricsDashboardPanel();

        // Register panels in the IDE dock layout.
        context.UIRegistry.RegisterPanel(
            OutlinePanelUiId,
            _outlinePanel,
            Id,
            new PanelDescriptor
            {
                Title           = ClassDiagramResources.ClassDiagram_OutlinePanelTitle,
                DefaultDockSide = "Left",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 220
            });

        context.UIRegistry.RegisterPanel(
            PropertiesPanelUiId,
            _propertiesPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Class Properties",
                DefaultDockSide = "Right",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredWidth  = 260
            });

        context.UIRegistry.RegisterPanel(
            ToolboxPanelUiId,
            _toolboxPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Diagram Toolbox",
                DefaultDockSide = "Left",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 220
            });

        context.UIRegistry.RegisterPanel(
            RelationshipsPanelUiId,
            _relPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Relationships",
                DefaultDockSide = "Right",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 240
            });

        context.UIRegistry.RegisterPanel(
            HistoryPanelUiId,
            _historyPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Diagram History",
                DefaultDockSide = "Right",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 240
            });

        context.UIRegistry.RegisterPanel(
            SearchPanelUiId,
            _searchPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Diagram Search",
                DefaultDockSide = "Bottom",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredHeight = 160
            });

        context.UIRegistry.RegisterPanel(
            MetricsPanelUiId,
            _metricsPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Metrics Dashboard",
                DefaultDockSide = "Right",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredWidth  = 320
            });

        // Status bar item (left, order=20) — shows the selected class name.
        _sbNode = new StatusBarItemDescriptor
        {
            Text      = string.Empty,
            Alignment = StatusBarAlignment.Left,
            Order     = 20,
            ToolTip   = "Selected class in the active Class Diagram"
        };
        context.UIRegistry.RegisterStatusBarItem(StatusBarNodeId, Id, _sbNode);

        // Register menu items (View / Tools).
        RegisterMenuItems(context);

        // Register Solution Explorer context menu contributor.
        context.UIRegistry.RegisterContextMenuContributor(Id, new ClassDiagramContextMenuContributor(this, context));

        // Subscribe to document focus changes so panels sync to the active diagram.
        context.FocusContext.FocusChanged += OnFocusChanged;
        context.FocusContext.FocusChanged += OnFocusSyncStatusBar;

        // Re-render all open diagram hosts when the IDE theme changes.
        // DiagramVisualLayer uses TryFindResource (CD_* tokens) which resolves at draw time,
        // but DrawingVisuals are not automatically invalidated by WPF on resource changes.
        context.Theme.ThemeChanged += OnThemeChanged;

        // Seed panels immediately for any document open at plugin-load time.
        SeedFromCurrentDocument(context);

        // B8 — Restore last diagram on startup if RestoreLastState is enabled.
        // GetSolutionDir is deferred to ApplicationIdle so the solution is fully
        // loaded before we read the session file path (which is next to the .sln).
        if (_options.RestoreLastState)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(async () =>
                {
                    string? solutionDir = GetSolutionDir(context);
                    var session = ClassDiagramSessionStateSerializer.Load(solutionDir);
                    if (session?.LastFilePath is { Length: > 0 } path && System.IO.File.Exists(path))
                        await OpenClassDiagramForFileAsync(path, context);
                }));
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
        {
            _context.FocusContext.FocusChanged -= OnFocusChanged;
            _context.FocusContext.FocusChanged -= OnFocusSyncStatusBar;
            _context.Theme.ThemeChanged        -= OnThemeChanged;
        }

        UnwireCurrentHost();

        foreach (var svc in _liveSyncServices.Values)
            svc.Dispose();
        _liveSyncServices.Clear();

        _outlinePanel    = null;
        _propertiesPanel = null;
        _toolboxPanel    = null;
        _relPanel        = null;
        _historyPanel    = null;
        _searchPanel     = null;
        _metricsPanel    = null;
        _context         = null;
        _optionsPage     = null;
        _sbNode          = null;
        _wiredHost       = null;

        return Task.CompletedTask;
    }

    // ── IPluginWithOptions ────────────────────────────────────────────────────

    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new ClassDiagramOptionsPage(_options);
        _optionsPage.Load();
        return _optionsPage;
    }

    public void SaveOptions() => _optionsPage?.Save();

    public void LoadOptions() => _optionsPage?.Load();

    public string GetOptionsCategory()     => ClassDiagramResources.ClassDiagram_OptionsCategory;
    public string GetOptionsCategoryIcon() => "📐";

    // ── Focus tracking ────────────────────────────────────────────────────────

    private void OnFocusChanged(object? sender, FocusChangedEventArgs e)
    {
        ClassDiagramSplitHost? host = ResolveActiveHost(e.ActiveDocument);

        // Same host — no rewiring needed.
        if (host is not null && ReferenceEquals(host, _wiredHost)) return;

        UnwireCurrentHost();
        _wiredHost = host;

        if (host is null)
        {
            ClearPanels();
            return;
        }

        WireHost(host);
    }

    /// <summary>
    /// Syncs the IDE status bar when focus switches to an open class diagram tab.
    /// The MainWindow's OnActiveDocumentChanged path handles normal editors; this
    /// handler covers plugin-owned document tabs that bypass CreateSmartFileEditorContent.
    /// </summary>
    private void OnFocusSyncStatusBar(object? sender, FocusChangedEventArgs e)
    {
        var host = ResolveActiveHost(e.ActiveDocument);
        if (host is not null)
            host.RefreshStatusBarItems();
    }

    /// <summary>
    /// Resolves the <see cref="ClassDiagramSplitHost"/> from an active IDE document,
    /// or returns null when the active document is not a class diagram.
    /// Uses the same pattern as XamlDesignerPlugin: look up the DocumentModel by
    /// ContentId and cast its AssociatedEditor.
    /// </summary>
    private ClassDiagramSplitHost? ResolveActiveHost(IDocument? doc)
    {
        if (doc is null || _context is null) return null;

        var model = _context.DocumentHost.Documents.OpenDocuments
            .FirstOrDefault(d => d.ContentId == doc.ContentId);

        return model?.AssociatedEditor as ClassDiagramSplitHost;
    }

    /// <summary>
    /// Wires all side panels to the given host and seeds them from the current document.
    /// </summary>
    private void WireHost(ClassDiagramSplitHost host)
    {
        host.SelectedClassChanged        += OnSelectedClassChanged;
        host.DiagramChanged              += OnDiagramChanged;
        host.NavigateToMemberRequested   += OnNavigateToMember;
        host.RenameNodeRequested         += OnRenameNode;
        host.ShowPropertiesRequested     += OnShowPropertiesRequested;
        host.FindReferencesRequested     += OnFindReferencesRequested;
        host.ShowMetricsRequested        += OnShowMetricsRequested;
        host.ChangeNodeColorRequested    += OnChangeNodeColorRequested;

        // History panel — bind to the host's undo manager.
        if (_historyPanel is not null)
        {
            _historyPanel.ViewModel.SetManager(host.UndoManager);
            _historyPanel.ViewModel.JumpRequested += OnHistoryJumpRequested;
        }

        // Wire search panel result selection → canvas selection.
        if (_searchPanel is not null)
            _searchPanel.ViewModel.ResultSelected += OnSearchResultSelected;

        // Wire relationships panel selection → highlight arrow + select source node.
        if (_relPanel is not null)
            _relPanel.ViewModel.SelectedRelationshipChanged += OnRelationshipSelected;

        // Seed panel content from the current document state.
        // DispatcherPriority.ApplicationIdle ensures the AssociatedEditor is fully
        // initialised before we call SetDocument (mirrors the XamlDesigner pattern).
        Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            () => SeedPanelsFromHost(host));
    }

    /// <summary>
    /// Detaches all event handlers from the outgoing host.
    /// </summary>
    private void UnwireCurrentHost()
    {
        if (_wiredHost is null) return;

        _wiredHost.SelectedClassChanged        -= OnSelectedClassChanged;
        _wiredHost.DiagramChanged              -= OnDiagramChanged;
        _wiredHost.NavigateToMemberRequested   -= OnNavigateToMember;
        _wiredHost.RenameNodeRequested         -= OnRenameNode;
        _wiredHost.ShowPropertiesRequested     -= OnShowPropertiesRequested;
        _wiredHost.FindReferencesRequested     -= OnFindReferencesRequested;
        _wiredHost.ShowMetricsRequested        -= OnShowMetricsRequested;
        _wiredHost.ChangeNodeColorRequested    -= OnChangeNodeColorRequested;

        if (_historyPanel is not null)
        {
            _historyPanel.ViewModel.JumpRequested -= OnHistoryJumpRequested;
            _historyPanel.ViewModel.SetManager(null!); // Clear to avoid stale reference.
        }

        if (_searchPanel is not null)
            _searchPanel.ViewModel.ResultSelected -= OnSearchResultSelected;

        if (_relPanel is not null)
            _relPanel.ViewModel.SelectedRelationshipChanged -= OnRelationshipSelected;

        _wiredHost = null;
    }

    /// <summary>
    /// Populates all panels from the active host's document.
    /// Called at ApplicationIdle priority to guarantee the host is stable.
    /// </summary>
    private void SeedPanelsFromHost(ClassDiagramSplitHost host)
    {
        _outlinePanel?.ViewModel.SetDocument(host.Document);
        _relPanel?.ViewModel.SetDocument(host.Document);
        _searchPanel?.ViewModel.SetDocument(host.Document);
        _metricsPanel?.ViewModel.SetDocument(host.Document);
        _propertiesPanel?.ViewModel.SetSelection(null);
        UpdateStatusBar(null);
    }

    /// <summary>
    /// Clears all panels when no class diagram document is active.
    /// </summary>
    private void ClearPanels()
    {
        if (_outlinePanel is not null)
            _outlinePanel.ViewModel.SetDocument(new DiagramDocument());
        if (_relPanel is not null)
            _relPanel.ViewModel.SetDocument(new DiagramDocument());
        if (_searchPanel is not null)
            _searchPanel.ViewModel.SetDocument(new DiagramDocument());
        if (_metricsPanel is not null)
            _metricsPanel.ViewModel.SetDocument(null);
        if (_propertiesPanel is not null)
            _propertiesPanel.ViewModel.SetSelection(null);
        UpdateStatusBar(null);
    }

    // ── Host event handlers ───────────────────────────────────────────────────

    private void OnSelectedClassChanged(object? sender, ClassNode? node)
    {
        // Sync outline panel selection to the newly selected class node.
        if (_outlinePanel is not null && _outlinePanel.ViewModel is { } vm)
        {
            vm.SelectedNode = node is not null
                ? vm.Nodes.FirstOrDefault(n => n.Node?.Name == node.Name)
                : null;
        }

        // Sync properties panel.
        _propertiesPanel?.ViewModel.SetSelection(node, null);

        // Update status bar.
        UpdateStatusBar(node);
    }

    private void OnDiagramChanged(object? sender, EventArgs e)
    {
        if (_wiredHost is null) return;
        _outlinePanel?.ViewModel.SetDocument(_wiredHost.Document);
        _relPanel?.ViewModel.SetDocument(_wiredHost.Document);
        _searchPanel?.ViewModel.SetDocument(_wiredHost.Document);
    }

    /// <summary>
    /// Forces a full re-render of every open diagram after a theme change.
    /// DrawingVisuals don't auto-refresh when DynamicResource tokens change;
    /// we must explicitly call RenderAll so brushes are re-resolved.
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (var host in _openHosts.Values)
            host.RefreshDiagramVisuals();
    }

    private void OnHistoryJumpRequested(object? sender, int targetIndex)
    {
        if (_wiredHost is null) return;

        // Calculate how many undo/redo steps are needed to reach targetIndex.
        int current = _wiredHost.UndoManager.UndoCount;
        int delta   = targetIndex - current;

        if (delta < 0)
            for (int i = 0; i < -delta; i++) _wiredHost.Undo();
        else
            for (int i = 0; i < delta; i++) _wiredHost.Redo();
    }

    private void OnNavigateToMember(object? sender, (ClassNode Node, ClassMember Member) e)
    {
        if (_context is null) return;

        string? filePath = e.Member?.SourceFilePath ?? e.Node.SourceFilePath;
        int     line     = e.Member?.SourceLineOneBased > 0
            ? e.Member.SourceLineOneBased
            : e.Node.SourceLineOneBased;

        if (string.IsNullOrEmpty(filePath))
        {
            _context.Output.Warning(
                $"[Class Diagram] No source file for '{e.Node.Name}'. "
                + "This type may come from an external assembly or manual diagram.");
            return;
        }

        // Ensure we have at least line 1 even when not resolvable
        if (line <= 0) line = 1;

        _context.Output.Info($"[Class Diagram] Navigate → {Path.GetFileName(filePath)}:{line}");
        _context.DocumentHost.ActivateAndNavigateTo(filePath, line, 1);
    }

    private void OnRenameNode(object? sender, (ClassNode Node, string? NewName) e)
    {
        // Show a simple WPF input dialog.
        string? newName = ShowInputDialog($"Rename '{e.Node.Name}' to:", "Rename", e.Node.Name);

        if (string.IsNullOrWhiteSpace(newName) || newName == e.Node.Name) return;

        _ = Task.Run(async () =>
        {
            bool ok = await DiagramCodeEditService.RenameMemberAsync(
                e.Node,
                // Pass a synthetic member representing the type declaration itself
                new WpfHexEditor.Editor.ClassDiagram.Core.Model.ClassMember
                {
                    Name               = e.Node.Name,
                    SourceFilePath     = e.Node.SourceFilePath,
                    SourceLineOneBased = e.Node.SourceLineOneBased
                },
                newName);

            if (!ok)
                Application.Current.Dispatcher.BeginInvoke(
                    () => _context?.Output.Warning($"[Class Diagram] Rename failed for '{e.Node.Name}'."));
        });
    }

    private void OnSearchResultSelected(object? sender, SearchResultItem? result)
    {
        if (result is null || _wiredHost is null) return;

        // Select the node on canvas + zoom to it
        _wiredHost.SelectNode(result.Node);
        _wiredHost.ZoomToNode(result.Node);
        _propertiesPanel?.ViewModel.SetSelection(result.Node, result.Member);
    }

    private void OnRelationshipSelected(object? sender, WpfHexEditor.Editor.ClassDiagram.Core.Model.ClassRelationship? rel)
    {
        if (_wiredHost is null) return;

        // Clear arrow highlight when selection is cleared.
        if (rel is null)
        {
            _wiredHost.HighlightRelationship(null);
            return;
        }

        // Highlight the arrow and select the source node.
        _wiredHost.HighlightRelationship(rel.SourceId);

        var srcNode = _wiredHost.Document.Classes.FirstOrDefault(n => n.Id == rel.SourceId);
        if (srcNode is not null)
        {
            _wiredHost.SelectNode(srcNode);
            _wiredHost.ZoomToNode(srcNode);
            _propertiesPanel?.ViewModel.SetSelection(srcNode, null);
        }
    }

    private void OnShowPropertiesRequested(object? sender, WpfHexEditor.Editor.ClassDiagram.Core.Model.ClassNode node)
    {
        if (_context is null) return;
        // Show / focus the properties panel and update it for the selected node.
        _context.UIRegistry.ShowPanel(PropertiesPanelUiId);
        _propertiesPanel?.ViewModel.SetSelection(node, null);
    }

    private void OnFindReferencesRequested(object? sender, WpfHexEditor.Editor.ClassDiagram.Core.Model.ClassNode node)
    {
        // Navigate to the class declaration to let the IDE's Find References work from there
        OnNavigateToMember(sender!, (node, null!));
    }

    private void OnShowMetricsRequested(object? sender, WpfHexEditor.Editor.ClassDiagram.Core.Model.ClassNode node)
    {
        if (_context is null) return;
        // Show the properties panel on the metrics tab
        _context.UIRegistry.ShowPanel(PropertiesPanelUiId);
        _propertiesPanel?.ViewModel.SetSelection(node, null);
    }

    private void OnChangeNodeColorRequested(object? sender, WpfHexEditor.Editor.ClassDiagram.Core.Model.ClassNode node)
    {
        // Preset accent colors to choose from
        (byte R, byte G, byte B)[] presets =
        [
            (79,  193, 255), // light blue (default class)
            (78,  201, 176), // green (interface)
            (197, 134, 192), // violet (enum)
            (220, 220, 170), // yellow (struct)
            (86,  156, 214), // blue (abstract)
            (156, 220, 254), // cyan (record)
            (220, 160,  80), // orange
            (200,  80,  80), // red
            (120, 200, 120), // lime
            (180, 180, 180), // grey
        ];

        var popup = new System.Windows.Controls.Primitives.Popup
        {
            StaysOpen      = false,
            AllowsTransparency = true,
            PlacementTarget = sender as UIElement ?? _wiredHost,
            Placement       = System.Windows.Controls.Primitives.PlacementMode.Mouse
        };

        var panel = new System.Windows.Controls.WrapPanel { Width = 170, Background = Brushes.Transparent };
        var border = new System.Windows.Controls.Border
        {
            Background      = new SolidColorBrush(Color.FromRgb(40, 40, 50)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(80, 80, 100)),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(6),
            Child           = panel
        };

        // "Reset" button to clear custom color
        var resetBtn = new System.Windows.Controls.Button
        {
            Content    = "Reset",
            Width      = 158,
            Height     = 22,
            Margin     = new Thickness(0, 0, 0, 4),
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 70)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        resetBtn.Click += (_, _) =>
        {
            node.CustomColor = null;
            _wiredHost?.InvalidateAndRender();
            popup.IsOpen = false;
        };
        panel.Children.Add(resetBtn);

        foreach (var (r, g, b) in presets)
        {
            var swatch = new System.Windows.Controls.Border
            {
                Width           = 28,
                Height          = 28,
                Margin          = new Thickness(2),
                CornerRadius    = new CornerRadius(4),
                Background      = new SolidColorBrush(Color.FromRgb(r, g, b)),
                BorderBrush     = node.CustomColor.HasValue && node.CustomColor.Value == (r, g, b)
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(node.CustomColor.HasValue && node.CustomColor.Value == (r, g, b) ? 2 : 1),
                Cursor          = System.Windows.Input.Cursors.Hand
            };
            var capturedColor = (r, g, b);
            swatch.MouseLeftButtonDown += (_, _) =>
            {
                node.CustomColor = capturedColor;
                _wiredHost?.InvalidateAndRender();
                popup.IsOpen = false;
            };
            panel.Children.Add(swatch);
        }

        popup.Child = border;
        popup.IsOpen = true;
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    private void UpdateStatusBar(ClassNode? node)
    {
        if (_sbNode is null) return;
        _sbNode.Text = node is not null ? $"  {node.Kind}: {node.Name}" : string.Empty;
    }

    // ── Seeding on plugin load ────────────────────────────────────────────────

    /// <summary>
    /// Seeds panels from the document already active at plugin-load time.
    /// Without this, a .classdiagram file open before the plugin loaded would
    /// never trigger OnFocusChanged, leaving all panels empty.
    /// </summary>
    private void SeedFromCurrentDocument(IIDEHostContext context)
    {
        Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            () =>
            {
                if (context.FocusContext.ActiveDocument is not { } activeDoc) return;
                OnFocusChanged(this, new FocusChangedEventArgs { ActiveDocument = activeDoc });
            });
    }

    // ── Menu registration ─────────────────────────────────────────────────────

    private void RegisterMenuItems(IIDEHostContext context)
    {
        // View > Panels submenu — one item per panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.View.Outline",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Class _Outline",
                ParentPath = "View",
                IconGlyph  = "\uE8A5",
                ToolTip    = "Show / hide the Class Outline panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(OutlinePanelUiId)),
                Group      = "ClassDiagram"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.View.Properties",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Class _Properties",
                ParentPath = "View",
                GestureText = "F4",
                IconGlyph  = "\uE90F",
                ToolTip    = "Show / hide the Class Properties panel (F4)",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(PropertiesPanelUiId)),
                Group      = "ClassDiagram"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.View.Toolbox",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Diagram _Toolbox",
                ParentPath = "View",
                IconGlyph  = "\uE7EF",
                ToolTip    = "Show / hide the Diagram Toolbox panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(ToolboxPanelUiId)),
                Group      = "ClassDiagram"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.View.Relationships",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Relationships",
                ParentPath = "View",
                IconGlyph  = "\uE8D9",
                ToolTip    = "Show / hide the Relationships panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(RelationshipsPanelUiId)),
                Group      = "ClassDiagram"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.View.History",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Diagram _History",
                ParentPath = "View",
                IconGlyph  = "\uE81C",
                ToolTip    = "Show / hide the Diagram History panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(HistoryPanelUiId)),
                Group      = "ClassDiagram"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.View.Search",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Diagram _Search",
                ParentPath = "View",
                GestureText = "Ctrl+Shift+D",
                IconGlyph  = "\uE721",
                ToolTip    = "Show / hide the Diagram Search panel (Ctrl+Shift+D)",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(SearchPanelUiId)),
                Group      = "ClassDiagram"
            });

        // View > Metrics Dashboard panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.View.Metrics",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Metrics Dashboard",
                ParentPath = "View",
                IconGlyph  = "\uE9F3",
                ToolTip    = "Show / hide the coupling metrics dashboard",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(MetricsPanelUiId)),
                Group      = "ClassDiagram"
            });

        // View > Live Sync toggle.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.View.LiveSync",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Live Sync",
                ParentPath = "View",
                IconGlyph  = "\uE895",
                ToolTip    = "Auto-refresh the class diagram when source files change on disk",
                Command    = new RelayCommand(_ =>
                {
                    _liveSyncEnabled = !_liveSyncEnabled;
                    if (!_liveSyncEnabled)
                    {
                        foreach (var svc in _liveSyncServices.Values) svc.Dispose();
                        _liveSyncServices.Clear();
                    }
                }),
                Group = "ClassDiagram"
            });

        // Tools menu — Solution Explorer context menu actions.
        // Tools menu — AI generation.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Tools.AIGenerate",
            Id,
            new MenuItemDescriptor
            {
                Header     = "✨ Generate Diagram from Description (AI)…",
                ParentPath = "Tools",
                IconGlyph  = "\uE8D4",
                ToolTip    = "Generate a class diagram from a natural-language description using AI",
                Command    = new RelayCommand(
                    execute: _ => _ = OpenAIGeneratedDiagramAsync(context)),
                Group = "ClassDiagram"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Tools.ViewDiagram",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_View Class Diagram for Active File",
                ParentPath = "Tools",
                IconGlyph  = "\uE92F",
                ToolTip    = "Analyze the active C# file and open it as a class diagram",
                Command    = new RelayCommand(_ => _ = OpenDiagramForActiveFileAsync(context)),
                Group = "ClassDiagram"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Tools.GenerateSolution",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Generate Class Diagram for Solution",
                ParentPath = "Tools",
                IconGlyph  = "\uE8A5",
                ToolTip    = "Analyze all C# files in the active solution and open a combined class diagram with per-project swimlanes",
                Command    = new RelayCommand(
                    execute: _ => _ = OpenDiagramForSolutionAsync(context),
                    canExecute: _ => context.SolutionExplorer.HasActiveSolution),
                Group = "ClassDiagram"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Tools.GenerateProject",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Generate Class Diagram for _Project…",
                ParentPath = "Tools",
                IconGlyph  = "\uE8EC",
                ToolTip    = "Pick a project from the active solution and open its class diagram",
                Command    = new RelayCommand(
                    execute: _ => _ = PickAndOpenProjectDiagramAsync(context),
                    canExecute: _ => context.SolutionExplorer.HasActiveSolution),
                Group = "ClassDiagram"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Tools.ExportPlantUml",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Export Class Diagram as _PlantUML…",
                ParentPath = "Tools",
                IconGlyph  = "\uE8A4",
                ToolTip    = "Export the active class diagram to PlantUML text format",
                Command    = new RelayCommand(
                    execute:    _ => ExportActiveDiagram(context, ExportFormat.PlantUml),
                    canExecute: _ => _wiredHost is not null),
                Group = "ClassDiagramExport"
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Tools.ExportMermaid",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Export Class Diagram as _Mermaid…",
                ParentPath = "Tools",
                IconGlyph  = "\uE8A4",
                ToolTip    = "Export the active class diagram to Mermaid classDiagram format",
                Command    = new RelayCommand(
                    execute:    _ => ExportActiveDiagram(context, ExportFormat.Mermaid),
                    canExecute: _ => _wiredHost is not null),
                Group = "ClassDiagramExport"
            });
    }

    private enum ExportFormat { PlantUml, Mermaid }

    private void ExportActiveDiagram(IIDEHostContext context, ExportFormat format)
    {
        if (_wiredHost is null) return;

        if (_lastAnalyzedDocument is null)
        {
            context.Output.Info("[Class Diagram] No analyzed document available for export.");
            return;
        }

        string text   = format == ExportFormat.PlantUml
            ? DiagramExportService.ToPlantUml(_lastAnalyzedDocument)
            : DiagramExportService.ToMermaid(_lastAnalyzedDocument);

        string ext    = format == ExportFormat.PlantUml ? "puml" : "md";
        string filter = format == ExportFormat.PlantUml
            ? "PlantUML files (*.puml)|*.puml|Text files (*.txt)|*.txt"
            : "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt";

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = format == ExportFormat.PlantUml ? "Export as PlantUML" : "Export as Mermaid",
            Filter           = filter,
            DefaultExt       = ext,
            FileName         = "ClassDiagram." + ext
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dlg.FileName, text, System.Text.Encoding.UTF8);
                context.Output.Info($"[Class Diagram] Exported to {dlg.FileName}");
            }
            catch (Exception ex)
            {
                context.Output.Info($"[Class Diagram] Export failed: {ex.Message}");
            }
        }
    }

    // ── Solution Explorer actions ─────────────────────────────────────────────

    /// <summary>
    /// Called from the "View Class Diagram for Active File" menu item.
    /// Parses the currently active .cs file and opens a virtual diagram document.
    /// </summary>
    private async Task OpenDiagramForActiveFileAsync(IIDEHostContext context)
    {
        string? filePath = context.FocusContext.ActiveDocument?.FilePath;

        // If no active source file, let user pick one via dialog.
        if (string.IsNullOrEmpty(filePath) || !IsSourceFile(filePath))
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Open Class Diagram for File",
                Filter = "C# Files (*.cs)|*.cs|VB.NET Files (*.vb)|*.vb|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() != true) return;
            filePath = dlg.FileName;
            if (!IsSourceFile(filePath))
            {
                context.Output.Info("[Class Diagram] Selected file is not a supported source file.");
                return;
            }
        }

        await OpenClassDiagramForFileAsync(filePath, context);
    }

    /// <summary>
    /// Called from the "Generate Class Diagram for Solution" menu item.
    /// Analyzes all .cs files per project in the active solution, opens a combined
    /// diagram with per-project swimlane grouping, and persists state to a .whscd
    /// twin file next to the solution file.
    /// </summary>
    public async Task OpenDiagramForSolutionAsync(IIDEHostContext context)
    {
        const string key = "solution";
        if (_openTabs.TryGetValue(key, out string? existingId)
            && context.UIRegistry.Exists(existingId))
        {
            context.UIRegistry.FocusPanel(existingId);
            return;
        }

        // ── 1. Collect projects + source files ───────────────────────────────
        IReadOnlyList<SolutionProjectInfo> projects = context.SolutionExplorer.GetSolutionProjects();

        // Filter each project's source files to those supported by the analyzer
        var filtered = projects
            .Where(p => !(_options.SolutionExcludeTestProjects && IsTestProject(p.Name)))
            .Select(p => (
                Name:  p.Name,
                Path:  p.ProjectPath,
                Files: (IReadOnlyList<string>)p.SourceFiles
                    .Where(IsSourceFile)
                    .ToList()))
            .Where(p => p.Files.Count > 0)
            .ToList();

        int totalFiles = filtered.Sum(p => p.Files.Count);
        if (totalFiles == 0)
        {
            context.Output.Info("[Class Diagram] No C# or VB.NET files found in the active solution.");
            return;
        }

        // Optional large-solution warning
        if (_options.SolutionMaxFilesPromptThreshold > 0
            && totalFiles > _options.SolutionMaxFilesPromptThreshold)
        {
            var answer = MessageBox.Show(
                $"The solution contains {totalFiles} source files ({filtered.Count} projects).\n\n" +
                $"Generating the diagram may take a moment for large solutions.\n\n" +
                "[Yes]  Continue\n[No]   Cancel",
                "Generate Class Diagram for Solution",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }

        context.Output.Info(
            $"[Class Diagram] Analyzing {totalFiles} source file(s) across {filtered.Count} project(s)…");

        // ── 2. Analyze with per-file progress reporting ──────────────────────
        // For solution-wide diagrams, skip the ForceDirected auto-layout:
        // with 100+ nodes it runs O(n²)×iterations which can take minutes.
        // A grid layout is applied here instead after analysis completes.
        var solutionOptions = new ClassDiagramOptions
        {
            AutoLayout              = false,    // layout applied below with node-count guard
            LayoutStrategy          = _options.LayoutStrategy,
            ForceDirectedIterations = _options.ForceDirectedIterations,
            SpringLength            = _options.SpringLength,
            SnapSize                = _options.SnapSize,
            DefaultNodeWidth        = _options.DefaultNodeWidth,
            DefaultNodeHeight       = _options.DefaultNodeHeight,
            IncludePrivateMembers   = _options.SolutionIncludePrivateMembers,
            IncludeInheritedMembers = _options.IncludeInheritedMembers,
            GroupByNamespace        = _options.GroupByNamespace,
        };

        using var cts = new System.Threading.CancellationTokenSource();
        var progress  = new Progress<RoslynClassDiagramAnalyzer.ClassDiagramProgress>(p =>
            context.Output.Info($"[Class Diagram] [{p.Analyzed}/{p.Total}] {p.CurrentFile}"));

        DiagramDocument doc;
        try
        {
            doc = await Task.Run(
                () => RoslynClassDiagramAnalyzer.AnalyzeProjects(filtered, solutionOptions, progress, cts.Token),
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            context.Output.Info("[Class Diagram] Analysis cancelled.");
            return;
        }
        catch (Exception ex)
        {
            context.Output.Info($"[Class Diagram] Analysis error: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        context.Output.Info(
            $"[Class Diagram] Parsed {doc.Classes.Count} classes / {doc.Relationships.Count} relationships" +
            $" across {doc.ProjectGroups.Count} project(s). Applying layout…");

        // Apply a fast grid/hierarchical layout on the background thread.
        // ForceDirected is O(n²)×iterations — prohibitive for 100+ nodes.
        // Nodes with a saved .whscd position will be repositioned by ApplyWhcdState later.
        if (doc.Classes.Count > 0)
        {
            // ForceDirected is O(n²)×iterations — switch to Hierarchical (O(n log n))
            // for solutions with many nodes to keep layout time under 1 second.
            var layoutKind = doc.Classes.Count <= 60 && _options.LayoutStrategy == WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyKind.ForceDirected
                ? WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyKind.ForceDirected
                : WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyKind.Hierarchical;

            int iterations = layoutKind == WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyKind.ForceDirected
                ? _options.ForceDirectedIterations : 0;
            await Task.Run(() =>
                WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyFactory
                    .Create(layoutKind)
                    .Layout(doc, new WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutOptions
                    {
                        Strategy        = layoutKind,
                        ColSpacing      = 60,
                        RowSpacing      = 80,
                        CanvasPadding   = 40,
                        MinBoxWidth     = solutionOptions.DefaultNodeWidth,
                        ForceIterations = iterations,
                        SpringLength    = _options.SpringLength
                    }));
        }

        context.Output.Info($"[Class Diagram] Layout done. Opening tab…");

        try
        {
            OpenSolutionDiagramTab(doc, context);
        }
        catch (Exception ex)
        {
            context.Output.Info($"[Class Diagram] Tab open error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void OpenSolutionDiagramTab(
        DiagramDocument doc,
        IIDEHostContext context)
    {
        const string key = "solution";

        // ── 3. Load .whscd twin (restore positions + filters) ────────────────
        string? solutionPath = context.SolutionExplorer.ActiveSolutionPath;
        string? whscdPath    = solutionPath is not null
            ? WhscdSerializer.GetWhscdPath(solutionPath)
            : null;

        WhscdDocument? whscd = whscdPath is not null ? WhscdSerializer.Load(whscdPath) : null;

        // Apply saved project group colors to the diagram groups
        if (whscd is not null)
        {
            var colorMap = whscd.ProjectGroups
                .ToDictionary(g => g.ProjectPath, g => g.Color, StringComparer.OrdinalIgnoreCase);
            foreach (var group in doc.ProjectGroups)
                if (colorMap.TryGetValue(group.ProjectPath, out string? color))
                    group.Color = color;
        }

        _lastAnalyzedDocument = doc;

        // ── 4. Build and register the tab ────────────────────────────────────
        string solutionName = context.SolutionExplorer.ActiveSolutionName ?? "Solution";
        string title = $"{solutionName} [Class Diagram]";
        string uiId  = $"doc-class-diagram-{Guid.NewGuid():N}";

        var host = new ClassDiagramSplitHost();
        host.LoadDocument(doc, title);

        // Apply swimlane default from options when no saved state exists
        if (whscd is null && _options.SolutionShowSwimLanesByDefault)
        {
            // Defer until after layout so the canvas is measured first
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                () => host.ApplyWhcdState(new WhcdDocument { ShowSwimLanes = true }));
        }

        // Restore view state from .whscd
        if (whscd is not null)
        {
            // Convert WhscdDocument → WhcdDocument for ApplyWhcdState reuse
            var legacyState = new WhcdDocument
            {
                Zoom              = whscd.Zoom,
                OffsetX           = whscd.OffsetX,
                OffsetY           = whscd.OffsetY,
                MinimapVisible    = whscd.MinimapVisible,
                SelectedNodeId    = whscd.SelectedNodeId,
                ShowSwimLanes     = whscd.ShowSwimLanes,
                SnapToGrid        = whscd.SnapToGrid,
                ViewMode          = whscd.ViewMode,
                SplitLayout       = whscd.SplitLayout,
                SplitColRatio     = whscd.SplitColRatio,
                SplitRowRatio     = whscd.SplitRowRatio,
                CollapsedSections = whscd.CollapsedSections,
                Nodes             = whscd.Nodes
            };
            host.ApplyWhcdState(legacyState);
            context.Output.Info(
                $"[WHSCD] Restored {whscd.Nodes.Count} node positions from {Path.GetFileName(whscdPath)}");
        }

        _openTabs[key]   = uiId;
        _openHosts[uiId] = host;
        context.UIRegistry.RegisterDocumentTab(uiId, host, Id, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = $"Solution class diagram — {solutionPath ?? "no path"}",
            CanClose  = true,
        });

        // ── 5. Save .whscd immediately + auto-save on change (debounced 1.5 s) ──
        if (whscdPath is not null)
        {
            string capturedWhscdPath    = whscdPath;
            string capturedSolutionPath = solutionPath!;

            // Initial save so the file is always created on first open
            SaveWhscdState(host, doc, capturedSolutionPath, capturedWhscdPath, context);

            host.DiagramChanged += (_, _) =>
            {
                _whcdSaveDebounce?.Dispose();
                _whcdSaveDebounce = new System.Threading.Timer(_ =>
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                        SaveWhscdState(host, doc, capturedSolutionPath, capturedWhscdPath, context)),
                    null, WhcdSaveDelayMs, System.Threading.Timeout.Infinite);
            };
        }

        // ── 6. Report ────────────────────────────────────────────────────────
        context.Output.Info(
            $"[Class Diagram] Generated diagram with {doc.Classes.Count} classes, " +
            $"{doc.Relationships.Count} relationships, " +
            $"{doc.ProjectGroups.Count} project group(s). " +
            $"Export via Tools → Export Class Diagram.");
    }

    // ── Project-level diagram ────────────────────────────────────────────────

    /// <summary>
    /// Shows a project picker and opens a class diagram for the selected project.
    /// Entry point for Tools → "Generate Class Diagram for Project…".
    /// </summary>
    private async Task PickAndOpenProjectDiagramAsync(IIDEHostContext context)
    {
        var projects = context.SolutionExplorer.GetSolutionProjects();
        if (projects.Count == 0)
        {
            context.Output.Info("[Class Diagram] No projects found in the active solution.");
            return;
        }

        // Simple project picker dialog using a ListBox.
        var listBox = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            MaxHeight     = 300,
            Margin        = new System.Windows.Thickness(0, 8, 0, 0),
        };
        foreach (var p in projects)
            listBox.Items.Add(p.Name);
        listBox.SelectedIndex = 0;

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Select a project:", Margin = new System.Windows.Thickness(0, 0, 0, 4) });
        panel.Children.Add(listBox);

        var win = new Window
        {
            Title                 = "Generate Class Diagram for Project",
            Content               = panel,
            SizeToContent         = SizeToContent.WidthAndHeight,
            MinWidth              = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode            = ResizeMode.NoResize,
            Owner                 = Application.Current?.MainWindow,
            Padding               = new System.Windows.Thickness(16),
        };

        var okButton = new Button { Content = "Open", IsDefault = true, Width = 80, Margin = new System.Windows.Thickness(0, 8, 8, 0) };
        var cancelButton = new Button { Content = "Cancel", IsCancel = true, Width = 80, Margin = new System.Windows.Thickness(0, 8, 0, 0) };
        okButton.Click     += (_, _) => { win.DialogResult = true; win.Close(); };
        cancelButton.Click += (_, _) => { win.DialogResult = false; win.Close(); };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        panel.Children.Add(buttons);

        if (win.ShowDialog() != true || listBox.SelectedIndex < 0) return;

        var selected = projects[listBox.SelectedIndex];
        await OpenDiagramForProjectAsync(selected.ProjectPath, context);
    }

    /// <summary>
    /// Analyzes a single project and opens its class diagram.
    /// Called from Solution Explorer context menu ("Generate Class Diagram for Project")
    /// and from the project picker dialog.
    /// </summary>
    public async Task OpenDiagramForProjectAsync(string projectPath, IIDEHostContext context)
    {
        // Dedup: reactivate existing tab for this project.
        if (_openTabs.TryGetValue(projectPath, out string? existingId)
            && context.UIRegistry.Exists(existingId))
        {
            context.UIRegistry.FocusPanel(existingId);
            return;
        }
        _openTabs.Remove(projectPath);

        // Resolve project info from solution (enables Roslyn-aware analysis + SourceFiles list).
        var allProjects     = context.SolutionExplorer.GetSolutionProjects();
        var projectInfo     = allProjects.FirstOrDefault(p =>
            string.Equals(p.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));
        string projectName  = projectInfo?.Name ?? Path.GetFileNameWithoutExtension(projectPath);

        IReadOnlyList<string> sourceFiles;
        if (projectInfo is not null)
        {
            sourceFiles = projectInfo.SourceFiles.Where(IsSourceFile).ToList();
        }
        else
        {
            // No solution loaded — fallback to scanning the project directory.
            string? projectDir = Path.GetDirectoryName(projectPath);
            sourceFiles = projectDir is not null
                ? Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(projectDir, "*.vb", SearchOption.AllDirectories))
                    .Where(IsSourceFile)
                    .ToList()
                : [];
        }

        if (sourceFiles.Count == 0)
        {
            context.Output.Info($"[Class Diagram] No C# or VB.NET files found in project '{projectName}'.");
            return;
        }

        // Threshold prompt (mirrors solution-level behaviour).
        if (_options.SolutionMaxFilesPromptThreshold > 0
            && sourceFiles.Count > _options.SolutionMaxFilesPromptThreshold)
        {
            var answer = MessageBox.Show(
                $"The project '{projectName}' contains {sourceFiles.Count} source files.\n\n" +
                "Generating the diagram may take a moment.\n\n" +
                "[Yes]  Continue\n[No]   Cancel",
                "Generate Class Diagram for Project",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }

        context.Output.Info($"[Class Diagram] Analyzing {sourceFiles.Count} file(s) in project '{projectName}'…");

        // Build a single-project list compatible with AnalyzeProjects.
        var singleProject = new[]
        {
            (Name: projectName, Path: projectPath, Files: sourceFiles)
        };

        var projectOptions = new ClassDiagramOptions
        {
            AutoLayout              = false,
            LayoutStrategy          = _options.LayoutStrategy,
            ForceDirectedIterations = _options.ForceDirectedIterations,
            SpringLength            = _options.SpringLength,
            SnapSize                = _options.SnapSize,
            DefaultNodeWidth        = _options.DefaultNodeWidth,
            DefaultNodeHeight       = _options.DefaultNodeHeight,
            IncludePrivateMembers   = _options.SolutionIncludePrivateMembers,
            IncludeInheritedMembers = _options.IncludeInheritedMembers,
            GroupByNamespace        = _options.GroupByNamespace,
        };

        using var cts      = new System.Threading.CancellationTokenSource();
        var       progress = new Progress<RoslynClassDiagramAnalyzer.ClassDiagramProgress>(p =>
            context.Output.Info($"[Class Diagram] [{p.Analyzed}/{p.Total}] {p.CurrentFile}"));

        DiagramDocument doc;
        try
        {
            doc = await Task.Run(
                () => RoslynClassDiagramAnalyzer.AnalyzeProjects(singleProject, projectOptions, progress, cts.Token),
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            context.Output.Info("[Class Diagram] Analysis cancelled.");
            return;
        }
        catch (Exception ex)
        {
            context.Output.Info($"[Class Diagram] Analysis error: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        context.Output.Info(
            $"[Class Diagram] Parsed {doc.Classes.Count} class(es) / {doc.Relationships.Count} relationship(s)" +
            $" in project '{projectName}'. Applying layout…");

        // Layout: ForceDirected for small projects, Hierarchical for large ones.
        if (doc.Classes.Count > 0)
        {
            var layoutKind = doc.Classes.Count <= 60 && _options.LayoutStrategy == WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyKind.ForceDirected
                ? WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyKind.ForceDirected
                : WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyKind.Hierarchical;

            int iterations = layoutKind == WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyKind.ForceDirected
                ? _options.ForceDirectedIterations : 0;
            await Task.Run(() =>
                WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutStrategyFactory
                    .Create(layoutKind)
                    .Layout(doc, new WpfHexEditor.Editor.ClassDiagram.Core.Layout.LayoutOptions
                    {
                        Strategy        = layoutKind,
                        ColSpacing      = 60,
                        RowSpacing      = 80,
                        CanvasPadding   = 40,
                        MinBoxWidth     = projectOptions.DefaultNodeWidth,
                        ForceIterations = iterations,
                        SpringLength    = _options.SpringLength
                    }));
        }

        context.Output.Info($"[Class Diagram] Layout done. Opening tab…");
        _lastAnalyzedDocument = doc;

        string title = $"{projectName} [Class Diagram]";
        string uiId  = $"doc-class-diagram-{Guid.NewGuid():N}";

        var host = new ClassDiagramSplitHost();
        host.LoadDocument(doc, title);

        // Restore .whcd twin (project-level — keyed by project file path).
        string whcdPath  = WhcdSerializer.GetFolderWhcdPath(Path.GetDirectoryName(projectPath) ?? projectPath);
        var    whcdState = WhcdSerializer.Load(whcdPath);
        if (whcdState is not null)
        {
            host.ApplyWhcdState(whcdState);
            context.Output.Info($"[WHCD] Restored {whcdState.Nodes.Count} node positions.");
        }
        else if (_options.SolutionShowSwimLanesByDefault)
        {
            Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                () => host.ApplyWhcdState(new WhcdDocument { ShowSwimLanes = true }));
        }

        _openTabs[projectPath] = uiId;
        _openHosts[uiId]       = host;
        context.UIRegistry.RegisterDocumentTab(uiId, host, Id, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = projectPath,
            CanClose  = true,
        });

        // Auto-save .whcd on diagram change (debounced).
        host.DiagramChanged += (_, _) =>
        {
            _whcdSaveDebounce?.Dispose();
            _whcdSaveDebounce = new System.Threading.Timer(_ =>
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    var whcd = host.GetWhcdState(sourceFiles.ToArray());
                    WhcdSerializer.Save(whcdPath, whcd);
                }),
                null, WhcdSaveDelayMs, System.Threading.Timeout.Infinite);
        };

        context.Output.Info(
            $"[Class Diagram] Project diagram ready: {doc.Classes.Count} classes. " +
            $"Export via Tools → Export Class Diagram.");
    }

    /// <summary>
    /// Parses a single .cs or .vb source file and opens the resulting diagram in a new tab.
    /// This is also the entry point called when the user right-clicks a source file
    /// in Solution Explorer and selects "View Class Diagram".
    /// </summary>
    public async Task OpenClassDiagramForFileAsync(
        string csharpFilePath,
        IIDEHostContext context)
    {
        // Reactivate the existing tab if already open for this file.
        if (_openTabs.TryGetValue(csharpFilePath, out string? existingId))
        {
            if (context.UIRegistry.Exists(existingId))
            {
                context.UIRegistry.FocusPanel(existingId);
                return;
            }
            // Tab was closed but _openTabs wasn't purged — remove stale entry and fall through.
            _openTabs.Remove(csharpFilePath);
            _openHosts.Remove(existingId);
        }

        // Discover partial-class sibling files in the same directory
        // so that e.g. MainWindow.cs + MainWindow.Build.cs + MainWindow.Commands.cs
        // are all analysed together and their members merged into one node.
        string dir      = Path.GetDirectoryName(csharpFilePath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(csharpFilePath).Split('.')[0];
        string[] siblings = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.cs")
                .Where(f => Path.GetFileNameWithoutExtension(f)
                                .StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                .ToArray()
            : [csharpFilePath];
        string[] filesToAnalyze;
        switch (_options.PartialClassScope)
        {
            case PartialClassScopeMode.ActiveFileOnly:
                filesToAnalyze = [csharpFilePath];
                break;
            case PartialClassScopeMode.WholeDirectory:
                filesToAnalyze = Directory.Exists(dir)
                    ? Directory.GetFiles(dir, "*.cs")
                    : [csharpFilePath];
                break;
            case PartialClassScopeMode.AskWhenAmbiguous when siblings.Length > 1:
                var result = System.Windows.MessageBox.Show(
                    $"Found {siblings.Length} files starting with '{baseName}'.\n\n" +
                    "[Yes]  Include all sibling files (merges partial classes)\n" +
                    "[No]   Analyze only the active file\n" +
                    "[Cancel]  Analyze entire directory",
                    "Class Diagram — File Scope",
                    System.Windows.MessageBoxButton.YesNoCancel,
                    System.Windows.MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Cancel)
                {
                    await OpenClassDiagramForFolderAsync(dir, context);
                    return;
                }
                filesToAnalyze = result == System.Windows.MessageBoxResult.Yes ? siblings : [csharpFilePath];
                break;
            default: // AllSiblings
                filesToAnalyze = siblings.Length > 1 ? siblings : [csharpFilePath];
                break;
        }

        DiagramDocument doc = await Task.Run(() =>
            RoslynClassDiagramAnalyzer.AnalyzeFiles(filesToAnalyze, _options));
        _lastAnalyzedDocument = doc;

        string title = Path.GetFileNameWithoutExtension(csharpFilePath) + " [Class Diagram]";
        string uiId  = $"doc-class-diagram-{Guid.NewGuid():N}";

        var host = new ClassDiagramSplitHost();
        host.LoadDocument(doc, title);

        // Restore .whcd twin (positions + view). Fall back to session view snapshot.
        string whcdPath  = WhcdSerializer.GetWhcdPath(csharpFilePath);
        var    whcdState = WhcdSerializer.Load(whcdPath);
        context.Output.Info($"[WHCD] path={whcdPath}  exists={File.Exists(whcdPath)}");
        if (whcdState is not null)
        {
            context.Output.Info($"[WHCD] Restoring {whcdState.Nodes.Count} positions, zoom={whcdState.Zoom:F2}");
            host.ApplyWhcdState(whcdState);
        }
        else if (_options.RestoreLastState)
        {
            string? solutionDir = GetSolutionDir(context);
            var session = ClassDiagramSessionStateSerializer.Load(solutionDir);
            if (session is not null &&
                string.Equals(session.LastFilePath, csharpFilePath, StringComparison.OrdinalIgnoreCase))
                host.ApplyViewSnapshot(session.ViewSnapshot);
        }

        _openTabs[csharpFilePath] = uiId;
        _openHosts[uiId]          = host;
        context.UIRegistry.RegisterDocumentTab(uiId, host, Id, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = csharpFilePath,
            CanClose  = true,
        });

        // ── Save .whcd immediately on open (creates file even before any interaction) ──
        Application.Current?.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                try
                {
                    var initial = host.GetWhcdState([csharpFilePath]);
                    WhcdSerializer.Save(whcdPath, initial);
                    context.Output.Info($"[WHCD] Initial save: {initial.Nodes.Count} nodes → {whcdPath}");
                }
                catch (Exception ex) { context.Output.Info($"[WHCD] Initial save FAILED: {ex.Message}"); }
            });

        // ── Auto-save on every diagram change (DispatcherTimer debounce, UI thread) ──
        var whcdTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(WhcdSaveDelayMs)
        };
        whcdTimer.Tick += (_, _) =>
        {
            whcdTimer.Stop();
            try
            {
                var whcd = host.GetWhcdState([csharpFilePath]);
                WhcdSerializer.Save(whcdPath, whcd);
                context.Output.Info($"[WHCD] Auto-saved {whcd.Nodes.Count} nodes → {whcdPath}");
            }
            catch (Exception ex) { context.Output.Info($"[WHCD] Auto-save FAILED: {ex.Message}"); }
        };

        host.DiagramChanged += (_, _) =>
        {
            context.Output.Info("[WHCD] DiagramChanged fired");
            SaveSession(host, csharpFilePath, context);
            whcdTimer.Stop();
            whcdTimer.Start();
        };

        // Attach live-sync service for this file.
        if (_liveSyncEnabled && File.Exists(csharpFilePath))
        {
            var svc = new DiagramLiveSyncService([csharpFilePath], doc, _options);
            svc.DocumentPatched += (_, e) =>
            {
                if (_openTabs.ContainsValue(uiId))
                    host.ApplyPatch(e.Patch, e.Document);
                else
                {
                    // Tab has been closed — purge all mappings so reopen works cleanly.
                    _openTabs.Remove(csharpFilePath);
                    _openHosts.Remove(uiId);
                    svc.Dispose();
                    _liveSyncServices.Remove(uiId);
                }
            };
            _liveSyncServices[uiId] = svc;
        }
    }

    // ── Session state helpers ────────────────────────────────────────────────

    private static string? GetSolutionDir(IIDEHostContext context)
    {
        string? solutionPath = context.SolutionExplorer.ActiveSolutionPath;
        if (string.IsNullOrEmpty(solutionPath)) return null;
        return Path.GetDirectoryName(solutionPath);
    }

    private void SaveSession(ClassDiagramSplitHost host, string filePath, IIDEHostContext context)
    {
        if (!_options.RestoreLastState) return;
        var state = new ClassDiagramSessionState
        {
            LastFilePath  = filePath,
            ViewSnapshot  = host.GetViewSnapshot()
        };
        ClassDiagramSessionStateSerializer.Save(state, GetSolutionDir(context));
    }

    /// <summary>
    /// Parses all .cs files in a folder tree and opens a combined diagram.
    /// Entry point for Solution Explorer "Generate Class Diagram for Project".
    /// </summary>
    public async Task OpenClassDiagramForFolderAsync(
        string folderPath,
        IIDEHostContext context)
    {
        // Reactivate the existing tab if already open for this folder.
        if (_openTabs.TryGetValue(folderPath, out string? existingId)
            && context.UIRegistry.Exists(existingId))
        {
            context.UIRegistry.FocusPanel(existingId);
            return;
        }

        var classDiagramExtensions = LanguageRegistry.Instance.AllLanguages()
            .Where(l => l.SupportsClassDiagram)
            .SelectMany(l => l.Extensions)
            .Select(e => "*" + e)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sourceFiles = classDiagramExtensions
            .SelectMany(pattern => Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories))
            .ToArray();

        DiagramDocument doc = await Task.Run(() =>
            RoslynClassDiagramAnalyzer.AnalyzeFiles(sourceFiles, _options));
        _lastAnalyzedDocument = doc;

        string folderName = Path.GetFileName(folderPath.TrimEnd('/', '\\', Path.DirectorySeparatorChar));
        string title      = folderName + " [Class Diagram]";
        string uiId       = $"doc-class-diagram-{Guid.NewGuid():N}";

        var host = new ClassDiagramSplitHost();
        host.LoadDocument(doc, title);

        // Restore .whcd twin for folder diagram
        string folderWhcdPath  = WhcdSerializer.GetFolderWhcdPath(folderPath);
        var    folderWhcdState = WhcdSerializer.Load(folderWhcdPath);
        if (folderWhcdState is not null)
            host.ApplyWhcdState(folderWhcdState);

        _openTabs[folderPath] = uiId;
        _openHosts[uiId]      = host;
        context.UIRegistry.RegisterDocumentTab(uiId, host, Id, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = folderPath,
            CanClose  = true,
        });

        // Auto-save .whcd on diagram change (debounced)
        host.DiagramChanged += (_, _) =>
        {
            _whcdSaveDebounce?.Dispose();
            _whcdSaveDebounce = new System.Threading.Timer(_ =>
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    var whcd = host.GetWhcdState(sourceFiles);
                    WhcdSerializer.Save(folderWhcdPath, whcd);
                }),
                null, WhcdSaveDelayMs, System.Threading.Timeout.Infinite);
        };
    }

    private async Task OpenAIGeneratedDiagramAsync(IIDEHostContext context)
    {
        string? prompt = ShowInputDialog(
            "Describe the classes to generate (e.g. 'Repository pattern for User entity'):",
            "✨ AI Generate Diagram");

        if (string.IsNullOrWhiteSpace(prompt)) return;

        context.Output.Info("[Class Diagram] Sending prompt to AI…");

        using var generator = new ClassDiagramAIGenerator();
        generator.ProgressChanged += (_, msg) => context.Output.Info($"[AI Diagram] {msg}");

        DiagramDocument doc = await Task.Run(() =>
            generator.GenerateAsync(prompt, _options));

        string shortPrompt = prompt.Length > 40 ? prompt[..40] + "…" : prompt;
        string title = $"AI: {shortPrompt} [Class Diagram]";
        string uiId  = $"doc-class-diagram-ai-{Guid.NewGuid():N}";

        var host = new ClassDiagramSplitHost();
        host.LoadDocument(doc, title);

        context.UIRegistry.RegisterDocumentTab(uiId, host, Id, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = $"AI-generated class diagram: {prompt}",
            CanClose  = true,
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? ShowInputDialog(string prompt, string title, string defaultValue = "")
    {
        var tb  = new TextBox { Text = defaultValue, MinWidth = 200, Margin = new Thickness(0, 6, 0, 0) };
        tb.SelectAll();

        var ok     = new Button { Content = "OK",     IsDefault = true,  Width = 60, Margin = new Thickness(0, 0, 4, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel  = true,  Width = 60 };
        var btns   = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = prompt });
        panel.Children.Add(tb);
        panel.Children.Add(btns);

        var dlg = new Window
        {
            Title           = title,
            Content         = panel,
            SizeToContent   = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode      = ResizeMode.NoResize,
            ShowInTaskbar   = false,
            Owner           = Application.Current.MainWindow
        };

        string? result = null;
        ok.Click     += (_, _) => { result = tb.Text; dlg.DialogResult = true; };
        cancel.Click += (_, _) => { dlg.DialogResult = false; };

        dlg.Loaded += (_, _) => tb.Focus();
        return dlg.ShowDialog() == true ? result : null;
    }

    private static bool HasActiveClassDiagramSource(IIDEHostContext context)
    {
        string? path = context.FocusContext.ActiveDocument?.FilePath;
        return !string.IsNullOrEmpty(path) && IsSourceFile(path);
    }

    private void SaveWhscdState(
        ClassDiagramSplitHost host,
        DiagramDocument doc,
        string solutionPath,
        string whscdPath,
        IIDEHostContext context)
    {
        try
        {
            WhcdDocument snap = host.GetWhcdState([]);
            var state = new WhscdDocument
            {
                SolutionPath      = solutionPath,
                GeneratedAt       = DateTime.UtcNow,
                Zoom              = snap.Zoom,
                OffsetX           = snap.OffsetX,
                OffsetY           = snap.OffsetY,
                MinimapVisible    = snap.MinimapVisible,
                SelectedNodeId    = snap.SelectedNodeId,
                ShowSwimLanes     = snap.ShowSwimLanes,
                SnapToGrid        = snap.SnapToGrid,
                ViewMode          = snap.ViewMode,
                SplitLayout       = snap.SplitLayout,
                SplitColRatio     = snap.SplitColRatio,
                SplitRowRatio     = snap.SplitRowRatio,
                CollapsedSections = snap.CollapsedSections,
                Nodes             = snap.Nodes,
                ProjectGroups     = doc.ProjectGroups
                    .Select(g => new WhscdProjectGroup
                    {
                        ProjectName = g.ProjectName,
                        ProjectPath = g.ProjectPath,
                        Color       = g.Color
                    })
                    .ToList()
            };
            WhscdSerializer.Save(whscdPath, state);
            context.Output.Info(
                $"[WHSCD] Saved {state.Nodes.Count} nodes → {Path.GetFileName(whscdPath)}");
        }
        catch (Exception ex)
        {
            context.Output.Info($"[WHSCD] Save failed: {ex.Message}");
        }
    }

    /// <summary>Returns true when a project name looks like a test project.</summary>
    private static bool IsTestProject(string projectName) =>
        projectName.Contains("Test",  StringComparison.OrdinalIgnoreCase)
     || projectName.Contains("Tests", StringComparison.OrdinalIgnoreCase)
     || projectName.Contains("Spec",  StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true for files of languages that support class diagrams.</summary>
    private static bool IsSourceFile(string path)
    {
        // Primary: ask LanguageRegistry (respects whfmt SupportsClassDiagram flag).
        if (LanguageRegistry.Instance.FindByExtension(Path.GetExtension(path))?.SupportsClassDiagram == true)
            return true;
        // Fallback: accept .cs/.vb directly so files with build warnings (or an
        // unregistered language definition) are never incorrectly rejected.
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".vb", StringComparison.OrdinalIgnoreCase);
    }
}

// ==========================================================
// ISolutionExplorerContextMenuContributor implementation
// Injects "View Class Diagram" / "Generate Class Diagram…" items
// into the Solution Explorer right-click context menu.
// ==========================================================

/// <summary>
/// Contributes Class Diagram items to the Solution Explorer context menu
/// for .cs/.vb files, project nodes, and solution nodes.
/// </summary>
file sealed class ClassDiagramContextMenuContributor : ISolutionExplorerContextMenuContributor
{
    private readonly ClassDiagramPlugin  _plugin;
    private readonly IIDEHostContext     _context;

    public ClassDiagramContextMenuContributor(ClassDiagramPlugin plugin, IIDEHostContext context)
    {
        _plugin  = plugin;
        _context = context;
    }

    public IReadOnlyList<SolutionContextMenuItem> GetContextMenuItems(string nodeKind, string? nodePath)
    {
        var items = new List<SolutionContextMenuItem>();

        switch (nodeKind)
        {
            case "File":
                // Only for .cs / .vb source files
                if (nodePath is null || !IsSourceFile(nodePath)) break;
                items.Add(SolutionContextMenuItem.Item(
                    header:   "View Class Diagram",
                    command:  new RelayCommand(_ => _ = _plugin.OpenClassDiagramForFileAsync(nodePath!, _context)),
                    iconGlyph: "\uE8EC"));
                break;

            case "Project":
                if (nodePath is null) break;
                items.Add(SolutionContextMenuItem.Item(
                    header:   "Generate Class Diagram for Project",
                    command:  new RelayCommand(_ => _ = _plugin.OpenDiagramForProjectAsync(nodePath, _context)),
                    iconGlyph: "\uE8EC"));
                break;

            case "Solution":
                if (!_context.SolutionExplorer.HasActiveSolution) break;
                items.Add(SolutionContextMenuItem.Item(
                    header:   "Generate Class Diagram for Solution",
                    command:  new RelayCommand(_ => _ = OpenDiagramForSolutionAsync()),
                    iconGlyph: "\uE8EC"));
                break;
        }

        return items;
    }

    private Task OpenDiagramForSolutionAsync()
        => _plugin.OpenDiagramForSolutionAsync(_context);

    private static bool IsSourceFile(string path) =>
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".vb", StringComparison.OrdinalIgnoreCase);
}
