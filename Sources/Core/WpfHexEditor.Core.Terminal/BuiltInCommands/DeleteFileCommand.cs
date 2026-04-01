//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class DeleteFileCommand : ITerminalCommandProvider
{
    public string CommandName => "deletefile";
    public string Description => "Delete a file from disk.";
    public string Usage       => "deletefile <path>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }

        var path = Path.IsPathRooted(args[0]) ? args[0] : Path.Combine(context.WorkingDirectory, args[0]);
        if (!File.Exists(path)) { output.WriteError($"File not found: {path}"); return Task.FromResult(1); }

        try
        {
            File.Delete(path);
            output.WriteLine($"Deleted: {path}");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            output.WriteError($"Delete failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }
}
