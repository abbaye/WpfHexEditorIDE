// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/BuildStatusCommand.cs
// Description: Terminal command — show current build system status and recent errors.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>build-status</c>
/// Displays the current build system state and up to the last 5 errors.
/// </summary>
public sealed class BuildStatusCommand : ITerminalCommandProvider
{
    public string CommandName => "build-status";
    public string Description => "Show the current build status and recent errors.";
    public string Usage       => "build-status";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var build = context.IDE.BuildSystem;
        if (build is null)
        {
            output.WriteWarning("Build system not available (no solution loaded or plugin disabled).");
            return Task.FromResult(0);
        }

        output.WriteInfo(build.HasActiveBuild ? "Build: RUNNING" : "Build: IDLE");

        var errors = context.IDE.ErrorPanel.GetRecentErrors(5);
        if (errors.Count == 0)
        {
            output.WriteLine("No recent errors.");
        }
        else
        {
            output.WriteWarning($"Last {errors.Count} error(s):");
            foreach (var e in errors)
                output.WriteError($"  {e}");
        }

        return Task.FromResult(0);
    }
}
