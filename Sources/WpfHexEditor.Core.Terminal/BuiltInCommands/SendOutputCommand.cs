//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class SendOutputCommand : ITerminalCommandProvider
{
    public string CommandName => "send-output";
    public string Description => "Send a message to the Output panel.";
    public string Usage       => "send-output <message...>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        var message = string.Join(" ", args);
        context.IDE.Output.Info(message);
        output.WriteLine($"[→ Output] {message}");
        return Task.FromResult(0);
    }
}
