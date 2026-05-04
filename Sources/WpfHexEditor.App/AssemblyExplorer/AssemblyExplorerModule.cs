// ==========================================================
// Project: WpfHexEditor.App
// File: AssemblyExplorer/AssemblyExplorerModule.cs
// Description:
//     Internal module that wires the Assembly Explorer into the IDE.
//     Registers 3 panels (Main, Search, Diff), 4 menu items, 4 terminal
//     commands, and subscribes to HexEditor + IDEEvents for auto-analysis
//     of managed assemblies.
//
//     Replaces the former WpfHexEditor.Plugins.AssemblyExplorer plugin
//     (ADR-011). Hosted directly by MainWindow.PluginSystem after the
//     core services are ready and before the plugin loader runs.
// Architecture:
//     App layer — consumes IIDEHostContext like a plugin would, so the
//     module remains portable if it is ever re-extracted into a plugin.
//     IDecompilationLanguage registry stays open for third-party langs.
// ==========================================================

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.Core.Events.IDEEvents;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;
using WpfHexEditor.App.AssemblyExplorer.Commands;
using WpfHexEditor.App.AssemblyExplorer.Languages;
using WpfHexEditor.App.AssemblyExplorer.Options;
using WpfHexEditor.App.AssemblyExplorer.Services;
using WpfHexEditor.App.AssemblyExplorer.Views;
using WpfHexEditor.App.AssemblyExplorer.Properties;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.Editor.Core.Dialogs;

namespace WpfHexEditor.App.AssemblyExplorer;

internal sealed class AssemblyExplorerModule
{
    private const string ModuleId       = "WpfHexEditor.App.AssemblyExplorer";
    private const string PanelUiId      = "WpfHexEditor.App.AssemblyExplorer.Panel.Main";
    private const string SearchPanelUiId = "WpfHexEditor.App.AssemblyExplorer.Panel.Search";
    private const string DiffPanelUiId  = "WpfHexEditor.App.AssemblyExplorer.Panel.Diff";

    private static readonly HashSet<string> ManagedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".winmd", ".netmodule"
    };

    private AssemblyExplorerPanel?   _panel;
    private AssemblySearchPanel?     _searchPanel;
    private AssemblyDiffPanel?       _diffPanel;
    private IIDEHostContext?         _context;
    private IAssemblyAnalysisEngine? _analysisEngine;
    private AssemblyHexSyncService?  _hexSyncService;
    private IDisposable?             _subProjectItemAdded;
    private IDisposable?             _subOpenAssembly;
    private volatile bool            _isShutdown;
    private bool                     _activated;

    /// <summary>
    /// Light-weight registration. Mirrors a Dormant plugin: only menu items,
    /// terminal commands, and a few event subscriptions are wired so the user
    /// can trigger activation. The expensive bits (DecompilerBackend ctor,
    /// 3 XAML panels, ViewModels, session restore) run lazily in
    /// <see cref="EnsureActivated"/> — this matches the behaviour the plugin
    /// had with PluginActivationOnStartup=false (ADR-011 follow-up).
    /// </summary>
    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        DecompilationLanguageRegistry.Register(CSharpDecompilationLanguage.Instance);
        DecompilationLanguageRegistry.Register(new VbNetDecompilationLanguage());
        DecompilationLanguageRegistry.Register(IlDecompilationLanguage.Instance);

        // Cheap: only checks PE headers when called. Allocate now so the
        // auto-analyse handlers below have it available before EnsureActivated.
        _analysisEngine = new AssemblyAnalysisEngine();

        RegisterMenuItems(context);
        RegisterTerminalCommands(context);

        _subOpenAssembly = context.EventBus.Subscribe<OpenAssemblyInExplorerEvent>(OnOpenAssemblyRequested);
        context.HexEditor.FileOpened          += OnFileOpened;
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;
        _subProjectItemAdded = context.IDEEvents.Subscribe<ProjectItemAddedEvent>(OnProjectItemAdded);

        return Task.CompletedTask;
    }

    /// <summary>
    /// First-use activation. Builds the decompiler backend, the 3 panels and
    /// their ViewModels, and registers them with the docking adapter. Also
    /// schedules the session restore in the background. Idempotent.
    /// </summary>
    private void EnsureActivated()
    {
        if (_activated || _context is null || _analysisEngine is null || _isShutdown) return;
        _activated = true;

        var context = _context;
        var decompiler = new DecompilerService(_analysisEngine);
        var backend    = BuildDecompilerBackend(decompiler);

        _panel = new AssemblyExplorerPanel(
            _analysisEngine, backend, decompiler,
            context.HexEditor, context.DocumentHost, context.Output,
            context.EventBus, context.UIRegistry, ModuleId);
        _panel.SetContext(context);

        context.UIRegistry.RegisterPanel(
            PanelUiId, _panel, ModuleId,
            new PanelDescriptor
            {
                Title           = AssemblyExplorerResources.AsmExplorer_PanelTitle,
                DefaultDockSide = "Left",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredWidth  = 280
            });

        _searchPanel = new AssemblySearchPanel(_panel.ViewModel);
        _searchPanel.SetContext(context);
        context.UIRegistry.RegisterPanel(
            SearchPanelUiId, _searchPanel, ModuleId,
            new PanelDescriptor
            {
                Title           = AssemblyExplorerResources.AsmExplorer_SearchPanelTitle,
                DefaultDockSide = "Bottom",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredHeight = 200
            });

        _diffPanel = new AssemblyDiffPanel(_panel.ViewModel);
        _diffPanel.SetContext(context);
        context.UIRegistry.RegisterPanel(
            DiffPanelUiId, _diffPanel, ModuleId,
            new PanelDescriptor
            {
                Title           = AssemblyExplorerResources.AsmExplorer_DiffPanelTitle,
                DefaultDockSide = "Bottom",
                DefaultAutoHide = true,
                CanClose        = true,
                PreferredHeight = 250
            });

        _panel.SetDiffPanel(_diffPanel, () => context.UIRegistry.ShowPanel(DiffPanelUiId));
        _panel.SetSolutionManager(context.SolutionManager);

        // Re-register terminal commands now that the panel exists (the menu-only
        // ones registered in InitializeAsync used a null panel reference).
        UpgradeTerminalCommands(context);

        _panel.ViewModel.AssemblyLoaded   += OnAssemblyLoaded;
        _panel.ViewModel.AssemblyUnloaded += OnAssemblyUnloaded;
        _panel.ViewModel.AssemblyCleared  += OnAssemblyCleared;

        _hexSyncService = new AssemblyHexSyncService(context.HexEditor, _panel.ViewModel);

        // Restore previous workspace in the background. Faults are logged, not
        // propagated. The _isShutdown guard prevents touching a torn-down module
        // if the user quits before the restore finishes.
        _ = Task.Run(async () =>
        {
            try { await RestoreLastSessionAsync().ConfigureAwait(false); }
            catch (Exception ex)
            {
                context.Output.Write("AssemblyExplorer", $"Session restore failed: {ex.Message}");
            }
        });
    }

    private void RegisterTerminalCommands(IIDEHostContext context)
    {
        // Until EnsureActivated runs there is no panel, so the load/list/close
        // commands need a panel-aware implementation. Register a minimal stub
        // that activates on first call.
        context.Terminal.RegisterCommand(new AsmSearchCommand());
    }

    private void UpgradeTerminalCommands(IIDEHostContext context)
    {
        if (_panel is null) return;
        context.Terminal.RegisterCommand(new AsmLoadCommand(_panel));
        context.Terminal.RegisterCommand(new AsmListCommand(_panel));
        context.Terminal.RegisterCommand(new AsmCloseCommand(_panel));
    }

    public void Shutdown()
    {
        _isShutdown = true;

        if (_panel?.ViewModel is not null)
            PersistCurrentSession(_panel.ViewModel.GetWorkspaceFilePaths());

        if (_context is not null)
        {
            _context.HexEditor.FileOpened          -= OnFileOpened;
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
        }

        if (_panel?.ViewModel is not null)
        {
            _panel.ViewModel.AssemblyLoaded   -= OnAssemblyLoaded;
            _panel.ViewModel.AssemblyUnloaded -= OnAssemblyUnloaded;
            _panel.ViewModel.AssemblyCleared  -= OnAssemblyCleared;
        }

        _subProjectItemAdded?.Dispose();
        _subProjectItemAdded = null;
        _subOpenAssembly?.Dispose();
        _subOpenAssembly = null;

        if (_context is not null)
        {
            _context.Terminal.UnregisterCommand("asm-load");
            _context.Terminal.UnregisterCommand("asm-list");
            _context.Terminal.UnregisterCommand("asm-search");
            _context.Terminal.UnregisterCommand("asm-close");
        }

        _hexSyncService?.Dispose();
        _hexSyncService = null;

        _panel          = null;
        _searchPanel    = null;
        _diffPanel      = null;
        _context        = null;
        _analysisEngine = null;
    }

    private static IDecompilerBackend BuildDecompilerBackend(DecompilerService decompiler)
    {
        var skeleton = new SkeletonDecompilerBackend(decompiler);
        var opts     = AssemblyExplorerOptions.Instance;
        if (opts.DecompilerBackend != "Skeleton")
            return new IlSpyDecompilerBackend(skeleton);
        return skeleton;
    }

    private async Task RestoreLastSessionAsync()
    {
        var opts  = AssemblyExplorerOptions.Instance;
        var paths = opts.LastSessionAssemblyPaths
            .Concat(opts.LastSessionAssemblyPath is not null ? [opts.LastSessionAssemblyPath] : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Where(p => !IsHostAssembly(p))
            .ToList();

        if (paths.Count == 0 || _panel is null) return;

        var tasks = paths.Select(p => _panel.ViewModel.LoadAssemblyAsync(p)).ToArray();
        await Task.WhenAll(tasks);
    }

    // ADR-011 follow-up: never auto-analyse the IDE's own binaries on session
    // restore. Loading WpfHexEditor.App.dll (or any sibling DLL in the host's
    // bin folder) builds a tens-of-thousands-of-types tree synchronously on
    // the UI thread, which freezes the IDE for several seconds at best and
    // hangs it at worst. The user can still drag the DLL onto the panel
    // explicitly if they really want to inspect it.
    private static readonly string _hostBinDirectory =
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsHostAssembly(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            return full.StartsWith(_hostBinDirectory, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void PersistCurrentSession(IReadOnlyList<string> filePaths)
    {
        var opts = AssemblyExplorerOptions.Instance;
        opts.LastSessionAssemblyPaths = [.. filePaths];
        opts.LastSessionAssemblyPath  = filePaths.FirstOrDefault();
        opts.Save();
    }

    private void OnOpenAssemblyRequested(OpenAssemblyInExplorerEvent evt)
    {
        if (string.IsNullOrEmpty(evt.FilePath)) return;
        EnsureActivated();
        _ = _panel?.ViewModel.LoadAssemblyAsync(evt.FilePath);
        if (evt.BringToFront)
            _context?.UIRegistry.ShowPanel(PanelUiId);
    }

    private void OnProjectItemAdded(ProjectItemAddedEvent evt)
    {
        if (string.IsNullOrEmpty(evt.FilePath)) return;
        if (!ManagedExtensions.Contains(Path.GetExtension(evt.FilePath))) return;
        if (!AssemblyExplorerOptions.Instance.AutoAnalyzeOnFileOpen) return;
        if (IsHostAssembly(evt.FilePath)) return;
        if (!(_analysisEngine?.HasManagedMetadata(evt.FilePath) ?? false)) return;

        EnsureActivated();
        if (_panel?.ViewModel.IsAssemblyLoaded(evt.FilePath) ?? false) return;
        _ = _panel?.ViewModel.LoadAssemblyAsync(evt.FilePath);
    }

    private void OnFileOpened(object? sender, EventArgs e)
    {
        if (!AssemblyExplorerOptions.Instance.AutoAnalyzeOnFileOpen) return;
        var path = _context?.HexEditor.CurrentFilePath;
        if (string.IsNullOrEmpty(path) || IsHostAssembly(path)) return;
        if (!(_analysisEngine?.HasManagedMetadata(path) ?? false)) return;

        EnsureActivated();
        if (_panel?.ViewModel.IsAssemblyLoaded(path) ?? false) return;
        _ = _panel?.ViewModel.LoadAssemblyAsync(path);
    }

    private void OnActiveEditorChanged(object? sender, EventArgs e)
    {
        if (!AssemblyExplorerOptions.Instance.AutoAnalyzeOnFileOpen) return;
        var path = _context?.HexEditor.CurrentFilePath;
        if (string.IsNullOrEmpty(path) || IsHostAssembly(path)) return;
        if (!(_analysisEngine?.HasManagedMetadata(path) ?? false)) return;

        EnsureActivated();
        if (_panel?.ViewModel.IsAssemblyLoaded(path) ?? false) return;
        _ = _panel?.ViewModel.LoadAssemblyAsync(path);
    }

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
        if (_panel is not null)
            PersistCurrentSession(_panel.ViewModel.GetWorkspaceFilePaths());
    }

    private void RegisterMenuItems(IIDEHostContext context)
    {
        context.UIRegistry.RegisterMenuItem(
            $"{ModuleId}.Menu.TogglePanel", ModuleId,
            new MenuItemDescriptor
            {
                Header     = "_Assembly Explorer",
                ParentPath = "View",
                Group      = "Core IDE",
                Category   = "Core IDE",
                IconGlyph  = "",
                ToolTip    = "Show or hide the Assembly Explorer panel",
                Command    = new RelayCommand(_ =>
                {
                    EnsureActivated();
                    context.UIRegistry.TogglePanel(PanelUiId);
                })
            });

        context.UIRegistry.RegisterMenuItem(
            $"{ModuleId}.Menu.AnalyzeAssembly", ModuleId,
            new MenuItemDescriptor
            {
                Header      = "_Analyze Assembly",
                ParentPath  = "Tools",
                Group       = "AssemblyExplorer",
                IconGlyph   = "",
                GestureText = "Ctrl+Shift+A",
                ToolTip     = "Analyze the currently open file in the Assembly Explorer",
                Command     = new RelayCommand(
                    _ =>
                    {
                        EnsureActivated();
                        var path = context.HexEditor.CurrentFilePath;
                        if (!string.IsNullOrEmpty(path))
                            _ = _panel?.ViewModel.LoadAssemblyAsync(path);
                        context.UIRegistry.ShowPanel(PanelUiId);
                    },
                    _ => context.HexEditor.IsActive)
            });

        context.UIRegistry.RegisterMenuItem(
            $"{ModuleId}.Menu.SearchAssemblies", ModuleId,
            new MenuItemDescriptor
            {
                Header      = "_Search in Assemblies…",
                ParentPath  = "Tools",
                Group       = "AssemblyExplorer",
                IconGlyph   = "",
                GestureText = "Ctrl+Shift+F",
                ToolTip     = "Search types and members across all loaded assemblies",
                Command     = new RelayCommand(_ =>
                {
                    EnsureActivated();
                    context.UIRegistry.ShowPanel(SearchPanelUiId);
                })
            });

        context.UIRegistry.RegisterMenuItem(
            $"{ModuleId}.Menu.GoToToken", ModuleId,
            new MenuItemDescriptor
            {
                Header     = "Go to _Metadata Token…",
                ParentPath = "Edit",
                Group      = "AssemblyExplorer",
                IconGlyph  = "",
                ToolTip    = "Navigate to a metadata token — coming in a future release",
                Command    = new RelayCommand(
                    _ => IdeMessageBox.Show(
                        "Go to Metadata Token — Coming in a future release.",
                        AssemblyExplorerResources.AsmExplorer_PluginName,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information))
            });
    }

}
