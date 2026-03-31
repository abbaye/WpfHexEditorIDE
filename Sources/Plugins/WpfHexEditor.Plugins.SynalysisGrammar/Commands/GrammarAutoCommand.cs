// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: Commands/GrammarAutoCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — toggle or set auto-apply for grammars.
// ==========================================================

using WpfHexEditor.Plugins.SynalysisGrammar.Options;
using WpfHexEditor.Plugins.SynalysisGrammar.ViewModels;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.SynalysisGrammar.Commands;

internal sealed class GrammarAutoCommand(GrammarSelectorViewModel viewModel) : PluginTerminalCommandBase
{
    public override string CommandName => "grammar-auto";
    public override string Description => "Enable, disable, or toggle auto-apply of grammars on file open.";
    public override string Usage       => "grammar-auto [on|off|toggle]";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var action = args.Length > 0 ? args[0].ToLowerInvariant() : "toggle";

        bool newValue = action switch
        {
            "on"     => true,
            "off"    => false,
            "toggle" => !viewModel.IsAutoApply,
            _        => !viewModel.IsAutoApply
        };

        viewModel.IsAutoApply              = newValue;
        GrammarExplorerOptions.Instance.AutoApplyOnFileOpen = newValue;
        GrammarExplorerOptions.Instance.Save();

        output.WriteInfo($"Grammar auto-apply: {(newValue ? "ON" : "OFF")}");
        return Task.FromResult(0);
    }
}
