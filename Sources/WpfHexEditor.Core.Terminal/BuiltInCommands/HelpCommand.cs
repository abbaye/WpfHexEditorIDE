//////////////////////////////////////////////
// Apache 2.0  - 2026
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
            output.WriteLine($"Usage: {cmd.Usage}");
            return Task.FromResult(0);
        }

        output.WriteLine("Available commands:");
        foreach (var cmd in registry.GetAll())
            output.WriteLine($"  {cmd.CommandName,-20} {cmd.Description}");
        return Task.FromResult(0);
    }
}
