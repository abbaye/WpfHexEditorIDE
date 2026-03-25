// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/BuildCancelCommand.cs
// Description: Terminal command — cancel the currently running build.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>build-cancel</c>
/// Requests cancellation of the active build. No-op when no build is running.
/// </summary>
public sealed class BuildCancelCommand : ITerminalCommandProvider
{
    public string CommandName => "build-cancel";
    public string Description => "Cancel the active build. No-op when no build is in progress.";
    public string Usage       => "build-cancel";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var build = context.IDE.BuildSystem;
        if (build is null) { output.WriteError("Build system not available."); return Task.FromResult(1); }

        if (!build.HasActiveBuild)
        {
            output.WriteInfo("No build is currently running.");
            return Task.FromResult(0);
        }

        build.CancelBuild();
        output.WriteWarning("Build cancellation requested.");
        return Task.FromResult(0);
    }
}
