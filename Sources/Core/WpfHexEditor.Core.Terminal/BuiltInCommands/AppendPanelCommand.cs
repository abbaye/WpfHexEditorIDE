//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class AppendPanelCommand : ITerminalCommandProvider
{
    public string CommandName => "appendpanel";
    public string Description => "Append text to an IDE panel (output, errors).";
    public string Usage       => "appendpanel <panel> <text…>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length < 2) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }

        var text = string.Join(" ", args.Skip(1));
        switch (args[0].ToLowerInvariant())
        {
            case "output":
                context.IDE.Output.Info(text);
                break;
            default:
                output.WriteWarning($"Unknown panel: '{args[0]}'. Supported: output.");
                return Task.FromResult(1);
        }
        return Task.FromResult(0);
    }
}
