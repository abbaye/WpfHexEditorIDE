// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AIAskCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Terminal command /ai-ask — sends a question to the active conversation tab.
// ==========================================================
using WpfHexEditor.Plugins.AIAssistant.Panel;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.AIAssistant.Commands.Terminal;

public sealed class AIAskCommand : ITerminalCommandProvider
{
    private readonly Func<AIAssistantPanelViewModel> _getVm;

    public string CommandName => "ai-ask";
    public string Description => "Send a question to AI Assistant";
    public string Usage => "/ai-ask <your question>";
    public string? Source => "Plugin";

    public AIAskCommand(Func<AIAssistantPanelViewModel> getVm) => _getVm = getVm;

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            output.WriteError("Usage: /ai-ask <question>");
            return Task.FromResult(1);
        }

        var question = string.Join(' ', args);
        var vm = _getVm();

        if (vm.ActiveTab is null)
            vm.CreateNewTabCommand.Execute(null);

        if (vm.ActiveTab is not null)
        {
            vm.ActiveTab.InputText = question;
            vm.ActiveTab.SendCommand.Execute(null);
            output.WriteInfo($"[Claude] Question sent: {question[..Math.Min(60, question.Length)]}...");
        }

        return Task.FromResult(0);
    }
}
