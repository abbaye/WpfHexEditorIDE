// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/CleanCommand.cs
// Description: Terminal command — remove all build outputs for the solution or a project.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>clean [project-name]</c>
/// Removes all build outputs for the active solution or a single project.
/// </summary>
public sealed class CleanCommand : ITerminalCommandProvider
{
    public string CommandName => "clean";
    public string Description => "Remove all build outputs for the active solution or a specific project.";
    public string Usage       => "clean [project-name]";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var build = context.IDE.BuildSystem;
        if (build is null) { output.WriteError("Build system not available."); return 1; }
        if (build.HasActiveBuild) { output.WriteError("A build is already in progress. Use build-cancel first."); return 1; }

        output.WriteInfo(args.Length > 0 ? $"Cleaning project '{args[0]}'..." : "Cleaning solution...");

        if (args.Length > 0)
            await build.CleanProjectAsync(args[0], ct).ConfigureAwait(false);
        else
            await build.CleanSolutionAsync(ct).ConfigureAwait(false);

        output.WriteInfo("Clean completed.");
        return 0;
    }
}
