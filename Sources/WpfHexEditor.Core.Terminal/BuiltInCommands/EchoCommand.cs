//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class EchoCommand : ITerminalCommandProvider
{
    public string CommandName => "echo";
    public string Description => "Print text to the terminal.";
    public string Usage       => "echo <text...>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        output.WriteLine(string.Join(" ", args));
        return Task.FromResult(0);
    }
}
