//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class SendErrorCommand : ITerminalCommandProvider
{
    public string CommandName => "send-error";
    public string Description => "Send an error message to the Error panel.";
    public string Usage       => "send-error <message...>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        var message = string.Join(" ", args);
        context.IDE.ErrorPanel.PostDiagnostic(WpfHexEditor.SDK.Contracts.Services.DiagnosticSeverity.Error, message, "Terminal");
        output.WriteWarning($"[→ ErrorPanel] {message}");
        return Task.FromResult(0);
    }
}
