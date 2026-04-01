// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/ITestRunnerService.cs
// Description:
//     Public SDK contract for the unit test runner.
//     Exposed via IDEHostContext.TestRunner (nullable — absent if the
//     UnitTesting plugin is not loaded).
//     Plugins (and terminal commands) use this to run tests programmatically.
// Architecture Notes:
//     Implemented by TestRunnerServiceAdapter inside WpfHexEditor.Plugins.UnitTesting.
//     The adapter is registered on plugin init; the App layer holds the reference.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Service for running unit tests inside the IDE.
/// Accessible to plugins and terminal commands via <c>context.TestRunner</c>.
/// </summary>
public interface ITestRunnerService
{
    /// <summary><c>true</c> when a test run is currently in progress.</summary>
    bool IsRunning { get; }

    /// <summary>Runs all tests discovered in the active solution.</summary>
    Task<TestRunSummary> RunAllAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>Runs tests for the project with the given <paramref name="projectName"/>.</summary>
    Task<TestRunSummary> RunProjectAsync(string projectName, CancellationToken ct = default);

    /// <summary>
    /// Runs tests matching <paramref name="filter"/> (dotnet test --filter syntax,
    /// e.g. <c>FullyQualifiedName~MyService</c> or <c>Category=Unit</c>).
    /// </summary>
    Task<TestRunSummary> RunFilterAsync(string filter, CancellationToken ct = default);
}

/// <summary>Summary result of a test run.</summary>
/// <param name="Pass">Number of passing tests.</param>
/// <param name="Fail">Number of failing tests.</param>
/// <param name="Skip">Number of skipped tests.</param>
/// <param name="Duration">Total wall-clock duration of the run.</param>
/// <param name="WasCancelled"><c>true</c> when the run was stopped before completion.</param>
public record TestRunSummary(int Pass, int Fail, int Skip, TimeSpan Duration, bool WasCancelled)
{
    /// <summary>Total number of tests executed (including failures and skips).</summary>
    public int Total => Pass + Fail + Skip;
}
