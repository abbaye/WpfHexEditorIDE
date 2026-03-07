//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class SearchCommand : ITerminalCommandProvider
{
    public string CommandName => "search";
    public string Description => "Search for a hex pattern or text in the active hex editor.";
    public string Usage       => "search <hex|text> <value>";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        if (args.Length < 2) { output.WriteError("Usage: " + Usage); return Task.FromResult(1); }

        var mode  = args[0].ToLowerInvariant();
        var value = args[1];

        var results = mode switch
        {
            "hex"  => context.IDE.HexEditor.SearchHex(value),
            "text" => context.IDE.HexEditor.SearchText(value),
            _      => null
        };

        if (results is null) { output.WriteError($"Unknown mode '{mode}'. Use 'hex' or 'text'."); return Task.FromResult(1); }
        if (results.Count == 0) { output.WriteLine("No matches found."); return Task.FromResult(0); }

        foreach (var offset in results)
            output.WriteLine($"  Match at 0x{offset:X8}");

        output.WriteLine($"Total: {results.Count} match(es).");
        return Task.FromResult(0);
    }
}
