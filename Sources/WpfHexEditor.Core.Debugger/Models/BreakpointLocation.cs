// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Models/BreakpointLocation.cs
// Description: IDE-side breakpoint model (file + line + condition + full settings).
// ==========================================================

namespace WpfHexEditor.Core.Debugger.Models;

// ── Enums mirrored from Plugin layer so Core.Debugger stays self-contained ───

/// <summary>Kind of condition on a breakpoint (mirrors Plugin.Models.BreakpointConditionKind).</summary>
public enum BpConditionKind  { None, ConditionalExpression, HitCount, Filter }

/// <summary>Evaluation mode for a conditional-expression breakpoint.</summary>
public enum BpConditionMode  { IsTrue, WhenChanged }

/// <summary>Comparison operator for a hit-count breakpoint.</summary>
public enum BpHitCountOp     { Equal, GreaterOrEqual, MultipleOf }

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

    /// <summary>Number of times this breakpoint was hit in the current debug session.</summary>
    public int HitCount { get; init; }

    // ── Extended condition / action / options settings ──────────────────────

    /// <summary>Kind of condition applied (None = always breaks).</summary>
    public BpConditionKind ConditionKind  { get; init; }

    /// <summary>Evaluation mode for ConditionalExpression kind.</summary>
    public BpConditionMode ConditionMode  { get; init; }

    /// <summary>Comparison operator for HitCount kind.</summary>
    public BpHitCountOp    HitCountOp     { get; init; }

    /// <summary>Target value for HitCount kind (default 1).</summary>
    public int             HitCountTarget { get; init; } = 1;

    /// <summary>Filter expression for Filter kind (machineName / processId / threadId).</summary>
    public string?         FilterExpr     { get; init; }

    /// <summary>True when this breakpoint logs a message (tracepoint / action).</summary>
    public bool            HasAction      { get; init; }

    /// <summary>Log message template (supports $FUNCTION, {expr} interpolation).</summary>
    public string?         LogMessage     { get; init; }

    /// <summary>When true, a tracepoint continues execution after logging (default true).</summary>
    public bool            ContinueExecution { get; init; } = true;

    /// <summary>When true, the breakpoint disables itself after being hit once.</summary>
    public bool            DisableOnceHit { get; init; }

    /// <summary>Key of another breakpoint that must be hit first ("filePath:line").</summary>
    public string?         DependsOnBpKey { get; init; }

    public override string ToString() =>
        $"{System.IO.Path.GetFileName(FilePath)}:{Line}{(string.IsNullOrEmpty(Condition) ? "" : $" [{Condition}]")}";
}

/// <summary>
/// Full settings snapshot produced by the BreakpointConditionDialog.
/// Passed to <c>IDebuggerService.UpdateBreakpointSettingsAsync</c>.
/// </summary>
public sealed record BreakpointSettings(
    // ── Condition ──────────────────────────────────────────────────────────
    BpConditionKind ConditionKind     = BpConditionKind.None,
    string?         ConditionExpr     = null,
    BpConditionMode ConditionMode     = BpConditionMode.IsTrue,
    BpHitCountOp    HitCountOp        = BpHitCountOp.Equal,
    int             HitCountTarget    = 1,
    string?         FilterExpr        = null,

    // ── Action ─────────────────────────────────────────────────────────────
    bool            HasAction         = false,
    string?         LogMessage        = null,
    bool            ContinueExecution = true,

    // ── Options ────────────────────────────────────────────────────────────
    bool            DisableOnceHit    = false,
    string?         DependsOnBpKey    = null
);
