// ==========================================================
// Project: WpfHexEditor.App.AssemblyExplorer
// File: Commands/AsmLoadCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — load a .NET assembly into the Assembly Explorer.
// ==========================================================

using System.IO;
using WpfHexEditor.App.AssemblyExplorer.Views;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.AssemblyExplorer.Commands;

internal sealed class AsmLoadCommand(AssemblyExplorerPanel panel) : PluginTerminalCommandBase
{
    public override string CommandName => "asm-load";
    public override string Description => "Load a .NET assembly into the Assembly Explorer.";
    public override string Usage       => "asm-load <path>";

    protected override async Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        if (!RequireArgs(1, args, output, Usage)) return 1;

        var path = args[0];
        if (!File.Exists(path)) { output.WriteError($"File not found: {path}"); return 1; }

        output.WriteInfo($"Loading {Path.GetFileName(path)}…");
        await panel.ViewModel.LoadAssemblyAsync(path, ct).ConfigureAwait(false);
        output.WriteInfo($"Loaded: {Path.GetFileName(path)}");
        ctx.IDE().UIRegistry.ShowPanel("WpfHexEditor.App.AssemblyExplorer.Panel.Main");
        return 0;
    }
}
