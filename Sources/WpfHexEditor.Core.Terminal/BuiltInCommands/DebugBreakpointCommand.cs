// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/DebugBreakpointCommand.cs
// Description: Terminal command — toggle a breakpoint at a given file and line.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>debug-breakpoint &lt;file&gt; &lt;line&gt;</c>
/// Toggles a breakpoint at the specified location.
/// </summary>
public sealed class DebugBreakpointCommand : ITerminalCommandProvider
{
    public string CommandName => "debug-breakpoint";
    public string Description => "Toggle a breakpoint at the specified file and line number.";
    public string Usage       => "debug-breakpoint <file-path> <line>";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var dbg = context.IDE.Debugger;
        if (dbg is null) { output.WriteError("Debugger not available."); return 1; }

        if (args.Length < 2)
        {
            output.WriteError("Usage: " + Usage);
            return 1;
        }

        var filePath = System.IO.Path.IsPathRooted(args[0])
            ? args[0]
            : System.IO.Path.Combine(context.WorkingDirectory, args[0]);

        if (!int.TryParse(args[1], out int line) || line < 1)
        {
            output.WriteError($"Invalid line number: '{args[1]}'");
            return 1;
        }

        bool isNowSet = await dbg.ToggleBreakpointAsync(filePath, line).ConfigureAwait(false);
        output.WriteInfo(isNowSet
            ? $"Breakpoint SET at {System.IO.Path.GetFileName(filePath)}:{line}"
            : $"Breakpoint REMOVED from {System.IO.Path.GetFileName(filePath)}:{line}");
        return 0;
    }
}
