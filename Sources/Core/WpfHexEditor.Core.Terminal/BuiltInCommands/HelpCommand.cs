//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class HelpCommand(TerminalCommandRegistry registry) : ITerminalCommandProvider
{
    public string CommandName => "help";
    public string Description => "List all available commands or show help for a specific command.";
    public string Usage       => "help [command]";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length > 0)
        {
            var cmd = registry.FindCommand(args[0]);
            if (cmd is null) { output.WriteError($"Unknown command: {args[0]}"); return Task.FromResult(1); }
            output.WriteLine($"{cmd.CommandName}  —  {cmd.Description}");
            output.WriteLine($"Usage:  {cmd.Usage}");
            output.WriteLine($"Source: {cmd.Source ?? "Built-in"}");
            return Task.FromResult(0);
        }

        var groups = registry.GetAll()
            .GroupBy(c => c.Source ?? "Built-in")
            .OrderBy(g => g.Key == "Built-in" ? 0 : g.Key == "Plugin" ? 1 : g.Key == "Script" ? 2 : 3)
            .ThenBy(g => g.Key);

        foreach (var group in groups)
        {
            output.WriteLine($"{group.Key} commands:");
            foreach (var cmd in group.OrderBy(c => c.CommandName))
                output.WriteLine($"  {cmd.CommandName,-22} {cmd.Description}");
            output.WriteLine("");
        }

        return Task.FromResult(0);
    }
}
