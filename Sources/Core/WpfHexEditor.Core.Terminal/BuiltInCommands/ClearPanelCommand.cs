//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class ClearPanelCommand : ITerminalCommandProvider
{
    public string CommandName => "clearpanel";
    public string Description => "Clear the content of a named IDE panel (output, errors, terminal).";
    public string Usage       => "clearpanel <panel>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }

        switch (args[0].ToLowerInvariant())
        {
            case "output":
                context.IDE.Output.Clear();
                output.WriteLine("Output panel cleared.");
                break;
            case "terminal":
                output.Clear();
                break;
            default:
                output.WriteWarning($"Unknown panel: '{args[0]}'. Supported: output, terminal.");
                return Task.FromResult(1);
        }
        return Task.FromResult(0);
    }
}
