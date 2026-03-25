// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/BuildCommand.cs
// Description: Terminal command — build the active solution or a specific project.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>build [project-name]</c>
/// Builds the active solution, or a single project when a name is supplied.
/// </summary>
public sealed class BuildCommand : ITerminalCommandProvider
{
    public string CommandName => "build";
    public string Description => "Build the active solution or a specific project.";
    public string Usage       => "build [project-name]";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var build = context.IDE.BuildSystem;
        if (build is null) { output.WriteError("Build system not available."); return 1; }
        if (build.HasActiveBuild) { output.WriteError("A build is already in progress. Use build-cancel first."); return 1; }

        output.WriteInfo(args.Length > 0 ? $"Building project '{args[0]}'..." : "Building solution...");

        BuildResult result = args.Length > 0
            ? await build.BuildProjectAsync(args[0], ct).ConfigureAwait(false)
            : await build.BuildSolutionAsync(ct).ConfigureAwait(false);

        return PrintBuildResult(result, output);
    }

    internal static int PrintBuildResult(BuildResult result, ITerminalOutput output)
    {
        if (result.IsSuccess)
        {
            output.WriteInfo($"Build succeeded in {result.Duration.TotalSeconds:F1}s" +
                             (result.Warnings.Count > 0 ? $" — {result.Warnings.Count} warning(s)" : "."));
        }
        else
        {
            output.WriteError($"Build FAILED in {result.Duration.TotalSeconds:F1}s — {result.Errors.Count} error(s), {result.Warnings.Count} warning(s).");
            foreach (var e in result.Errors.Take(10))
            {
                var location = e.FilePath is not null
                    ? $"{System.IO.Path.GetFileName(e.FilePath)}({e.Line}): "
                    : string.Empty;
                output.WriteError($"  [{e.Code}] {location}{e.Message}");
            }
            if (result.Errors.Count > 10)
                output.WriteError($"  ... and {result.Errors.Count - 10} more error(s). See Error List panel.");
        }
        return result.IsSuccess ? 0 : 1;
    }
}
