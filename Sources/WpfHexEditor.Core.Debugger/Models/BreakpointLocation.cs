// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Models/BreakpointLocation.cs
// Description: IDE-side breakpoint model (file + line + condition).
// ==========================================================

namespace WpfHexEditor.Core.Debugger.Models;

/// <summary>An IDE-managed breakpoint location (source + line).</summary>
public sealed record BreakpointLocation
{
    /// <summary>Absolute path of the source file.</summary>
    public string FilePath  { get; init; } = string.Empty;

    /// <summary>1-based line number.</summary>
    public int    Line      { get; init; }

    /// <summary>1-based column (0 = any column).</summary>
    public int    Column    { get; init; }

    /// <summary>Optional conditional expression (empty = unconditional).</summary>
    public string Condition { get; init; } = string.Empty;

    /// <summary>Whether the breakpoint is enabled.</summary>
    public bool   IsEnabled { get; init; } = true;

    /// <summary>True when the adapter has verified the location is valid.</summary>
    public bool   IsVerified { get; init; }

    /// <summary>Adapter-reported message (e.g. "unbound" reason).</summary>
    public string? Message   { get; init; }

    public override string ToString() =>
        $"{System.IO.Path.GetFileName(FilePath)}:{Line}{(string.IsNullOrEmpty(Condition) ? "" : $" [{Condition}]")}";
}
