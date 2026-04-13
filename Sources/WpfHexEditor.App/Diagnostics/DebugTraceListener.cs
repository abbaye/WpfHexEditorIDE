// ==========================================================
// Project: WpfHexEditor.App
// File: DebugTraceListener.cs
// Description: Routes System.Diagnostics.Debug.WriteLine() from any loaded
//              assembly to OutputLogger.Debug() → Output Panel "Debug" channel.
// ==========================================================

using System.Diagnostics;

namespace WpfHexEditor.App.Diagnostics;

/// <summary>
/// Intercepts all <see cref="System.Diagnostics.Debug.WriteLine"/> calls
/// (including those from plugin assemblies) and routes them to
/// <see cref="OutputLogger.Debug"/> so they appear in the Output Panel "Debug" channel.
/// </summary>
internal sealed class DebugTraceListener : TraceListener
{
    private readonly string _prefix;

    public DebugTraceListener(string prefix = "[Debug]")
    {
        _prefix = prefix;
        Name    = "WpfHexEditorDebugListener";
    }

    // Write() accumulates partial messages (no newline) — flushed by WriteLine
    private string _buffer = string.Empty;

    public override void Write(string? message)
        => _buffer += message ?? string.Empty;

    public override void WriteLine(string? message)
    {
        var full = _buffer + (message ?? string.Empty);
        _buffer = string.Empty;

        if (!string.IsNullOrWhiteSpace(full))
            OutputLogger.Debug($"{_prefix} {full}");
    }
}
