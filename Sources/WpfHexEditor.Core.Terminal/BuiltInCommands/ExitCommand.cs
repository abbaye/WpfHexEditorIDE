//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class ExitCommand : ITerminalCommandProvider
{
    public string CommandName => "exit";
    public string Description => "Close the terminal panel session.";
    public string Usage       => "exit";

    /// <summary>Raised when the user requests to close the terminal session.</summary>
    public event EventHandler? ExitRequested;

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(0);
    }
}
