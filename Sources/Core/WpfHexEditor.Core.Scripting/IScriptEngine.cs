// ==========================================================
// Project: WpfHexEditor.Core.Scripting
// File: IScriptEngine.cs
// Description:
//     Core scripting engine contract.
//     Implementations compile and execute C# scripts using Roslyn.
//     The engine is stateless between runs; each call to RunAsync
//     creates an isolated script state.
// ==========================================================

namespace WpfHexEditor.Core.Scripting;

/// <summary>
/// Compiles and executes C# scripts with access to IDE services via <see cref="ScriptGlobals"/>.
/// </summary>
public interface IScriptEngine
{
    /// <summary>
    /// Compiles and runs <paramref name="code"/> asynchronously.
    /// Output written via <c>globals.Output</c> is captured in <see cref="ScriptResult.Output"/>.
    /// </summary>
    Task<ScriptResult> RunAsync(string code, ScriptGlobals globals, CancellationToken ct = default);

    /// <summary>
    /// Compiles <paramref name="code"/> without running it.
    /// Returns diagnostics (errors/warnings) without side effects.
    /// </summary>
    Task<ScriptResult> ValidateAsync(string code, CancellationToken ct = default);
}
