//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.Scripting.ScriptInstructions;

/// <summary>
/// A single executable instruction in a parsed .hxscript program.
/// </summary>
public interface IScriptInstruction
{
    /// <summary>
    /// Executes this instruction asynchronously.
    /// </summary>
    /// <param name="output">Terminal output sink.</param>
    /// <param name="context">Execution context.</param>
    /// <param name="registry">Command registry for dispatching commands.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    Task<int> ExecuteAsync(
        ITerminalOutput output,
        ITerminalContext context,
        TerminalCommandRegistry registry,
        CancellationToken ct);
}
