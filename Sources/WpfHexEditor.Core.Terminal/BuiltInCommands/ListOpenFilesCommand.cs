//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class ListOpenFilesCommand : ITerminalCommandProvider
{
    public string CommandName => "list-open";
    public string Description => "List all currently open files.";
    public string Usage       => "list-open";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        var files = context.IDE.SolutionExplorer.GetOpenFilePaths();
        if (files.Count == 0) { output.WriteLine("(no open files)"); return Task.FromResult(0); }
        foreach (var f in files) output.WriteLine($"  {f}");
        return Task.FromResult(0);
    }
}
