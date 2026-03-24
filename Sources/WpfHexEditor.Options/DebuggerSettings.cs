// ==========================================================
// Project: WpfHexEditor.Options
// File: DebuggerSettings.cs
// Description:
//     User preferences for the integrated debugger.
//     Persisted as part of AppSettings.Debugger.
// ==========================================================

namespace WpfHexEditor.Options;

/// <summary>Persisted breakpoint entry (file + line).</summary>
public sealed class PersistedBreakpoint
{
    public string FilePath  { get; set; } = string.Empty;
    public int    Line      { get; set; }
    public string Condition { get; set; } = string.Empty;
    public bool   IsEnabled { get; set; } = true;
}

/// <summary>
/// Integrated debugger user preferences.
/// Stored under AppSettings.Debugger and serialised to JSON with the rest of the settings.
/// </summary>
public sealed class DebuggerSettings
{
    // ── Adapter ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Override path to the debug adapter executable (netcoredbg / vsdbg).
    /// Empty = auto-detect via DebugAdapterLocator.
    /// </summary>
    public string NetCoreDbgPath { get; set; } = string.Empty;

    // ── Launch defaults ───────────────────────────────────────────────────────

    /// <summary>When true, the debuggee halts at its entry point (Program.Main).</summary>
    public bool StopAtEntry { get; set; } = false;

    /// <summary>When true, return values are shown in the Locals panel after a step.</summary>
    public bool ShowReturnValues { get; set; } = true;

    // ── Breakpoint persistence ────────────────────────────────────────────────

    /// <summary>Breakpoints saved between IDE sessions.</summary>
    public List<PersistedBreakpoint> Breakpoints { get; set; } = [];
}
