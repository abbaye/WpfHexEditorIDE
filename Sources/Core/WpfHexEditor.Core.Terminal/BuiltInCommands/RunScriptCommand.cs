//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core.Terminal.Scripting;

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class RunScriptCommand(HxScriptEngine engine) : ITerminalCommandProvider
{
    public string CommandName => "run-script";
    public string Description => "Execute a .hxscript file.";
    public string Usage       => "run-script <path>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }

        var path = Path.IsPathRooted(args[0]) ? args[0] : Path.Combine(context.WorkingDirectory, args[0]);
        if (!File.Exists(path)) { output.WriteError($"File not found: {path}"); return 1; }

        var script = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return await engine.RunAsync(script, output, context, ct).ConfigureAwait(false);
    }
}
