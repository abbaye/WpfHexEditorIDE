// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/TestRunFilterCommand.cs
// Description: Terminal command — run tests matching a dotnet test --filter expression.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>test-run-filter &lt;filter&gt;</c>
/// Runs tests matching a <c>dotnet test --filter</c> expression, e.g.
/// <c>FullyQualifiedName~MyService</c> or <c>Category=Unit</c>.
/// </summary>
public sealed class TestRunFilterCommand : ITerminalCommandProvider
{
    public string CommandName => "test-run-filter";
    public string Description => "Run tests matching a dotnet test --filter expression.";
    public string Usage       => "test-run-filter <filter>  (e.g. FullyQualifiedName~MyClass)";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }

        var runner = context.IDE.TestRunner;
        if (runner is null) { output.WriteError("Unit testing service not available (plugin not loaded)."); return 1; }
        if (runner.IsRunning) { output.WriteError("A test run is already in progress."); return 1; }

        var filter = string.Join(" ", args);
        output.WriteInfo($"Running tests with filter: {filter}");
        var summary = await runner.RunFilterAsync(filter, ct).ConfigureAwait(false);
        return TestRunCommand.PrintSummary(summary, output);
    }
}
