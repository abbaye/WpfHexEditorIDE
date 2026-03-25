// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/TestRunCommand.cs
// Description: Terminal command — run all tests in the active solution.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>test-run</c>
/// Runs all unit tests found in the active solution.
/// </summary>
public sealed class TestRunCommand : ITerminalCommandProvider
{
    public string CommandName => "test-run";
    public string Description => "Run all unit tests in the active solution.";
    public string Usage       => "test-run";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var runner = context.IDE.TestRunner;
        if (runner is null) { output.WriteError("Unit testing service not available (plugin not loaded)."); return 1; }
        if (runner.IsRunning) { output.WriteError("A test run is already in progress."); return 1; }

        output.WriteInfo("Running all tests...");
        var progress = new Progress<string>(line => output.WriteLine($"  {line}"));
        var summary  = await runner.RunAllAsync(progress, ct).ConfigureAwait(false);
        return PrintSummary(summary, output);
    }

    internal static int PrintSummary(SDK.Contracts.Services.TestRunSummary s, ITerminalOutput output)
    {
        if (s.WasCancelled)
        {
            output.WriteWarning($"Run cancelled — {s.Pass} passed, {s.Fail} failed, {s.Skip} skipped.");
            return 1;
        }
        if (s.Fail == 0)
            output.WriteInfo($"✓ All {s.Total} test(s) passed in {s.Duration.TotalSeconds:F1}s.");
        else
            output.WriteError($"✗ {s.Fail} failed, {s.Pass} passed, {s.Skip} skipped ({s.Total} total) in {s.Duration.TotalSeconds:F1}s.");
        return s.Fail == 0 ? 0 : 1;
    }
}
