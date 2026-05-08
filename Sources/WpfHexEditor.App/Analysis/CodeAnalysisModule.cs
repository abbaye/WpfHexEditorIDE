// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeAnalysisModule.cs
// Description: Internal module that wires the Code Analysis feature into the IDE.
//              Owns the runner, snapshot service, options service, all IDE bridges,
//              and the report document tab. Follows the DebugModule/AssemblyExplorerModule
//              pattern: Initialize(IIDEHostContext) + lazy activation.
// Architecture Notes:
//     App layer — does NOT go through the SDK plugin path.
//     Opens the report document via IDockingAdapter.AddDocumentTab.
//     Re-using the same tab on re-run (remove + re-add).
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.App.Analysis.IDE;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Analysis.Services;
using WpfHexEditor.App.Analysis.UI;
using WpfHexEditor.App.Analysis.UI.ViewModels;
using WpfHexEditor.Core.Commands;
using WpfHexEditor.Core.Options;
using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Analysis;

internal sealed class CodeAnalysisModule
{
    /// <summary>ContentId of the persistent report tab — used by MainWindow.BuildContentForItem.</summary>
    public const string ReportTabUiId = "WpfHexEditor.Analysis.ReportTab";

    /// <summary>Returns the live report pane (or null if module not yet initialized / pane not yet shown).</summary>
    internal UIElement? GetReportPane() => _reportPane;

    private IIDEHostContext?           _context;
    private IDockingAdapter?           _docking;
    private IStatusBarAdapter?         _statusBarAdapter;
    private IMenuAdapter?              _menuAdapter;
    private Dispatcher?                _dispatcher;

    private CodeAnalysisOptionsService _optionsService  = new();
    private AnalysisSnapshotService    _snapshotService = new();
    private CodeAnalysisRunner?        _runner;
    private AnalysisOutputLogger?      _logger;
    private AnalysisErrorPanelBridge?  _errorBridge;
    private AnalysisStatusBarContribution? _statusBar;
    private AnalysisToolbarContribution?   _toolbar;
    private AnalysisCommandsRegistrar?     _commands;

    private CodeAnalysisReportViewModel? _reportVm;
    private CodeAnalysisReportPane?      _reportPane;
    private CancellationTokenSource?     _cts;

    internal void Initialize(
        IIDEHostContext context,
        IDockingAdapter docking,
        IStatusBarAdapter statusBar,
        IMenuAdapter menu,
        Dispatcher dispatcher,
        ICommandRegistry commandRegistry)
    {
        _context          = context;
        _docking          = docking;
        _statusBarAdapter = statusBar;
        _menuAdapter      = menu;
        _dispatcher       = dispatcher;

        _optionsService.Load();

        _runner      = new CodeAnalysisRunner(_optionsService, _snapshotService);
        _logger      = new AnalysisOutputLogger(context.Output, _optionsService.Options.OutputVerbosity);
        _errorBridge = new AnalysisErrorPanelBridge(context.ErrorPanel);
        _statusBar   = new AnalysisStatusBarContribution(statusBar);
        _toolbar     = new AnalysisToolbarContribution(menu);

        // Register toolbar menu items
        _toolbar.Register(
            runSolution: () => RunAsync(AnalysisScope.Solution, GetSolutionPath()),
            openReport:  () => OpenReportAsync());

        // Register IDE commands (Command Palette)
        _commands = new AnalysisCommandsRegistrar(
            runSolution:   () => RunAsync(AnalysisScope.Solution, GetSolutionPath()),
            openReport:    () => OpenReportAsync(),
            clearSnapshot: ClearSnapshot);
        _commands.Register(commandRegistry);

        // Register Solution Explorer context menu contributors
        context.UIRegistry.RegisterContextMenuContributor(
            "WpfHexEditor.Analysis.SolutionMenu",
            new SolutionAnalysisContextMenuContributor(
                path => RunAsync(AnalysisScope.Solution, path)));

        context.UIRegistry.RegisterContextMenuContributor(
            "WpfHexEditor.Analysis.ProjectMenu",
            new ProjectAnalysisContextMenuContributor(
                path => RunAsync(AnalysisScope.Project, path)));

        context.UIRegistry.RegisterContextMenuContributor(
            "WpfHexEditor.Analysis.FileMenu",
            new FileAnalysisContextMenuContributor(
                path => RunAsync(AnalysisScope.File, path)));

        // Register single options page (General + Thresholds + Rules all in one scrollable page)
        OptionsPageRegistry.RegisterDynamic(
            "Code Analysis", "General",
            () =>
            {
                var page = new UI.Options.CodeAnalysisOptionsPage(_optionsService);
                page.Load(null!);
                return page;
            });

        // Show badge if enabled
        if (_optionsService.Options.ShowStatusBarBadge)
        {
            var snapshot = _snapshotService.LoadLatest();
            if (snapshot is not null)
                _statusBar.ShowScore(snapshot.Score, ScoreToGrade(snapshot.Score));
        }
    }

    // ── Run ──────────────────────────────────────────────────────────────────

    private async Task RunAsync(AnalysisScope scope, string path)
    {
        if (_runner is null || _context is null) return;

        // Cancel any in-flight run
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var opts = _optionsService.Options;

        // Update options (verbosity may have changed)
        _logger = new AnalysisOutputLogger(_context.Output, opts.OutputVerbosity);

        // path is already a directory for Solution/Project scope
        var solutionDir = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? path);
        _optionsService.SetSolutionDirectory(solutionDir);
        _snapshotService.SetSolutionDirectory(solutionDir);

        // UI: show running state
        await _dispatcher!.InvokeAsync(() =>
        {
            _statusBar?.ShowRunning();
            EnsureReportPane();
            _reportVm!.IsRunning  = true;
            _reportVm.StatusText  = "Analysis running…";
        });

        if (opts.PushToErrorPanel)
            _errorBridge?.ClearPrevious();

        var scopeLabel = scope switch
        {
            AnalysisScope.Solution => Path.GetFileName(path),
            AnalysisScope.Project  => Path.GetFileNameWithoutExtension(path),
            _                      => Path.GetFileName(path),
        };
        _context.Output.Info($"[Code Analysis] Scope path: {path}");
        _logger?.LogStart(scopeLabel);

        CodeAnalysisReport? report = null;
        try
        {
            report = await _runner.RunAsync(scope, path, _logger, ct)
                                   .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            _logger?.LogError($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            await _dispatcher!.InvokeAsync(() =>
            {
                _statusBar?.Hide();
                _reportVm!.IsRunning  = false;
                _reportVm.StatusText  = $"Analysis failed: {ex.GetType().Name}: {ex.Message}";
            });
            return;
        }

        _logger?.LogFinished(report.Score.Score, report.Score.Grade, report.TotalFiles);

        if (opts.PushToErrorPanel)
            _errorBridge?.Push(report.Diagnostics);

        await _dispatcher!.InvokeAsync(() =>
        {
            if (opts.ShowStatusBarBadge)
                _statusBar?.ShowScore(report.Score.Score, report.Score.Grade);

            EnsureReportPane();
            _reportVm!.SetReport(report);
        });
    }

    private Task OpenReportAsync()
    {
        _dispatcher?.Invoke(() =>
        {
            EnsureReportPane();
            _docking?.ShowDockablePanel(ReportTabUiId);
        });
        return Task.CompletedTask;
    }

    private void ClearSnapshot()
    {
        _snapshotService.Clear();
        _context?.Output.Info("[Code Analysis] Snapshot cleared.");
    }

    // ── Report pane ──────────────────────────────────────────────────────────

    /// <summary>
    /// Build the report pane + view-model if not already created. Used by
    /// MainWindow.RefreshModulePanels to satisfy the layout-restored tab
    /// without forcing it to the foreground.
    /// </summary>
    internal void EnsureReportPaneExists()
    {
        if (_reportPane is not null || _context is null) return;
        _reportVm   = new CodeAnalysisReportViewModel();
        _reportPane = new CodeAnalysisReportPane(_reportVm, _context.DocumentHost);
        _reportPane.SetReRunCallback(
            () => RunAsync(AnalysisScope.Solution, GetSolutionPath()));
    }

    private void EnsureReportPane()
    {
        // Create the pane + VM once; keep them across re-runs so the report data persists
        EnsureReportPaneExists();

        // Always (re-)register the tab — the user may have closed it. AddDocumentTab is
        // expected to no-op (or refresh) when a tab with the same uiId already exists.
        _docking!.AddDocumentTab(
            ReportTabUiId,
            _reportPane!,
            new DocumentDescriptor
            {
                Title   = "Code Analysis",
                ToolTip = "Code Analysis Report",
            });
        _docking.ShowDockablePanel(ReportTabUiId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string GetSolutionPath()
    {
        // 1. IDE solution open → use its directory
        var solutionFile = _context?.SolutionExplorer?.ActiveSolutionPath;
        if (!string.IsNullOrEmpty(solutionFile))
            return Path.GetDirectoryName(solutionFile) ?? AppDomain.CurrentDomain.BaseDirectory;

        // 2. Walk up from the executable to find a .sln file (dev/CI scenario)
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            var parent = Path.GetDirectoryName(dir);
            if (parent is null || parent == dir) break;
            dir = parent;
        }

        // 3. Last resort: executable directory
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static string ScoreToGrade(int score) => score switch
    {
        >= 93 => "A", >= 87 => "B+", >= 80 => "B",
        >= 70 => "C", >= 60 => "D",  _ => "F",
    };
}
