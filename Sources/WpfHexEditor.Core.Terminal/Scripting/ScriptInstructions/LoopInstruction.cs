//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.Scripting.ScriptInstructions;

/// <summary>loop &lt;n&gt; ... endloop — repeats the body n times.</summary>
public sealed class LoopInstruction(int count, IReadOnlyList<IScriptInstruction> body) : IScriptInstruction
{
    public async Task<int> ExecuteAsync(ITerminalOutput output, ITerminalContext context,
        TerminalCommandRegistry registry, CancellationToken ct)
    {
        int code = 0;
        for (int i = 0; i < count; i++)
        {
            foreach (var instr in body)
            {
                ct.ThrowIfCancellationRequested();
                code = await instr.ExecuteAsync(output, context, registry, ct).ConfigureAwait(false);
            }
        }
        return code;
    }
}
