// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Services/IDebuggerService.cs
// Description:
//     SDK contract for the IDE debugger service.
//     Plugins use this to query session state and react to debug events.
//     DebuggerServiceImpl (App layer) is the concrete implementation.
// Architecture:
//     Depends on WpfHexEditor.Core.Debugger models (via transitive ref).
//     No WPF types exposed here — pure contract.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Breakpoint entry visible to plugins (read-only snapshot).
/// </summary>
public sealed record DebugBreakpointInfo(
    string  FilePath,
    int     Line,
    string? Condition,
    bool    IsEnabled,
    bool    IsVerified,
    int     HitCount = 0,
    // Extended settings (mirrors BreakpointLocation)
    WpfHexEditor.Core.Debugger.Models.BpConditionKind ConditionKind  = WpfHexEditor.Core.Debugger.Models.BpConditionKind.None,
    WpfHexEditor.Core.Debugger.Models.BpConditionMode ConditionMode  = WpfHexEditor.Core.Debugger.Models.BpConditionMode.IsTrue,
    WpfHexEditor.Core.Debugger.Models.BpHitCountOp    HitCountOp     = WpfHexEditor.Core.Debugger.Models.BpHitCountOp.Equal,
    int     HitCountTarget    = 1,
    string? FilterExpr        = null,
    bool    HasAction         = false,
    string? LogMessage        = null,
    bool    ContinueExecution = true,
    bool    DisableOnceHit    = false,
    string? DependsOnBpKey    = null
);

/// <summary>
/// Stack frame snapshot visible to plugins.
/// </summary>
public sealed record DebugFrameInfo(
    int     Id,
    string  Name,
    string? FilePath,
    int     Line,
    int     Column
);

/// <summary>
/// Thread snapshot visible to plugins.
/// </summary>
public sealed record DebugThreadInfo(
    int    Id,
    string Name
);

/// <summary>
/// Exception filter configuration for the Exception Settings panel.
/// </summary>
public sealed record ExceptionFilterInfo(
    string  Filter,
    string  Label,
    bool    IsEnabled,
    string? Condition = null
);

/// <summary>
/// Variable snapshot visible to plugins.
/// </summary>
public sealed record DebugVariableInfo(
    string  Name,
    string  Value,
    string? Type,
    int     VariablesReference
);

/// <summary>
/// IDE debugger lifecycle state (mirrors Core.Debugger.DebugSessionState).
/// Duplicated to avoid SDK depending on Core.Debugger directly.
/// </summary>
public enum DebugState { Idle, Launching, Running, Paused, Stopped }

/// <summary>
/// SDK contract for the IDE integrated debugger.
/// Exposed via <see cref="IIDEHostContext.Debugger"/>.
/// </summary>
public interface IDebuggerService
{
    // ── Session ────────────────────────────────────────────────────────────

    /// <summary>Current session state.</summary>
    DebugState State { get; }

    /// <summary>True when a session is active (Running or Paused).</summary>
    bool IsActive { get; }

    /// <summary>True when the session is paused.</summary>
    bool IsPaused { get; }

    /// <summary>File path where execution is currently paused (null when running).</summary>
    string? PausedFilePath { get; }

    /// <summary>1-based line number where execution is paused (0 when running).</summary>
    int PausedLine { get; }

    /// <summary>Raised whenever session state changes.</summary>
    event EventHandler? SessionChanged;

    /// <summary>Launch a new debug session using the given configuration.</summary>
    Task LaunchAsync(WpfHexEditor.Core.Debugger.Models.DebugLaunchConfig config);

    /// <summary>Stop the active debug session. No-op when idle.</summary>
    Task StopSessionAsync();

    // ── Breakpoints ────────────────────────────────────────────────────────

    /// <summary>All currently registered breakpoints.</summary>
    IReadOnlyList<DebugBreakpointInfo> Breakpoints { get; }

    /// <summary>Raised when the breakpoint list changes (add/remove/verify).</summary>
    event EventHandler? BreakpointsChanged;

    /// <summary>Toggle a breakpoint at the given file/line. Returns the new state.</summary>
    Task<bool> ToggleBreakpointAsync(string filePath, int line, string? condition = null);

    /// <summary>
    /// Update an existing breakpoint's condition and/or enabled state.
    /// No-op when no breakpoint exists at that location.
    /// </summary>
    Task UpdateBreakpointAsync(string filePath, int line, string? condition, bool isEnabled);

    /// <summary>
    /// Apply full breakpoint settings (condition, action, options) from the
    /// <c>BreakpointConditionDialog</c>.  No-op when no breakpoint exists at that location.
    /// </summary>
    Task UpdateBreakpointSettingsAsync(string filePath, int line,
        WpfHexEditor.Core.Debugger.Models.BreakpointSettings settings);

    /// <summary>
    /// Explicitly delete a breakpoint. No-op when no breakpoint exists at that location.
    /// </summary>
    Task DeleteBreakpointAsync(string filePath, int line);

    /// <summary>Remove all breakpoints.</summary>
    Task ClearAllBreakpointsAsync();

    /// <summary>Get the hit count for a breakpoint in the current session (0 if not hit or no session).</summary>
    int GetHitCount(string filePath, int line) => 0;

    /// <summary>Get all breakpoints for a specific file.</summary>
    IReadOnlyList<DebugBreakpointInfo> GetBreakpointsForFile(string filePath) =>
        Breakpoints.Where(b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase)).ToList();

    // ── Execution ──────────────────────────────────────────────────────────

    /// <summary>Continue execution (resume after pause).</summary>
    Task ContinueAsync();

    /// <summary>Step over the current line.</summary>
    Task StepOverAsync();

    /// <summary>Step into a method call on the current line.</summary>
    Task StepIntoAsync();

    /// <summary>Step out of the current method.</summary>
    Task StepOutAsync();

    /// <summary>Pause execution (break all). No-op when already paused or idle.</summary>
    Task PauseAsync();

    // ── Inspection ─────────────────────────────────────────────────────────

    /// <summary>Get all active threads (only valid when paused).</summary>
    Task<IReadOnlyList<DebugThreadInfo>> GetThreadsAsync();

    /// <summary>Get the call stack frames for a specific thread (0 = active thread).</summary>
    Task<IReadOnlyList<DebugFrameInfo>> GetCallStackForThreadAsync(int threadId);

    /// <summary>Get the current call stack frames (only valid when paused).</summary>
    Task<IReadOnlyList<DebugFrameInfo>> GetCallStackAsync();

    /// <summary>Get variables in a scope by variablesReference (0 = locals).</summary>
    Task<IReadOnlyList<DebugVariableInfo>> GetVariablesAsync(int variablesReference);

    /// <summary>Evaluate an expression in the current frame context.</summary>
    Task<string> EvaluateAsync(string expression, int? frameId = null);

    /// <summary>
    /// Set a variable's value in the given scope (variablesReference).
    /// Returns the new value string, or null if the adapter does not support setVariable.
    /// </summary>
    Task<string?> SetVariableAsync(int variablesReference, string name, string newValue);

    /// <summary>
    /// Run to the given file/line (set a temporary breakpoint + continue, or use gotoTargets/goto if supported).
    /// No-op when no session is active.
    /// </summary>
    Task RunToCursorAsync(string filePath, int line1);

    /// <summary>
    /// Get the exception filter list supported by the active adapter (or a built-in default set).
    /// Returns empty list when no session active or adapter does not support exception filters.
    /// </summary>
    IReadOnlyList<ExceptionFilterInfo> ExceptionFilters { get; }

    /// <summary>
    /// Apply the given exception filter settings to the active debug session.
    /// Persisted in the session; re-applied on next LaunchAsync/AttachAsync.
    /// </summary>
    Task SetExceptionFiltersAsync(IReadOnlyList<ExceptionFilterInfo> filters);

    // ── Adapter registry ───────────────────────────────────────────────────

    /// <summary>
    /// Register a custom debug adapter factory for a language ID.
    /// Plugins use this to add support for non-built-in languages (e.g. "ruby", "rust").
    /// The factory is called each time a new session is launched for that language.
    /// Default implementation is a no-op (override in DebuggerServiceImpl).
    /// </summary>
    void RegisterAdapter(string languageId, Func<object> factory) { }

    /// <summary>Unregister a previously registered custom adapter factory.</summary>
    void UnregisterAdapter(string languageId) { }
}
