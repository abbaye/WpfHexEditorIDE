// ==========================================================
// Project: WpfHexEditor.Core.Scripting
// File: RoslynScriptEngine.cs
// Description:
//     IScriptEngine implementation backed by Microsoft.CodeAnalysis.CSharp.Scripting.
//     Each call to RunAsync creates an isolated ScriptState — no shared state between runs.
//     Compilation is done inline; Roslyn handles its own caching internally.
// Architecture:
//     - Thread safety: RunAsync and ValidateAsync are safe to call from any thread.
//     - Output: captured via ScriptGlobals.Print() → ScriptResult.Output.
//     - Errors: compiler diagnostics mapped to ScriptError records.
// ==========================================================

using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace WpfHexEditor.Core.Scripting;

/// <summary>
/// Roslyn-backed C# scripting engine.
/// </summary>
public sealed class RoslynScriptEngine : IScriptEngine
{
    private static readonly ScriptOptions _options = ScriptOptions.Default
        .WithReferences(ScriptHostAssemblies.GetReferences())
        .WithImports(
            "System",
            "System.Collections.Generic",
            "System.IO",
            "System.Linq",
            "System.Text",
            "System.Threading",
            "System.Threading.Tasks",
            "WpfHexEditor.SDK.Contracts.Services");

    // ── IScriptEngine ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ScriptResult> RunAsync(
        string code, ScriptGlobals globals, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(globals);
        if (string.IsNullOrWhiteSpace(code))
            return ScriptResult.Ok(TimeSpan.Zero);

        var sw = Stopwatch.StartNew();
        try
        {
            var script = CSharpScript.Create<object?>(
                code, _options, globalsType: typeof(ScriptGlobals));

            // Compile first — surface diagnostics cleanly
            var compilation = script.Compile(ct);
            var errors = MapDiagnostics(compilation);
            if (errors.Any(e => !e.IsWarning))
                return new ScriptResult(false, string.Empty, errors, sw.Elapsed);

            await script.RunAsync(globals, ct).ConfigureAwait(false);

            return new ScriptResult(true, globals.DrainOutput(), errors, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ScriptResult.Fail("Script execution was cancelled.");
        }
        catch (CompilationErrorException cex)
        {
            return new ScriptResult(
                false, globals.DrainOutput(),
                MapDiagnostics(cex.Diagnostics), sw.Elapsed, cex);
        }
        catch (Exception ex)
        {
            return ScriptResult.Fail(
                $"Runtime error: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<ScriptResult> ValidateAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ScriptResult.Ok(TimeSpan.Zero);

        var sw = Stopwatch.StartNew();
        try
        {
            var script = CSharpScript.Create<object?>(
                code, _options, globalsType: typeof(ScriptGlobals));

            var diagnostics = await Task.Run(
                () => script.Compile(ct), ct).ConfigureAwait(false);

            var errors = MapDiagnostics(diagnostics);
            var success = !errors.Any(e => !e.IsWarning);
            return new ScriptResult(success, string.Empty, errors, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ScriptResult.Fail("Validation cancelled.");
        }
        catch (Exception ex)
        {
            return ScriptResult.Fail($"Validation error: {ex.Message}", ex);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<ScriptError> MapDiagnostics(
        IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d =>
            {
                var span = d.Location.GetLineSpan();
                return new ScriptError(
                    d.GetMessage(),
                    span.StartLinePosition.Line + 1,
                    span.StartLinePosition.Character + 1,
                    d.Severity == DiagnosticSeverity.Warning);
            })
            .ToList();
    }
}
