//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.Scripting.ScriptInstructions;

/// <summary>sleep &lt;ms&gt; — pauses execution for the specified milliseconds.</summary>
public sealed class SleepInstruction(int milliseconds) : IScriptInstruction
{
    public async Task<int> ExecuteAsync(ITerminalOutput output, ITerminalContext context,
        TerminalCommandRegistry registry, CancellationToken ct)
    {
        await Task.Delay(milliseconds, ct).ConfigureAwait(false);
        return 0;
    }
}
