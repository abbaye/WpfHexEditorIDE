// ==========================================================
// Project: WpfHexEditor.Core.Scripting
// File: ScriptGlobals.cs
// Description:
//     Top-level globals injected into every script.
//     Properties declared here are accessible directly from script code
//     (no qualifier needed: e.g. `Print("hello")` or `HexEditor.WriteBytes(...)`)
// ==========================================================

using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Core.Scripting;

/// <summary>
/// Global state visible to C# scripts at the top level.
/// Script code can call <c>Print("…")</c>, access <c>HexEditor</c>, <c>Documents</c>, etc.
/// </summary>
public sealed class ScriptGlobals
{
    private readonly List<string> _outputLines = [];

    // ── Services injected by the host ─────────────────────────────────────────

    /// <summary>Hex editor service: read/write bytes, navigate, inspect selection.</summary>
    public IHexEditorService HexEditor { get; init; } = null!;

    /// <summary>Document host: open, activate, and navigate to documents in the IDE.</summary>
    public IDocumentHostService Documents { get; init; } = null!;

    /// <summary>Output panel service: write to the Output panel.</summary>
    public IOutputService Output { get; init; } = null!;

    /// <summary>Cancellation token linked to the Cancel button in the ScriptRunner panel.</summary>
    public CancellationToken CT { get; init; }

    // ── Script-facing helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Writes a line to both the script output capture buffer and the IDE Output panel.
    /// Equivalent to <c>Console.WriteLine</c> for script use.
    /// </summary>
    public void Print(object? value = null)
    {
        var text = value?.ToString() ?? string.Empty;
        _outputLines.Add(text);
        Output?.Info(text);
    }

    /// <summary>Writes formatted text (same as <see cref="Print"/> with string.Format).</summary>
    public void Printf(string format, params object?[] args) =>
        Print(string.Format(format, args));

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>Returns captured output lines (used by <see cref="RoslynScriptEngine"/> to build result).</summary>
    internal IReadOnlyList<string> CapturedLines => _outputLines;

    /// <summary>Drains the captured output as a single string.</summary>
    internal string DrainOutput() => string.Join(Environment.NewLine, _outputLines);
}
