// ==========================================================
// Project: WpfHexEditor.App
// File: Plugins/Commands/PluginNewCommand.cs
// Description: HxTerminal command — scaffold a new WpfHexEditor plugin
//              skeleton into the target folder.
// Usage:  plugin-new <name> [author] [--path <dir>]
// ==========================================================

using System.IO;
using WpfHexEditor.App.Properties;
using WpfHexEditor.PluginHost.DevTools;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.App.Plugins.Commands;

internal sealed class PluginNewCommand : PluginTerminalCommandBase
{
    public override string CommandName => "plugin-new";
    public override string Description => AppResources.PluginCmd_New_Description;
    public override string Usage       => AppResources.PluginCmd_New_Usage;

    protected override Task<int> ExecuteCoreAsync(
        string[] args, ITerminalOutput output, ITerminalContext ctx, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            output.WriteError($"{AppResources.PluginCmd_New_NameRequired} Usage: {Usage}");
            return Task.FromResult(1);
        }

        var name   = args[0];
        var author = TerminalArgs.GetFlag(args, "--author")
                     ?? (args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null);
        var path   = TerminalArgs.GetFlag(args, "--path") ?? Environment.CurrentDirectory;

        if (!Directory.Exists(path))
        {
            output.WriteError($"{AppResources.PluginCmd_New_FolderNotFound} {path}");
            return Task.FromResult(1);
        }

        var result = new PluginTemplateScaffolder().Create(path, name, author);
        if (!result.Success)
        {
            output.WriteError($"{AppResources.PluginCmd_New_Failed} {result.Error}");
            return Task.FromResult(1);
        }

        output.WriteInfo($"{AppResources.PluginCmd_New_Success} {result.PluginDirectory}");
        output.WriteInfo(AppResources.PluginCmd_New_NextStep);
        return Task.FromResult(0);
    }
}
