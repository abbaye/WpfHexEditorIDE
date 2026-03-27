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
    bool    IsVerified
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
    /// Explicitly delete a breakpoint. No-op when no breakpoint exists at that location.
    /// </summary>
    Task DeleteBreakpointAsync(string filePath, int line);

    /// <summary>Remove all breakpoints.</summary>
    Task ClearAllBreakpointsAsync();

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

    /// <summary>Get the current call stack frames (only valid when paused).</summary>
    Task<IReadOnlyList<DebugFrameInfo>> GetCallStackAsync();

    /// <summary>Get variables in a scope by variablesReference (0 = locals).</summary>
    Task<IReadOnlyList<DebugVariableInfo>> GetVariablesAsync(int variablesReference);

    /// <summary>Evaluate an expression in the current frame context.</summary>
    Task<string> EvaluateAsync(string expression, int? frameId = null);
}
