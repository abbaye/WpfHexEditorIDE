// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AINewTabCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Terminal command /ai-new-tab — creates a new conversation tab.
// ==========================================================
using WpfHexEditor.Plugins.AIAssistant.Panel;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.AIAssistant.Commands.Terminal;

public sealed class AINewTabCommand : ITerminalCommandProvider
{
    private readonly Func<AIAssistantPanelViewModel> _getVm;

    public string CommandName => "ai-new-tab";
    public string Description => "Open a new AI conversation tab";
    public string Usage => "/ai-new-tab";
    public string? Source => "Plugin";

    public AINewTabCommand(Func<AIAssistantPanelViewModel> getVm) => _getVm = getVm;

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct = default)
    {
        _getVm().CreateNewTabCommand.Execute(null);
        output.WriteInfo("[Claude] New conversation tab created.");
        return Task.FromResult(0);
    }
}
