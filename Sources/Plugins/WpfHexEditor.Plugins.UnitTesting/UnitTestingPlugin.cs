// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: UnitTestingPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Plugin entry point for the Unit Testing Panel (ADR-UT-01).
//
//     Flow:
//       1. InitializeAsync — registers dockable panel, View menu item,
//          subscribes to BuildSucceededEvent (auto-run on successful build).
//       2. RunAllAsync      — discovers test projects in the current solution,
//          runs `dotnet test --logger trx` per project, parses TRX results.
//       3. ShutdownAsync    — cancels any active run, disposes subscriptions.
//
// Architecture Notes:
//     Pattern: Observer + Facade.
//     DotnetTestRunner + TrxParser are stateless service helpers.
//     All UI mutations go through UnitTestingViewModel on the Dispatcher.
// ==========================================================

using System.IO;
using System.Windows;
using WpfHexEditor.Events.IDEEvents;
using WpfHexEditor.Plugins.UnitTesting.Models;
using WpfHexEditor.Plugins.UnitTesting.Services;
using WpfHexEditor.Plugins.UnitTesting.ViewModels;
using WpfHexEditor.Plugins.UnitTesting.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.UnitTesting;

/// <summary>
/// Unit Testing Panel plugin — discovers test projects in the loaded solution
/// and runs <c>dotnet test</c> on each, displaying results in a dockable panel.
/// </summary>
public sealed class UnitTestingPlugin : IWpfHexEditorPlugin
{
    private const string PanelUiId = "WpfHexEditor.Plugins.UnitTesting.Panel";

    private IIDEHostContext?      _context;
    private UnitTestingViewModel? _vm;
    private UnitTestingPanel?     _panel;

    private IDisposable? _subBuildSucceeded;

    private CancellationTokenSource? _runCts;
    private readonly DotnetTestRunner _runner = new();

    // ── IWpfHexEditorPlugin ─────────────────────────────────────────────────

    public string  Id      => "WpfHexEditor.Plugins.UnitTesting";
    public string  Name    => "Unit Testing";
    public Version Version => new(0, 1, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = false,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _vm      = new UnitTestingViewModel();

        // Must create UI on UI thread — InitializeAsync is called there.
        _panel = new UnitTestingPanel(_vm);
        _panel.RunAllRequested     += (_, _)    => _ = RunAllAsync();
        _panel.StopRequested       += (_, _)    => StopRun();
        _panel.RunFailedRequested  += (_, _)    => _ = RunFailedAsync();
        _panel.RunThisTestRequested += (_, row) => _ = RunThisTestAsync(row);

        // Register dockable panel.
        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Unit Testing",
                DefaultDockSide = "Bottom",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredHeight = 260,
            });

        // View menu item.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Unit Testing",
                ParentPath = "View",
                Group      = "Testing",
                IconGlyph  = "\uE9E6",
                Command    = new RelayCommand(_ => context.UIRegistry.TogglePanel(PanelUiId)),
            });

        // Auto-run on successful build.
        _subBuildSucceeded = context.IDEEvents.Subscribe<BuildSucceededEvent>(_ev =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() => { _ = RunAllAsync(); });
        });

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _subBuildSucceeded?.Dispose();
        StopRun();
        _panel = null;
        _vm    = null;
        return Task.CompletedTask;
    }

    // ── Test execution ───────────────────────────────────────────────────────

    private async Task RunAllAsync(string? testFilter = null)
    {
        if (_vm is null || _context is null) return;
        if (_vm.IsRunning) return;

        var testProjects = FindTestProjects();
        if (testProjects.Count == 0)
        {
            _vm.StatusText = "No test projects found in the current solution.";
            return;
        }

        _runCts = new CancellationTokenSource();
        var ct  = _runCts.Token;

        _vm.Reset();
        _vm.IsRunning  = true;
        _vm.StatusText = $"Running {testProjects.Count} test project(s)…";

        try
        {
            int projectsDone = 0;
            foreach (var proj in testProjects)
            {
                if (ct.IsCancellationRequested) break;

                var projName    = Path.GetFileNameWithoutExtension(proj);
                _vm.StatusText  = $"Running {projName}… ({projectsDone + 1}/{testProjects.Count})";

                // Show placeholder row while this project runs.
                TestResultRow? placeholder = null;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _vm.AddRunningPlaceholder(projName);
                    placeholder = _vm.RunningRow;
                });

                var progress = new Progress<string>(line =>
                    _context?.Output.Write("Unit Testing", line));

                try
                {
                    var results = await _runner.RunAsync(proj, testFilter, progress, ct)
                                               .ConfigureAwait(false);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _vm.RemoveRunningPlaceholder(placeholder);
                        _vm.AddResults(results);
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _context?.Output.Write("Unit Testing", $"[Error] {projName}: {ex.Message}");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        _vm.RemoveRunningPlaceholder(placeholder));
                }

                projectsDone++;
            }

            _vm.StatusText = ct.IsCancellationRequested
                ? "Run cancelled."
                : BuildSummary(_vm.PassCount, _vm.FailCount, _vm.SkipCount);
        }
        catch (OperationCanceledException)
        {
            _vm.StatusText = "Run cancelled.";
        }
        finally
        {
            _vm.IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    private Task RunFailedAsync()
    {
        if (_vm is null) return Task.CompletedTask;

        var failedFilter = string.Join("|", _vm.Results
            .Where(r => r.Outcome == Models.TestOutcome.Failed)
            .Select(r => $"FullyQualifiedName~{r.ClassName}.{r.Display}"));

        return string.IsNullOrEmpty(failedFilter)
            ? Task.CompletedTask
            : RunAllAsync(failedFilter);
    }

    private Task RunThisTestAsync(TestResultRow? row)
    {
        if (row is null || row.IsPlaceholder) return Task.CompletedTask;
        var filter = $"FullyQualifiedName~{row.ClassName}.{row.Display}";
        return RunAllAsync(filter);
    }

    private void StopRun()
    {
        _runCts?.Cancel();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all .csproj files in the loaded solution that look like test projects:
    /// references xunit, nunit, or mstest NuGet packages, or has IsTestProject=true.
    /// Falls back to all .csproj files if the solution manager is not available.
    /// </summary>
    private IReadOnlyList<string> FindTestProjects()
    {
        var sm = _context?.SolutionManager;
        if (sm?.CurrentSolution is null) return [];

        var result = new List<string>();
        foreach (var project in sm.CurrentSolution.Projects)
        {
            var path = project.ProjectFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

            if (IsTestProject(path))
                result.Add(path);
        }
        return result;
    }

    private static bool IsTestProject(string csprojPath)
    {
        try
        {
            var content = File.ReadAllText(csprojPath);
            return content.Contains("IsTestProject>true", StringComparison.OrdinalIgnoreCase)
                || content.Contains("xunit",              StringComparison.OrdinalIgnoreCase)
                || content.Contains("nunit",              StringComparison.OrdinalIgnoreCase)
                || content.Contains("MSTest",             StringComparison.OrdinalIgnoreCase)
                || content.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string BuildSummary(int pass, int fail, int skip)
    {
        var total = pass + fail + skip;
        if (total == 0) return "No tests executed.";
        var status = fail == 0 ? "✓ All passed" : $"✗ {fail} failed";
        return $"{status} — {pass} passed, {fail} failed, {skip} skipped of {total}";
    }
}
