// ==========================================================
// Project: WpfHexEditor.App
// File: AssemblyExplorer/AssemblyExplorerModule.cs
// Description:
//     Internal module that wires the Assembly Explorer into the IDE.
//     Subscribes to HexEditor + IDE events for auto-analysis of managed
//     assemblies, registers terminal commands, and exposes the 3 panels
//     (Main, Search, Diff) lazily via GetPanel(contentId) for the
//     MainWindow.BuildContentForItem switch.
//
//     Replaces the former WpfHexEditor.Plugins.AssemblyExplorer plugin
//     (ADR-011). Menu items live in MainWindow.AssemblyExplorerMenu.cs;
//     this module no longer touches IUIRegistry/RegisterPanel/RegisterMenuItem.
// Architecture:
//     App layer — consumes the SDK contract types it needs (IHexEditorService,
//     IDocumentHostService, …) but does not register UI elements through the
//     SDK plugin path. The SDK is a communication contract for plugins; core
//     modules dock their panels through MainWindow's BuildContentForItem like
//     SolutionExplorer does.
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.Core.Events.IDEEvents;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;
using WpfHexEditor.App.AssemblyExplorer.Commands;
using WpfHexEditor.App.AssemblyExplorer.Languages;
using WpfHexEditor.App.AssemblyExplorer.Options;
using WpfHexEditor.App.AssemblyExplorer.Services;
using WpfHexEditor.App.AssemblyExplorer.Views;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Events;

namespace WpfHexEditor.App.AssemblyExplorer;

internal sealed class AssemblyExplorerModule
{
    public const string ContentIdMain   = "WpfHexEditor.App.AssemblyExplorer.Panel.Main";
    public const string ContentIdSearch = "WpfHexEditor.App.AssemblyExplorer.Panel.Search";
    public const string ContentIdDiff   = "WpfHexEditor.App.AssemblyExplorer.Panel.Diff";

    // Module identity for ViewModel constructors (originally a plugin id).
    private const string ModuleId = "WpfHexEditor.App.AssemblyExplorer";

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
    /// Light-weight initialisation. Wires HexEditor + IDE event subscriptions
    /// and terminal commands. Does NOT instantiate any panel — panels are
    /// built lazily by <see cref="GetPanel"/> when MainWindow's docking
    /// layout asks for the corresponding ContentId. The first heavy
    /// activation (decompiler backend, panel ViewModels) happens on the
    /// first GetPanel call.
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

        // Terminal command that does not need the panel.
        context.Terminal.RegisterCommand(new AsmSearchCommand());

        _subOpenAssembly = context.EventBus.Subscribe<OpenAssemblyInExplorerEvent>(OnOpenAssemblyRequested);
        context.HexEditor.FileOpened          += OnFileOpened;
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;
        _subProjectItemAdded = context.IDEEvents.Subscribe<ProjectItemAddedEvent>(OnProjectItemAdded);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the panel for an AssemblyExplorer ContentId. All three panels
    /// are shared singletons built once by <see cref="EnsureActivated"/> and
    /// reused across undock/redock — DockControl's internal content cache
    /// handles parent detachment correctly when the same UIElement comes back
    /// from the ContentFactory, the same way Solution Explorer does.
    /// </summary>
    public UIElement? GetPanel(string contentId)
    {
        EnsureActivated();
        if (_isShutdown) return null;

        return contentId switch
        {
            ContentIdMain   => _panel,
            ContentIdSearch => _searchPanel,
            ContentIdDiff   => _diffPanel,
            _               => null
        };
    }

    /// <summary>
    /// Instance check — returns true for AssemblyExplorer ContentIds.
    /// </summary>
    public bool IsKnownContentId(string contentId)
        => contentId == ContentIdMain || contentId == ContentIdSearch || contentId == ContentIdDiff;

    /// <summary>
    /// Static check usable before the module instance is constructed.
    /// Allows BuildContentForItem to recognise AssemblyExplorer ContentIds
    /// even when <see cref="AssemblyExplorerModule"/> is not yet initialised.
    /// </summary>
    public static bool IsKnownContentIdStatic(string contentId)
        => contentId == ContentIdMain || contentId == ContentIdSearch || contentId == ContentIdDiff;

    /// <summary>
    /// First-use activation. Builds the decompiler backend, the 3 panels
    /// and their ViewModels, and wires the persistence + hex sync. Idempotent.
    /// Synchronous but only runs once. Called from GetPanel(contentId) so the
    /// panel materialisation happens just-in-time when MainWindow asks for it.
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

        _searchPanel = new AssemblySearchPanel(_panel.ViewModel);
        _searchPanel.SetContext(context);

        _diffPanel = new AssemblyDiffPanel(_panel.ViewModel);
        _diffPanel.SetContext(context);

        _panel.SetDiffPanel(_diffPanel, () => context.UIRegistry.ShowPanel(ContentIdDiff));
        _panel.SetSolutionManager(context.SolutionManager);

        // Panel-aware terminal commands (the search-only one was registered in InitializeAsync).
        context.Terminal.RegisterCommand(new AsmLoadCommand(_panel));
        context.Terminal.RegisterCommand(new AsmListCommand(_panel));
        context.Terminal.RegisterCommand(new AsmCloseCommand(_panel));

        _panel.ViewModel.AssemblyLoaded   += OnAssemblyLoaded;
        _panel.ViewModel.AssemblyUnloaded += OnAssemblyUnloaded;
        _panel.ViewModel.AssemblyCleared  += OnAssemblyCleared;

        _hexSyncService = new AssemblyHexSyncService(context.HexEditor, _panel.ViewModel);

        // Restore previous workspace in the background. Faults are logged, not
        // propagated.
        _ = Task.Run(async () =>
        {
            try { await RestoreLastSessionAsync().ConfigureAwait(false); }
            catch (Exception ex)
            {
                context.Output.Write("AssemblyExplorer", $"Session restore failed: {ex.Message}");
            }
        });
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

    // ADR-011 follow-up: never auto-analyse the IDE's own binaries on
    // session restore. Loading WpfHexEditor.App.dll itself builds a
    // tens-of-thousands-of-types tree synchronously on the UI thread.
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
            _context?.UIRegistry.ShowPanel(ContentIdMain);
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
}
