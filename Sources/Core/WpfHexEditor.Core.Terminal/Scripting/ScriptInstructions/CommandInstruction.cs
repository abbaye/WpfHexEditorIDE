//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.Scripting.ScriptInstructions;

/// <summary>
/// A single command invocation instruction.
/// </summary>
public sealed class CommandInstruction(string commandName, string[] args) : IScriptInstruction
{
    public async Task<int> ExecuteAsync(ITerminalOutput output, ITerminalContext context,
        TerminalCommandRegistry registry, CancellationToken ct)
    {
        var cmd = registry.FindCommand(commandName);
        if (cmd is null) { output.WriteError($"Unknown command: {commandName}"); return 1; }
        return await cmd.ExecuteAsync(args, output, context, ct).ConfigureAwait(false);
    }
}
