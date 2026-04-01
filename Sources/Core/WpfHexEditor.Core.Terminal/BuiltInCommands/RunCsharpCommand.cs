// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: BuiltInCommands/RunCsharpCommand.cs
// Description: Terminal command — compile and run a C# script via IScriptingService.
// Architecture Notes:
//     Uses context.IDE.Scripting (nullable).
//     --inline "<code>" flag allows single-line code without a file.
// ==========================================================

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Terminal command: <c>run-csharp &lt;file.cs&gt;</c> or <c>run-csharp --inline "&lt;code&gt;"</c>
/// Compiles and runs a C# script using the Roslyn scripting engine.
/// Scripts have access to: <c>HexEditor</c>, <c>Documents</c>, <c>Output</c>, <c>Print()</c>.
/// </summary>
public sealed class RunCsharpCommand : ITerminalCommandProvider
{
    public string CommandName => "run-csharp";
    public string Description => "Compile and run a C# script. Use --inline \"code\" for one-liners.";
    public string Usage       => "run-csharp <file.cs>  |  run-csharp --inline \"<code>\"";

    public async Task<int> ExecuteAsync(string[] args, ITerminalOutput output,
        ITerminalContext context, CancellationToken ct)
    {
        var scripting = context.IDE.Scripting;
        if (scripting is null)
        {
            output.WriteError("Scripting service not available.");
            return 1;
        }

        if (args.Length == 0) { output.WriteError("Usage: " + Usage); return 1; }

        string code;

        if (args[0].Equals("--inline", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2) { output.WriteError("Usage: run-csharp --inline \"<code>\""); return 1; }
            code = string.Join(" ", args.Skip(1));
        }
        else
        {
            var path = System.IO.Path.IsPathRooted(args[0])
                ? args[0]
                : System.IO.Path.Combine(context.WorkingDirectory, args[0]);

            if (!System.IO.File.Exists(path))
            {
                output.WriteError($"Script file not found: {path}");
                return 1;
            }

            code = await System.IO.File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }

        var result = await scripting.RunAsync(code, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(result.Output))
            output.WriteLine(result.Output.TrimEnd());

        if (result.HasErrors)
        {
            output.WriteError($"Script failed ({result.Duration.TotalMilliseconds:F0}ms).");
            foreach (var d in result.Diagnostics.Where(d => !d.IsWarning))
                output.WriteError($"  ({d.Line},{d.Column}): {d.Message}");
            return result.Success ? 0 : 1;
        }

        output.WriteInfo($"Script completed in {result.Duration.TotalMilliseconds:F0}ms.");
        return 0;
    }
}
