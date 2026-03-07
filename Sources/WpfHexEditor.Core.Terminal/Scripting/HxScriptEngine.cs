//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal.Scripting;

/// <summary>
/// Executes .hxscript programs line by line, asynchronously and cancellably.
/// Delegates parsing to <see cref="HxScriptParser"/> and instruction dispatch
/// to <see cref="TerminalCommandRegistry"/>.
/// </summary>
public sealed class HxScriptEngine(TerminalCommandRegistry registry)
{
    private readonly HxScriptParser _parser = new();

    public async Task<int> RunAsync(
        string source,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default)
    {
        var instructions = _parser.Parse(source);
        int lastCode = 0;

        foreach (var instruction in instructions)
        {
            ct.ThrowIfCancellationRequested();
            lastCode = await instruction.ExecuteAsync(output, context, registry, ct).ConfigureAwait(false);
        }

        return lastCode;
    }
}
