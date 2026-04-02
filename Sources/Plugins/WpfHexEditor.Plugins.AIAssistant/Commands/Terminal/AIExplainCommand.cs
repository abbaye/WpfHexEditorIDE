// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AIExplainCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Terminal command /ai-explain — explains the current code selection.
// ==========================================================
using WpfHexEditor.Plugins.AIAssistant.Panel;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.AIAssistant.Commands.Terminal;

public sealed class AIExplainCommand : ITerminalCommandProvider
{
    private readonly Func<AIAssistantPanelViewModel> _getVm;
    private readonly IIDEHostContext _context;

    public string CommandName => "ai-explain";
    public string Description => "Explain the current code selection with AI Assistant";
    public string Usage => "/ai-explain";
    public string? Source => "Plugin";

    public AIExplainCommand(Func<AIAssistantPanelViewModel> getVm, IIDEHostContext context)
    {
        _getVm = getVm;
        _context = context;
    }

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct = default)
    {
        var selection = _context.CodeEditor?.GetSelectedText();
        if (string.IsNullOrWhiteSpace(selection))
        {
            output.WriteWarning("No code selected. Select code in the editor first.");
            return Task.FromResult(1);
        }

        var vm = _getVm();
        if (vm.ActiveTab is null)
            vm.CreateNewTabCommand.Execute(null);

        if (vm.ActiveTab is not null)
        {
            vm.ActiveTab.InputText = $"@selection Explain this code in detail.";
            vm.ActiveTab.SendCommand.Execute(null);
            output.WriteInfo("[Claude] Explaining selection...");
        }

        return Task.FromResult(0);
    }
}
