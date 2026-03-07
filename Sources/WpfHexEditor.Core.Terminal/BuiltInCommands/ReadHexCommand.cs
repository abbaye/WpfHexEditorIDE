//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class ReadHexCommand : ITerminalCommandProvider
{
    public string CommandName => "read-hex";
    public string Description => "Display hex bytes from the active hex editor at a given offset.";
    public string Usage       => "read-hex <offset> [length]";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }

        if (!long.TryParse(args[0], out var offset))
        {
            output.WriteError($"Invalid offset: {args[0]}");
            return Task.FromResult(1);
        }

        int length = args.Length > 1 && int.TryParse(args[1], out var l) ? l : 16;
        var bytes = context.IDE.HexEditor.ReadBytes(offset, length);

        if (bytes is null || bytes.Length == 0) { output.WriteWarning("No bytes read (no active hex editor?)."); return Task.FromResult(0); }

        output.WriteLine($"0x{offset:X8}  {BitConverter.ToString(bytes).Replace("-", " ")}");
        return Task.FromResult(0);
    }
}
