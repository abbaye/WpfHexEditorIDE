//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class ListFilesCommand : ITerminalCommandProvider
{
    public string CommandName => "listfiles";
    public string Description => "List files in a directory (default: working directory).";
    public string Usage       => "listfiles [path]";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        var dir = args.Length > 0
            ? (Path.IsPathRooted(args[0]) ? args[0] : Path.Combine(context.WorkingDirectory, args[0]))
            : context.WorkingDirectory;

        var files = context.IDE.SolutionExplorer.GetFilesInDirectory(dir);
        if (files.Count == 0) { output.WriteLine($"(no files in {dir})"); return Task.FromResult(0); }

        foreach (var f in files) output.WriteLine(Path.GetFileName(f));
        output.WriteLine($"--- {files.Count} file(s) ---");
        return Task.FromResult(0);
    }
}
