// ==========================================================
// Project: WpfHexEditor.App.AssemblyExplorer
// File: Commands/AsmListCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — list loaded assemblies in the workspace.
// ==========================================================

using System.IO;
using WpfHexEditor.App.AssemblyExplorer.Views;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.AssemblyExplorer.Commands;

internal sealed class AsmListCommand(AssemblyExplorerPanel panel) : PluginTerminalCommandBase
{
    public override string CommandName => "asm-list";
    public override string Description => "List all assemblies currently loaded in the Assembly Explorer.";
    public override string Usage       => "asm-list";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var paths = panel.ViewModel.GetWorkspaceFilePaths();

        if (paths.Count == 0)
        {
            output.WriteWarning("No assemblies loaded. Use 'asm-load <path>' to load one.");
            return Task.FromResult(0);
        }

        output.WriteInfo($"{paths.Count} assembl{(paths.Count == 1 ? "y" : "ies")} loaded:");
        foreach (var p in paths)
            output.WriteLine($"  {Path.GetFileName(p),-40} {p}");

        return Task.FromResult(0);
    }
}
