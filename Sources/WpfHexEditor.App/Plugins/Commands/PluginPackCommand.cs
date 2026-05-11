// ==========================================================
// Project: WpfHexEditor.App
// File: Plugins/Commands/PluginPackCommand.cs
// Description: HxTerminal command — package a built plugin folder
//              into a .whxplugin ZIP archive.
// Usage:  plugin-pack [--path <dir>] [--output <file>]
// ==========================================================

using System.IO;
using WpfHexEditor.App.Properties;
using WpfHexEditor.PluginHost.DevTools;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.Plugins.Commands;

internal sealed class PluginPackCommand : PluginTerminalCommandBase
{
    public override string CommandName => "plugin-pack";
    public override string Description => AppResources.PluginCmd_Pack_Description;
    public override string Usage       => AppResources.PluginCmd_Pack_Usage;

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        var pluginDir = TerminalArgs.GetFlag(args, "--path") ?? Environment.CurrentDirectory;
        if (!Directory.Exists(pluginDir))
        {
            output.WriteError($"{AppResources.PluginCmd_Pack_FolderNotFound} {pluginDir}");
            return Task.FromResult(1);
        }

        var defaultOut = Path.Combine(pluginDir, $"{Path.GetFileName(pluginDir.TrimEnd(Path.DirectorySeparatorChar))}.whxplugin");
        var outputPath = TerminalArgs.GetFlag(args, "--output") ?? defaultOut;

        var result = new WhxpluginPackager().Pack(pluginDir, outputPath);
        if (!result.Success)
        {
            output.WriteError($"{AppResources.PluginCmd_Pack_Failed} {result.Error}");
            return Task.FromResult(1);
        }

        output.WriteInfo($"{AppResources.PluginCmd_Pack_Success} {result.OutputPath}");
        return Task.FromResult(0);
    }
}
