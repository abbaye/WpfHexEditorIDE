//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class CopyFileCommand : ITerminalCommandProvider
{
    public string CommandName => "copyfile";
    public string Description => "Copy a file from source to destination.";
    public string Usage       => "copyfile <src> <dst>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length < 2) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }

        var src = Path.IsPathRooted(args[0]) ? args[0] : Path.Combine(context.WorkingDirectory, args[0]);
        var dst = Path.IsPathRooted(args[1]) ? args[1] : Path.Combine(context.WorkingDirectory, args[1]);

        if (!File.Exists(src)) { output.WriteError($"Source not found: {src}"); return Task.FromResult(1); }

        try
        {
            File.Copy(src, dst, overwrite: true);
            output.WriteLine($"Copied: {src} → {dst}");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            output.WriteError($"Copy failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }
}
