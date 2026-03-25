// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/TestRunProjectCommand.cs
// Description: Terminal command — run all tests in a specific project.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>test-run-project &lt;project-name&gt;</c>
/// Runs all tests for the project whose name contains the given string.
/// </summary>
public sealed class TestRunProjectCommand : ITerminalCommandProvider
{
    public string CommandName => "test-run-project";
    public string Description => "Run all tests in a specific project (partial name match).";
    public string Usage       => "test-run-project <project-name>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }

        var runner = context.IDE.TestRunner;
        if (runner is null) { output.WriteError("Unit testing service not available (plugin not loaded)."); return 1; }
        if (runner.IsRunning) { output.WriteError("A test run is already in progress."); return 1; }

        output.WriteInfo($"Running tests for project matching '{args[0]}'...");
        var summary = await runner.RunProjectAsync(args[0], ct).ConfigureAwait(false);
        return TestRunCommand.PrintSummary(summary, output);
    }
}
