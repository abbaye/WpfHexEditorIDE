//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class OpenFileCommand : ITerminalCommandProvider
{
    public string CommandName => "open";
    public string Description => "Open a file in the active editor.";
    public string Usage       => "open <path>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }
        var path = Path.IsPathRooted(args[0]) ? args[0] : Path.Combine(context.WorkingDirectory, args[0]);
        if (!File.Exists(path)) { output.WriteError($"File not found: {path}"); return 1; }
        await context.IDE.SolutionExplorer.OpenFileAsync(path, ct).ConfigureAwait(false);
        output.WriteLine($"Opened: {path}");
        return 0;
    }
}
