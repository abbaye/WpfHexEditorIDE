//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class ClearCommand : ITerminalCommandProvider
{
    public string CommandName => "clear";
    public string Description => "Clear the terminal output.";
    public string Usage       => "clear";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        output.Clear();
        return Task.FromResult(0);
    }
}
