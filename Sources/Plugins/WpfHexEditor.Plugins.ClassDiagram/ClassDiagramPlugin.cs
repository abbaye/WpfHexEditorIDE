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
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.ClassDiagram.Controls;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.Serializer;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;
using WpfHexEditor.Plugins.ClassDiagram.Analysis;
using WpfHexEditor.Plugins.ClassDiagram.Options;
using WpfHexEditor.Plugins.ClassDiagram.Panels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;
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
    public string  Name    => "Class Diagram";
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

    // Live-sync services keyed by uiId; disposed when the plugin unloads.
    private readonly Dictionary<string, DiagramLiveSyncService> _liveSyncServices =
        new(StringComparer.Ordinal);

    private bool _liveSyncEnabled = true;

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
                Title           = "Class Outline",
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

        // Seed panels immediately for any document open at plugin-load time.
        SeedFromCurrentDocument(context);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
            _context.FocusContext.FocusChanged -= OnFocusChanged;

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

    public string GetOptionsCategory()     => "Editors";
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

        // History panel — bind to the host's undo manager.
        if (_historyPanel is not null)
        {
            _historyPanel.ViewModel.SetManager(host.UndoManager);
            _historyPanel.ViewModel.JumpRequested += OnHistoryJumpRequested;
        }

        // Wire search panel result selection → canvas selection.
        if (_searchPanel is not null)
            _searchPanel.ViewModel.ResultSelected += OnSearchResultSelected;

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

        if (_historyPanel is not null)
        {
            _historyPanel.ViewModel.JumpRequested -= OnHistoryJumpRequested;
            _historyPanel.ViewModel.SetManager(null!); // Clear to avoid stale reference.
        }

        if (_searchPanel is not null)
            _searchPanel.ViewModel.ResultSelected -= OnSearchResultSelected;

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
        string? filePath = e.Member?.SourceFilePath ?? e.Node.SourceFilePath;
        int     line     = e.Member?.SourceLineOneBased > 0 ? e.Member.SourceLineOneBased
                         : e.Node.SourceLineOneBased;

        if (string.IsNullOrEmpty(filePath) || line <= 0 || _context is null)
        {
            _context?.Output.Warning("[Class Diagram] No source location for navigation.");
            return;
        }

        // Open the file in the IDE and navigate to the declaration line.
        _context.DocumentHost.ActivateAndNavigateTo(filePath, line, 1);
        _context.Output.Info($"[Class Diagram] Navigate → {Path.GetFileName(filePath)}:{line}");
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

        // Navigate the canvas to the selected class node.
        // The canvas exposes SelectByNode via SelectedClassChanged event round-trip.
        _propertiesPanel?.ViewModel.SetSelection(result.Node, result.Member);
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
            $"{Id}.Menu.Tools.GenerateProject",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Generate Class Diagram for Solution Files",
                ParentPath = "Tools",
                IconGlyph  = "\uE8A5",
                ToolTip    = "Analyze all C# files in the active solution and open a combined class diagram",
                Command    = new RelayCommand(
                    execute: _ => _ = OpenDiagramForSolutionAsync(context),
                    canExecute: _ => context.SolutionExplorer.HasActiveSolution),
                Group = "ClassDiagram"
            });
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
    /// Called from the "Generate Class Diagram for Solution Files" menu item.
    /// Analyzes all .cs files in the active solution and opens a combined diagram.
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

        string[] csFiles = context.SolutionExplorer.GetSolutionFilePaths()
            .Where(f => IsSourceFile(f))
            .ToArray();

        if (csFiles.Length == 0)
        {
            context.Output.Info("[Class Diagram] No C# or VB.NET files found in the active solution.");
            return;
        }

        context.Output.Info($"[Class Diagram] Analyzing {csFiles.Length} source files…");

        DiagramDocument doc = await Task.Run(() =>
            RoslynClassDiagramAnalyzer.AnalyzeFiles(csFiles, _options));

        string title = "Solution [Class Diagram]";
        string uiId  = $"doc-class-diagram-{Guid.NewGuid():N}";

        var host = new ClassDiagramSplitHost();
        host.LoadDocument(doc, title);

        _openTabs[key] = uiId;
        context.UIRegistry.RegisterDocumentTab(uiId, host, Id, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = "Class diagram for all solution source files",
            CanClose  = true,
        });

        context.Output.Info(
            $"[Class Diagram] Generated diagram with {doc.Classes.Count} classes " +
            $"and {doc.Relationships.Count} relationships.");
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
        if (_openTabs.TryGetValue(csharpFilePath, out string? existingId)
            && context.UIRegistry.Exists(existingId))
        {
            context.UIRegistry.FocusPanel(existingId);
            return;
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
        string[] filesToAnalyze = siblings.Length > 1 ? siblings : [csharpFilePath];

        DiagramDocument doc = await Task.Run(() =>
            RoslynClassDiagramAnalyzer.AnalyzeFiles(filesToAnalyze, _options));

        string title = Path.GetFileNameWithoutExtension(csharpFilePath) + " [Class Diagram]";
        string uiId  = $"doc-class-diagram-{Guid.NewGuid():N}";

        var host = new ClassDiagramSplitHost();
        host.LoadDocument(doc, title);

        _openTabs[csharpFilePath] = uiId;
        context.UIRegistry.RegisterDocumentTab(uiId, host, Id, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = csharpFilePath,
            CanClose  = true,
        });

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
                    // Tab has been closed — dispose silently.
                    svc.Dispose();
                    _liveSyncServices.Remove(uiId);
                }
            };
            _liveSyncServices[uiId] = svc;
        }
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

        string folderName = Path.GetFileName(folderPath.TrimEnd('/', '\\', Path.DirectorySeparatorChar));
        string title      = folderName + " [Class Diagram]";
        string uiId       = $"doc-class-diagram-{Guid.NewGuid():N}";

        var host = new ClassDiagramSplitHost();
        host.LoadDocument(doc, title);

        _openTabs[folderPath] = uiId;
        context.UIRegistry.RegisterDocumentTab(uiId, host, Id, new DocumentDescriptor
        {
            Title     = title,
            ContentId = uiId,
            ToolTip   = folderPath,
            CanClose  = true,
        });
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

    /// <summary>Returns true for files of languages that support class diagrams.</summary>
    private static bool IsSourceFile(string path) =>
        LanguageRegistry.Instance.FindByExtension(Path.GetExtension(path))?.SupportsClassDiagram == true;
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
                var projectDir = Path.GetDirectoryName(nodePath);
                if (projectDir is null) break;
                items.Add(SolutionContextMenuItem.Item(
                    header:   "Generate Class Diagram for Project",
                    command:  new RelayCommand(_ => _ = _plugin.OpenClassDiagramForFolderAsync(projectDir, _context)),
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
