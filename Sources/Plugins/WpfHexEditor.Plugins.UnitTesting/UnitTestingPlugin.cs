// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: UnitTestingPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Updated: 2026-03-24 (ADR-UT-07 — VS-style TreeView + toolbar pills)
// Description:
//     Plugin entry point for the Unit Testing Panel (ADR-UT-01 → ADR-UT-07).
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
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Plugins.UnitTesting.Models;
using WpfHexEditor.Plugins.UnitTesting.Options;
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
public sealed class UnitTestingPlugin : IWpfHexEditorPlugin, IPluginWithOptions
{
    private const string PanelUiId = "WpfHexEditor.Plugins.UnitTesting.Panel";

    private IIDEHostContext?      _context;
    private UnitTestingViewModel? _vm;
    private UnitTestingPanel?     _panel;

    private UnitTestingOptionsPage? _optionsPage;
    private IDisposable? _subBuildSucceeded;
    private IDisposable? _subWorkspaceChanged;

    private CancellationTokenSource? _runCts;
    private readonly DotnetTestRunner     _runner     = new();
    private readonly DotnetTestDiscoverer _discoverer = new();

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
        _panel.RunAllRequested      += (_, _)    => _ = RunAllAsync();
        _panel.StopRequested        += (_, _)    => StopRun();
        _panel.RunFailedRequested   += (_, _)    => _ = RunFailedAsync();
        _panel.RunThisTestRequested += async (_, row)  => await RunSingleTestAsync(row);
        _panel.GoToSourceRequested  += (_, row)  => NavigateToSource(row);

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

        // Auto-run on successful build (respects AutoRunOnBuild option).
        _subBuildSucceeded = context.IDEEvents.Subscribe<BuildSucceededEvent>(_ev =>
        {
            if (UnitTestingOptions.Instance.AutoRunOnBuild)
                Application.Current?.Dispatcher.InvokeAsync(() => { _ = RunAllAsync(); });
        });

        // Auto-discover projects when solution changes.
        _subWorkspaceChanged = context.IDEEvents.Subscribe<WorkspaceChangedEvent>(_ev =>
        {
            _ = DiscoverProjectsAsync();
        });

        // Wire Refresh button → re-discover only (no test run).
        _panel.RefreshProjectsRequested += (_, _) => _ = DiscoverProjectsAsync();

        // Discover immediately if a solution is already loaded.
        if (_context?.SolutionManager?.CurrentSolution is not null)
            _ = DiscoverProjectsAsync();

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _subBuildSucceeded?.Dispose();
        _subWorkspaceChanged?.Dispose();
        StopRun();
        _panel       = null;
        _vm          = null;
        _optionsPage = null;
        return Task.CompletedTask;
    }

    // ── IPluginWithOptions ───────────────────────────────────────────────────

    public System.Windows.FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new UnitTestingOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    public void SaveOptions()
    {
        _optionsPage?.Save();
        _vm?.ApplyOptions();
    }

    public void LoadOptions()
    {
        UnitTestingOptions.Invalidate();
        _optionsPage?.Load();
    }

    public string GetOptionsCategory()     => "Testing";
    public string GetOptionsCategoryIcon() => "\uE9E6"; // Segoe MDL2: BulletedList

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

                // Mark project node as running while tests are in progress.
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    _vm.AddRunningPlaceholder(projName));

                var progress = new Progress<string>(line =>
                    _context?.Output.Write("Unit Testing", line));

                try
                {
                    var rawResults = await _runner.RunAsync(proj, testFilter, progress, ct)
                                                  .ConfigureAwait(false);
                    var results = EnrichWithSourcePaths(proj, rawResults);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _vm.RemoveRunningPlaceholder();
                        _vm.AddResults(results);

                        if (UnitTestingOptions.Instance.AutoExpandDetailOnFailure
                            && _vm.SelectedResult is null)
                        {
                            _vm.SelectedResult = _vm.AllLeafResults
                                .FirstOrDefault(r => r.Outcome == TestOutcome.Failed);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // Ensure project node spinner stops even when the run is cancelled.
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        _vm.RemoveRunningPlaceholder());
                    throw; // propagate so outer catch sets StatusText = "Run cancelled."
                }
                catch (Exception ex)
                {
                    _context?.Output.Write("Unit Testing", $"[Error] {projName}: {ex.Message}");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        _vm.RemoveRunningPlaceholder());
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
            // Dispatch to the UI thread — PropertyChanged from a background thread
            // can be silently dropped under load, leaving the ProgressBar stuck visible.
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_vm is not null)
                    _vm.IsRunning = false;
            });
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    private Task RunFailedAsync()
    {
        if (_vm is null) return Task.CompletedTask;

        var failedFilter = string.Join("|", _vm.AllLeafResults
            .Where(r => r.Outcome == Models.TestOutcome.Failed)
            .Select(r => $"FullyQualifiedName~{r.ClassName}.{r.Display}"));

        return string.IsNullOrEmpty(failedFilter)
            ? Task.CompletedTask
            : RunAllAsync(failedFilter);
    }

    private async Task RunSingleTestAsync(TestResultRow? row)
    {
        if (row is null || _vm is null || _context is null) return;
        if (_vm.IsRunning) return;

        // Find which csproj owns this test row.
        var testProjects = FindTestProjects();
        string? projPath = null;
        foreach (var proj in _vm.ProjectNodes)
        {
            if (proj.Classes.Any(c => c.Tests.Contains(row)))
            {
                projPath = testProjects.FirstOrDefault(p =>
                    string.Equals(Path.GetFileNameWithoutExtension(p), proj.ProjectName,
                                  StringComparison.OrdinalIgnoreCase));
                break;
            }
        }
        if (projPath is null) return;

        _runCts = new CancellationTokenSource();
        var ct   = _runCts.Token;

        _vm.IsRunning  = true;
        _vm.StatusText = $"Running {row.Display}…";

        await Application.Current.Dispatcher.InvokeAsync(() => row.IsRunning = true);

        try
        {
            var filter   = $"FullyQualifiedName~{row.ClassName}.{row.Display}";
            var progress = new Progress<string>(line =>
                _context?.Output.Write("Unit Testing", line));

            var rawResults = await _runner.RunAsync(projPath, filter, progress, ct)
                                          .ConfigureAwait(false);
            var results = EnrichWithSourcePaths(projPath, rawResults);

            var match = results.FirstOrDefault(r => r.TestName == row.Display);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                row.IsRunning = false;
                if (match is not null)
                    _vm.ReplaceResult(row, match);
            });

            _vm.StatusText = ct.IsCancellationRequested
                ? "Run cancelled."
                : BuildSummary(_vm.PassCount, _vm.FailCount, _vm.SkipCount);
        }
        catch (OperationCanceledException)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => row.IsRunning = false);
            _vm.StatusText = "Run cancelled.";
        }
        catch (Exception ex)
        {
            _context?.Output.Write("Unit Testing", $"[Error] {ex.Message}");
            await Application.Current.Dispatcher.InvokeAsync(() => row.IsRunning = false);
            _vm.StatusText = "Run failed — see Output panel.";
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_vm is not null)
                    _vm.IsRunning = false;
            });
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    private void StopRun()
    {
        _runCts?.Cancel();
    }

    // ── Discovery ────────────────────────────────────────────────────────────

    private async Task DiscoverProjectsAsync()
    {
        if (_vm is null) return;
        var projects = FindTestProjects();

        // Add project nodes immediately (synchronous, fast).
        var names = projects.Select(p => Path.GetFileNameWithoutExtension(p)!);
        await Application.Current.Dispatcher.InvokeAsync(() => _vm.DiscoverProjects(names));

        // Remove stale NotRun rows so Refresh always reflects current source.
        await Application.Current.Dispatcher.InvokeAsync(() => _vm.ClearNotRunTests());

        // Discover individual tests per project in the background.
        foreach (var proj in projects)
        {
            try
            {
                var discovered = await _discoverer.DiscoverAsync(proj).ConfigureAwait(false);
                if (discovered.Count == 0) continue;
                await Application.Current.Dispatcher.InvokeAsync(
                    () => _vm.AddDiscoveredTests(discovered));
            }
            catch
            {
                // Non-fatal — project might not be built yet; user can still run tests.
            }
        }
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

    private void NavigateToSource(TestResultRow? row)
    {
        if (row?.SourceFile is not string path) return;
        if (row.SourceLine > 0)
            _context?.DocumentHost.ActivateAndNavigateTo(path, row.SourceLine, column: 1);
        else
            _context?.DocumentHost.OpenDocument(path);
    }

    /// <summary>
    /// For tests whose <see cref="TestResult.SourceFile"/> is null (e.g. passing tests with no
    /// stack trace), searches the project directory for a <c>.cs</c> file matching the class name.
    /// </summary>
    private static IReadOnlyList<TestResult> EnrichWithSourcePaths(
        string projFilePath, IReadOnlyList<TestResult> results)
    {
        var projDir  = Path.GetDirectoryName(projFilePath) ?? string.Empty;
        var fileCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var enriched  = new List<TestResult>(results.Count);

        foreach (var r in results)
        {
            if (r.SourceFile is not null)
            {
                enriched.Add(r);
                continue;
            }

            var shortClass = r.ClassName.Contains('.')
                ? r.ClassName[(r.ClassName.LastIndexOf('.') + 1)..]
                : r.ClassName;

            if (!fileCache.TryGetValue(shortClass, out var found))
            {
                found = FindCsFile(projDir, shortClass);
                fileCache[shortClass] = found;
            }

            enriched.Add(found is not null ? r with { SourceFile = found } : r);
        }

        return enriched;
    }

    private static string? FindCsFile(string dir, string className)
    {
        try
        {
            return Directory
                .EnumerateFiles(dir, $"{className}.cs", SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    private static string BuildSummary(int pass, int fail, int skip)
    {
        var total = pass + fail + skip;
        if (total == 0) return "No tests executed.";
        var status = fail == 0 ? "✓ All passed" : $"✗ {fail} failed";
        return $"{status} — {pass} passed, {fail} failed, {skip} skipped of {total}";
    }
}
