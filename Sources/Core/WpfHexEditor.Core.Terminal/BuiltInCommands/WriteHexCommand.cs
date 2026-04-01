//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class WriteHexCommand : ITerminalCommandProvider
{
    public string CommandName => "writehex";
    public string Description => "Write hex bytes to a position in the active hex editor.";
    public string Usage       => "writehex <offset> <hexbytes…>  e.g. writehex 0x10 FF 00 AB";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length < 2) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }

        if (!TryParseOffset(args[0], out var offset))
        {
            output.WriteError($"Invalid offset: {args[0]}");
            return Task.FromResult(1);
        }

        var byteTokens = args.Skip(1)
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        var bytes = new List<byte>(byteTokens.Length);
        foreach (var token in byteTokens)
        {
            if (!byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                output.WriteError($"Invalid hex byte: {token}");
                return Task.FromResult(1);
            }
            bytes.Add(b);
        }

        context.IDE.HexEditor.WriteBytes(offset, bytes.ToArray());
        output.WriteLine($"Written {bytes.Count} byte(s) at offset 0x{offset:X}.");
        return Task.FromResult(0);
    }

    private static bool TryParseOffset(string s, out long value)
    {
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
        return long.TryParse(s, out value);
    }
}
