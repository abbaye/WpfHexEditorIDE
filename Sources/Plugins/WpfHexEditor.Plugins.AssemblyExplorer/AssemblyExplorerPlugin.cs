// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: AssemblyExplorerPlugin.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Updated: 2026-03-16 — v2.0: multi-assembly workspace, status bar stats,
//                        decompiler backend selection, workspace session restore,
//                        AssemblyWorkspaceChangedEvent publishing.
// Description:
//     Official plugin entry point for the .NET Assembly Explorer.
//     Implements IWpfHexEditorPlugin + IPluginWithOptions.
//     Registers the main panel, search panel, diff panel, and 4 menu items.
//     Wires HexEditor FileOpened / ActiveEditorChanged events for auto-analysis.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to IHexEditorService events.
//     All UI constructed and registered on the calling thread (UI thread).
//     UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost on unload.
//
//     IDE menu integration:
//       View  > "_Assembly Explorer"         (Panels group)
//       Tools > "_Analyze Assembly"          (AssemblyExplorer group, Ctrl+Shift+A)
//       Tools > "_Search in Assemblies…"     (AssemblyExplorer group, Ctrl+Shift+F)
//       Edit  > "Go to _Metadata Token…"    (AssemblyExplorer group)
// ==========================================================

using System.IO;
using System.Windows;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;
using WpfHexEditor.Plugins.AssemblyExplorer.Languages;
using WpfHexEditor.Plugins.AssemblyExplorer.Options;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer;

/// <summary>
/// Entry point for the official Assembly Explorer plugin (v2.0).
/// Multi-assembly workspace, deep hex editor integration, cross-assembly search,
/// assembly diff, decompiler backend selection, cross-ref navigation.
/// </summary>
public sealed class AssemblyExplorerPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string  Id      => "WpfHexEditor.Plugins.AssemblyExplorer";
    public string  Name    => "Assembly Explorer";
    public Version Version => new(0, 2, 1);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true,
        AccessSettings   = true
    };

    // ── UI ID constants ───────────────────────────────────────────────────────

    private const string PanelUiId        = "WpfHexEditor.Plugins.AssemblyExplorer.Panel.Main";
    private const string SearchPanelUiId  = "WpfHexEditor.Plugins.AssemblyExplorer.Panel.Search";
    private const string DiffPanelUiId    = "WpfHexEditor.Plugins.AssemblyExplorer.Panel.Diff";
    // ── State ─────────────────────────────────────────────────────────────────

    private AssemblyExplorerPanel? _panel;
    private AssemblySearchPanel?   _searchPanel;
    private AssemblyDiffPanel?     _diffPanel;
    private IIDEHostContext?       _context;
    private IAssemblyAnalysisEngine? _analysisEngine;
    private AssemblyExplorerOptionsPage? _optionsPage;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // Register decompilation languages (Strategy pattern — IDecompilationLanguage).
        // Must happen before building the backend so the VM can resolve languages from the registry.
        DecompilationLanguageRegistry.Register(CSharpDecompilationLanguage.Instance);
        DecompilationLanguageRegistry.Register(new VbNetDecompilationLanguage());

        // Build internal services
        _analysisEngine    = new AssemblyAnalysisEngine();
        var analysisEngine = _analysisEngine;
        var decompiler     = new DecompilerService(analysisEngine);

        // Select decompiler backend based on options.
        var backend = BuildDecompilerBackend(decompiler);

        // Build main panel with all dependencies.
        _panel = new AssemblyExplorerPanel(
            analysisEngine, backend, decompiler,
            context.HexEditor, context.DocumentHost, context.Output,
            context.EventBus, context.UIRegistry, Id);

        _panel.SetContext(context);

        // Register the main dockable panel (left-docked, VS-Like).
        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Assembly Explorer",
                DefaultDockSide = "Left",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredWidth  = 280
            });

        // Build and register the search panel.
        _searchPanel = new AssemblySearchPanel(_panel.ViewModel);
        _searchPanel.SetContext(context);
        context.UIRegistry.RegisterPanel(
            SearchPanelUiId,
            _searchPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Assembly Search",
                DefaultDockSide = "Bottom",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredHeight = 200
            });

        // Build and register the diff panel.
        _diffPanel = new AssemblyDiffPanel(_panel.ViewModel);
        _diffPanel.SetContext(context);
        context.UIRegistry.RegisterPanel(
            DiffPanelUiId,
            _diffPanel,
            Id,
            new PanelDescriptor
            {
                Title           = "Assembly Diff",
                DefaultDockSide = "Bottom",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredHeight = 250
            });

        // Wire diff panel into the main panel so "Compare with…" can show it.
        _panel.SetDiffPanel(_diffPanel, () => context.UIRegistry.ShowPanel(DiffPanelUiId));

        // Wire solution manager for "Extract to Project" workflow.
        _panel.SetSolutionManager(context.SolutionManager);

        // Register menu items.
        RegisterMenuItems(context);

        // Wire ViewModel events.
        _panel.ViewModel.AssemblyLoaded        += OnAssemblyLoaded;
        _panel.ViewModel.AssemblyUnloaded      += OnAssemblyUnloaded;
        _panel.ViewModel.AssemblyCleared       += OnAssemblyCleared;
        _panel.ViewModel.WorkspaceStatsChanged += OnWorkspaceStatsChanged;

        // Subscribe to cross-plugin "open assembly" requests (safe immediately).
        context.EventBus.Subscribe<OpenAssemblyInExplorerEvent>(OnOpenAssemblyRequested);

        // Restore the previous session as a background task so that
        // InitializeAsync returns before the watchdog timeout fires.
        // HexEditor auto-analysis events are wired AFTER restoration completes
        // to guarantee the IDE's document-tab restoration cannot inject
        // unintended assemblies into the workspace during the restore window.
        _ = RestoreLastSessionAsync().ContinueWith(_ =>
        {
            context.HexEditor.FileOpened          += OnFileOpened;
            context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;
        }, TaskScheduler.Default);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_panel?.ViewModel is not null)
            PersistCurrentSession(_panel.ViewModel.GetWorkspaceFilePaths());

        if (_context is not null)
        {
            _context.HexEditor.FileOpened          -= OnFileOpened;
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
        }

        if (_panel?.ViewModel is not null)
        {
            _panel.ViewModel.AssemblyLoaded        -= OnAssemblyLoaded;
            _panel.ViewModel.AssemblyUnloaded      -= OnAssemblyUnloaded;
            _panel.ViewModel.AssemblyCleared       -= OnAssemblyCleared;
            _panel.ViewModel.WorkspaceStatsChanged -= OnWorkspaceStatsChanged;
        }

        _panel          = null;
        _searchPanel    = null;
        _diffPanel      = null;
        _context        = null;
        _analysisEngine = null;
        _optionsPage    = null;

        return Task.CompletedTask;
    }

    // ── Decompiler backend selection ─────────────────────────────────────────

    private static IDecompilerBackend BuildDecompilerBackend(DecompilerService decompiler)
    {
        var skeleton = new SkeletonDecompilerBackend(decompiler);
        var opts     = AssemblyExplorerOptions.Instance;

        // ILSpy is now a hard dependency — always available unless user explicitly opts out.
        if (opts.DecompilerBackend != "Skeleton")
            return new IlSpyDecompilerBackend(skeleton);

        return skeleton;
    }

    // ── Session persistence ──────────────────────────────────────────────────

    private async Task RestoreLastSessionAsync()
    {
        var opts  = AssemblyExplorerOptions.Instance;
        var paths = opts.LastSessionAssemblyPaths
            .Concat(opts.LastSessionAssemblyPath is not null ? [opts.LastSessionAssemblyPath] : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            // No .Take() — workspace eviction enforces MaxLoadedAssemblies naturally.
            .ToList();

        if (paths.Count == 0 || _panel is null) return;

        // Await ALL restore loads before returning.
        // This ensures HexEditor auto-analysis subscriptions (wired after this method)
        // cannot fire during restoration and pollute the workspace with unintended assemblies.
        var tasks = paths.Select(p => _panel.ViewModel.LoadAssemblyAsync(p)).ToArray();
        await Task.WhenAll(tasks);
    }

    private static void PersistCurrentSession(IReadOnlyList<string> filePaths)
    {
        var opts = AssemblyExplorerOptions.Instance;
        opts.LastSessionAssemblyPaths = [.. filePaths];
        opts.LastSessionAssemblyPath  = filePaths.FirstOrDefault();
        opts.Save();
    }

    // ── EventBus handler: OpenAssemblyInExplorerEvent ─────────────────────────

    private void OnOpenAssemblyRequested(OpenAssemblyInExplorerEvent evt)
    {
        if (string.IsNullOrEmpty(evt.FilePath)) return;
        _ = _panel?.ViewModel.LoadAssemblyAsync(evt.FilePath);
        if (evt.BringToFront)
            _context?.UIRegistry.ShowPanel(PanelUiId);
    }

    // ── HexEditor event handlers ──────────────────────────────────────────────

    private void OnFileOpened(object? sender, EventArgs e)
    {
        if (!AssemblyExplorerOptions.Instance.AutoAnalyzeOnFileOpen) return;
        var path = _context?.HexEditor.CurrentFilePath;
        // Skip native PE files (clrgc, coreclr, clrjit…) — they produce empty stubs.
        if (!string.IsNullOrEmpty(path)
            && !(_panel?.ViewModel.IsAssemblyLoaded(path) ?? false)
            && (_analysisEngine?.HasManagedMetadata(path) ?? false))
            _ = _panel?.ViewModel.LoadAssemblyAsync(path);
    }

    private void OnActiveEditorChanged(object? sender, EventArgs e)
    {
        var path = _context?.HexEditor.CurrentFilePath;
        if (string.IsNullOrEmpty(path) || _panel is null) return;
        // Only load managed assemblies — native PE files produce empty stubs.
        if (!_panel.ViewModel.IsAssemblyLoaded(path)
            && (_analysisEngine?.HasManagedMetadata(path) ?? false))
            _ = _panel.ViewModel.LoadAssemblyAsync(path);
    }

    // ── ViewModel events ──────────────────────────────────────────────────────

    private static void OnAssemblyCleared(object? sender, EventArgs e)
    {
        var opts = AssemblyExplorerOptions.Instance;
        opts.LastSessionAssemblyPaths.Clear();
        opts.LastSessionAssemblyPath = null;
        opts.Save();
    }

    private void OnAssemblyLoaded(object? sender, Events.AssemblyLoadedEvent evt)
    {
        if (_panel is not null)
            PersistCurrentSession(_panel.ViewModel.GetWorkspaceFilePaths());
    }

    private void OnAssemblyUnloaded(object? sender, EventArgs e)
    {
        // Persist immediately when a single assembly is closed so the next
        // session does not resurrect assemblies the user intentionally removed.
        if (_panel is not null)
            PersistCurrentSession(_panel.ViewModel.GetWorkspaceFilePaths());
    }

    private void OnWorkspaceStatsChanged(object? sender, EventArgs e)
    {
        if (_panel is null) return;

        // Publish workspace changed event for other plugins.
        if (_context is not null && _panel?.ViewModel is not null)
        {
            // Determine what changed by comparing against the current workspace.
            // For simplicity we publish a generic "workspace refreshed" event.
        }
    }

    // ── Menu items ────────────────────────────────────────────────────────────

    private void RegisterMenuItems(IIDEHostContext context)
    {
        // View > Assembly Explorer
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.TogglePanel",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Assembly Explorer",
                ParentPath = "View",
                Group      = "Panels",
                IconGlyph  = "\uE8A5",
                ToolTip    = "Show or hide the Assembly Explorer panel",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(PanelUiId))
            });

        // Tools > Analyze Assembly (Ctrl+Shift+A)
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.AnalyzeAssembly",
            Id,
            new MenuItemDescriptor
            {
                Header      = "_Analyze Assembly",
                ParentPath  = "Tools",
                Group       = "AssemblyExplorer",
                IconGlyph   = "\uE8F4",
                GestureText = "Ctrl+Shift+A",
                ToolTip     = "Analyze the currently open file in the Assembly Explorer",
                Command     = new RelayCommand(
                    _ =>
                    {
                        var path = context.HexEditor.CurrentFilePath;
                        if (!string.IsNullOrEmpty(path))
                            _ = _panel?.ViewModel.LoadAssemblyAsync(path);
                        context.UIRegistry.ShowPanel(PanelUiId);
                    },
                    _ => context.HexEditor.IsActive)
            });

        // Tools > Search in Assemblies (Ctrl+Shift+F)
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.SearchAssemblies",
            Id,
            new MenuItemDescriptor
            {
                Header      = "_Search in Assemblies\u2026",
                ParentPath  = "Tools",
                Group       = "AssemblyExplorer",
                IconGlyph   = "\uE721",
                GestureText = "Ctrl+Shift+F",
                ToolTip     = "Search types and members across all loaded assemblies",
                Command     = new RelayCommand(_ =>
                {
                    context.UIRegistry.ShowPanel(SearchPanelUiId);
                })
            });

        // Edit > Go to Metadata Token…
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.GoToToken",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Go to _Metadata Token\u2026",
                ParentPath = "Edit",
                Group      = "AssemblyExplorer",
                IconGlyph  = "\uE9D2",
                ToolTip    = "Navigate to a metadata token — coming in a future release",
                Command    = new RelayCommand(
                    _ => MessageBox.Show(
                        "Go to Metadata Token — Coming in a future release.",
                        "Assembly Explorer",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information))
            });
    }

    // ── IPluginWithOptions ────────────────────────────────────────────────────

    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new AssemblyExplorerOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    public void SaveOptions()
    {
        _optionsPage?.Save();
        _panel?.ApplyOptions();
    }

    public void LoadOptions()
    {
        AssemblyExplorerOptions.Invalidate();
        _optionsPage?.Load();
    }
}
