//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.Scripting.ScriptInstructions;

/// <summary>
/// if &lt;exitcode&gt; ... else ... endif — branches on the exit code of the preceding instruction.
/// </summary>
public sealed class IfInstruction(
    int expectedCode,
    IReadOnlyList<IScriptInstruction> thenBranch,
    IReadOnlyList<IScriptInstruction> elseBranch,
    int lastExitCode) : IScriptInstruction
{
    public async Task<int> ExecuteAsync(ITerminalOutput output, ITerminalContext context,
        TerminalCommandRegistry registry, CancellationToken ct)
    {
        var branch = lastExitCode == expectedCode ? thenBranch : elseBranch;
        int code = 0;
        foreach (var instr in branch)
        {
            ct.ThrowIfCancellationRequested();
            code = await instr.ExecuteAsync(output, context, registry, ct).ConfigureAwait(false);
        }
        return code;
    }
}
