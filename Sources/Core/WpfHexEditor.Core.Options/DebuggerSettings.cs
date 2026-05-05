// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: DebuggerSettings.cs
// Description:
//     User preferences for the integrated debugger.
//     Persisted as part of AppSettings.Debugger.
// ==========================================================

namespace WpfHexEditor.Core.Options;

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

    // ── VS Breakpoint Interop ──────────────────────────────────────────────

    /// <summary>Auto-import VS breakpoints on first solution open (when .whide/breakpoints.json is empty).</summary>
    public bool AutoImportVsBreakpoints { get; set; } = false;

    /// <summary>Auto-export breakpoints to VS XML alongside .whide on save.</summary>
    public bool AutoExportVsXml { get; set; } = false;

    /// <summary>Export path relative to solution dir (default: .whide/breakpoints-vs.xml).</summary>
    public string VsExportRelativePath { get; set; } = ".whide/breakpoints-vs.xml";
    // ── Gutter highlights ─────────────────────────────────────────────────────

    /// <summary>When true, the entire source line is highlighted when a breakpoint is hit.</summary>
    public bool BreakpointLineHighlightEnabled { get; set; } = true;

    // ── Symbol servers ────────────────────────────────────────────────────────

    /// <summary>
    /// Ordered list of symbol server URLs.
    /// First entry that has the matching PDB wins.
    /// Default: Microsoft public symbol server.
    /// </summary>
    public List<string> SymbolServerUrls { get; set; } =
    [
        "https://msdl.microsoft.com/download/symbols"
    ];

    /// <summary>
    /// Local symbol cache directory.
    /// Empty = %TEMP%\WpfHexEditorSymbols.
    /// </summary>
    public string SymbolCachePath { get; set; } = string.Empty;

    /// <summary>When false, symbol server lookups are disabled entirely.</summary>
    public bool SymbolServerEnabled { get; set; } = true;
}
