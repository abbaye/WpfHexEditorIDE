// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Services/TestRunnerServiceAdapter.cs
// Description:
//     Adapter exposing UnitTesting run capabilities as ITestRunnerService
//     so that terminal commands and external plugins can trigger test runs
//     without a direct reference to this plugin.
// Architecture Notes:
//     Registered into IExtensionRegistry by UnitTestingPlugin.InitializeAsync.
//     Resolved by IDEHostContext.TestRunner via GetExtensions<ITestRunnerService>().
//     Uses DotnetTestRunner directly (same engine as the panel).
//     SolutionManager access is captured once at construction time.
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.UnitTesting.Services;

/// <summary>
/// Implements <see cref="ITestRunnerService"/> by delegating to <see cref="DotnetTestRunner"/>
/// and using <see cref="ISolutionManager"/> to locate test projects.
/// </summary>
public sealed class TestRunnerServiceAdapter : ITestRunnerService
{
    private readonly DotnetTestRunner _runner;
    private readonly ISolutionManager? _solutionManager;
    private int _runningFlag; // 0 = idle, 1 = running (interlocked)

    public TestRunnerServiceAdapter(DotnetTestRunner runner, ISolutionManager? solutionManager)
    {
        _runner          = runner ?? throw new ArgumentNullException(nameof(runner));
        _solutionManager = solutionManager;
    }

    /// <inheritdoc />
    public bool IsRunning => System.Threading.Interlocked.CompareExchange(ref _runningFlag, 0, 0) == 1;

    /// <inheritdoc />
    public async Task<TestRunSummary> RunAllAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        => await RunCoreAsync(null, null, progress, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<TestRunSummary> RunProjectAsync(string projectName, CancellationToken ct = default)
        => await RunCoreAsync(projectName, null, null, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<TestRunSummary> RunFilterAsync(string filter, CancellationToken ct = default)
        => await RunCoreAsync(null, filter, null, ct).ConfigureAwait(false);

    // ── Core run logic ──────────────────────────────────────────────────────

    private async Task<TestRunSummary> RunCoreAsync(
        string? projectNameFilter,
        string? testFilter,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (System.Threading.Interlocked.Exchange(ref _runningFlag, 1) == 1)
            return new TestRunSummary(0, 0, 0, TimeSpan.Zero, WasCancelled: false);

        int pass = 0, fail = 0, skip = 0;
        var sw   = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var projects = FindTestProjects(projectNameFilter);
            if (projects.Count == 0)
            {
                progress?.Report("No test projects found.");
                return new TestRunSummary(0, 0, 0, TimeSpan.Zero, WasCancelled: false);
            }

            foreach (var proj in projects)
            {
                if (ct.IsCancellationRequested) break;

                var results = await _runner.RunAsync(proj, testFilter, progress, ct)
                                           .ConfigureAwait(false);
                foreach (var r in results)
                {
                    switch (r.Outcome)
                    {
                        case Models.TestOutcome.Passed:  pass++; break;
                        case Models.TestOutcome.Failed:  fail++; break;
                        default:                         skip++; break;
                    }
                }
            }
        }
        finally
        {
            sw.Stop();
            System.Threading.Interlocked.Exchange(ref _runningFlag, 0);
        }

        return new TestRunSummary(pass, fail, skip, sw.Elapsed, ct.IsCancellationRequested);
    }

    // ── Project discovery (mirrors UnitTestingPlugin.FindTestProjects) ───────

    private IReadOnlyList<string> FindTestProjects(string? nameFilter = null)
    {
        if (_solutionManager?.CurrentSolution is null) return [];

        var result = new List<string>();
        foreach (var project in _solutionManager.CurrentSolution.Projects)
        {
            var path = project.ProjectFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
            if (!IsTestProject(path)) continue;

            if (nameFilter is not null &&
                !Path.GetFileNameWithoutExtension(path)
                     .Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(path);
        }
        return result;
    }

    private static bool IsTestProject(string csprojPath)
    {
        try
        {
            var content = File.ReadAllText(csprojPath);
            return content.Contains("IsTestProject>true",          StringComparison.OrdinalIgnoreCase)
                || content.Contains("xunit",                       StringComparison.OrdinalIgnoreCase)
                || content.Contains("nunit",                       StringComparison.OrdinalIgnoreCase)
                || content.Contains("MSTest",                      StringComparison.OrdinalIgnoreCase)
                || content.Contains("Microsoft.NET.Test.Sdk",      StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
