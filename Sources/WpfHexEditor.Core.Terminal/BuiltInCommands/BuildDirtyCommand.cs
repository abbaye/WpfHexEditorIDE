// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/BuildDirtyCommand.cs
// Description: Terminal command — incremental build of only modified projects.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>build-dirty</c>
/// Builds only the projects that have changed since the last successful build,
/// plus their transitive dependents. Falls back to a full build when all are dirty.
/// </summary>
public sealed class BuildDirtyCommand : ITerminalCommandProvider
{
    public string CommandName => "build-dirty";
    public string Description => "Incrementally build only projects modified since the last successful build.";
    public string Usage       => "build-dirty";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var build = context.IDE.BuildSystem;
        if (build is null) { output.WriteError("Build system not available."); return 1; }
        if (build.HasActiveBuild) { output.WriteError("A build is already in progress. Use build-cancel first."); return 1; }

        output.WriteInfo("Building dirty projects...");
        var result = await build.BuildDirtyAsync(ct).ConfigureAwait(false);
        return BuildCommand.PrintBuildResult(result, output);
    }
}
