// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/TestStatusCommand.cs
// Description: Terminal command — show the current unit test runner status.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>test-status</c>
/// Shows whether a test run is currently in progress.
/// </summary>
public sealed class TestStatusCommand : ITerminalCommandProvider
{
    public string CommandName => "test-status";
    public string Description => "Show whether a test run is currently in progress.";
    public string Usage       => "test-status";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var runner = context.IDE.TestRunner;
        if (runner is null)
        {
            output.WriteWarning("Unit testing service not available (UnitTesting plugin not loaded).");
            return Task.FromResult(0);
        }

        output.WriteInfo(runner.IsRunning ? "Tests: RUNNING" : "Tests: IDLE");
        return Task.FromResult(0);
    }
}
