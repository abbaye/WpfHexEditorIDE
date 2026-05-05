// ==========================================================
// Project: WpfHexEditor.App.AssemblyExplorer
// File: Commands/AsmSearchCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — open the Assembly Search panel.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.AssemblyExplorer.Commands;

internal sealed class AsmSearchCommand : PluginTerminalCommandBase
{
    private const string SearchPanelUiId = "WpfHexEditor.App.AssemblyExplorer.Panel.Search";

    public override string CommandName => "asm-search";
    public override string Description => "Open the Assembly Search panel to find types and members.";
    public override string Usage       => "asm-search [term]";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        ctx.IDE().UIRegistry.ShowPanel(SearchPanelUiId);

        if (args.Length > 0)
            output.WriteInfo($"Search panel opened — type '{string.Join(" ", args)}' to filter results.");
        else
            output.WriteInfo("Assembly Search panel opened.");

        return Task.FromResult(0);
    }
}
