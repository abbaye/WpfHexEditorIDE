// ==========================================================
// Project: WpfHexEditor.App.AssemblyExplorer
// File: Commands/AsmCloseCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description: HxTerminal command — close (unload) a loaded assembly by path or name.
// ==========================================================

using System.IO;
using WpfHexEditor.App.AssemblyExplorer.Views;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.AssemblyExplorer.Commands;

internal sealed class AsmCloseCommand(AssemblyExplorerPanel panel) : PluginTerminalCommandBase
{
    public override string CommandName => "asm-close";
    public override string Description => "Unload a loaded assembly by file name or full path.";
    public override string Usage       => "asm-close <name-or-path>";

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        if (!RequireArgs(1, args, output, Usage)) return Task.FromResult(1);

        var arg   = args[0];
        var paths = panel.ViewModel.GetWorkspaceFilePaths();

        // Match by full path or by file name (case-insensitive).
        var match = paths.FirstOrDefault(p =>
            string.Equals(p, arg, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(p), arg, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            output.WriteError($"Assembly not found: {arg}");
            output.WriteWarning("Use 'asm-list' to see loaded assemblies.");
            return Task.FromResult(1);
        }

        panel.ViewModel.CloseAssembly(match);
        output.WriteInfo($"Unloaded: {Path.GetFileName(match)}");
        return Task.FromResult(0);
    }
}
