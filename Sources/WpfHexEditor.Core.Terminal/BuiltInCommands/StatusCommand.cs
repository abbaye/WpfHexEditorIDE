//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class StatusCommand : ITerminalCommandProvider
{
    public string CommandName => "status";
    public string Description => "Show current IDE state (active document, working dir, etc.).";
    public string Usage       => "status";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        output.WriteLine($"Working directory : {context.WorkingDirectory}");
        output.WriteLine($"Active document   : {context.ActiveDocument?.FilePath ?? "(none)"}");
        output.WriteLine($"Active panel      : {context.ActivePanel?.PanelId ?? "(none)"}");
        return Task.FromResult(0);
    }
}
