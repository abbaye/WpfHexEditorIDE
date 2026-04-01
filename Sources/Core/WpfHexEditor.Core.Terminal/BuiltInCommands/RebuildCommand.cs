// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/RebuildCommand.cs
// Description: Terminal command — clean + rebuild the active solution or a specific project.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>rebuild [project-name]</c>
/// Cleans then builds the active solution or a single project.
/// </summary>
public sealed class RebuildCommand : ITerminalCommandProvider
{
    public string CommandName => "rebuild";
    public string Description => "Clean and rebuild the active solution or a specific project.";
    public string Usage       => "rebuild [project-name]";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var build = context.IDE.BuildSystem;
        if (build is null) { output.WriteError("Build system not available."); return 1; }
        if (build.HasActiveBuild) { output.WriteError("A build is already in progress. Use build-cancel first."); return 1; }

        output.WriteInfo(args.Length > 0 ? $"Rebuilding project '{args[0]}'..." : "Rebuilding solution...");

        var result = args.Length > 0
            ? await build.RebuildProjectAsync(args[0], ct).ConfigureAwait(false)
            : await build.RebuildSolutionAsync(ct).ConfigureAwait(false);

        return BuildCommand.PrintBuildResult(result, output);
    }
}
