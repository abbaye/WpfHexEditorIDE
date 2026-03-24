// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Models/DebugSession.cs
// Description: Immutable snapshot of the current debug session state.
// ==========================================================

namespace WpfHexEditor.Core.Debugger.Models;

/// <summary>Debugger lifecycle state machine.</summary>
public enum DebugSessionState
{
    /// <summary>No active session.</summary>
    Idle,

    /// <summary>Adapter process launched, initializing.</summary>
    Launching,

    /// <summary>Session active, process is running.</summary>
    Running,

    /// <summary>Process stopped (breakpoint / step / exception).</summary>
    Paused,

    /// <summary>Session ended (process exited or detached).</summary>
    Stopped
}

/// <summary>Why the session paused (mirrors DAP stopped-reason).</summary>
public enum PauseReason { None, Breakpoint, Step, Exception, Pause, Entry }

/// <summary>
/// Immutable snapshot of the current debug session.
/// Published via IDebuggerService.Session; changes via SessionChanged event.
/// </summary>
public sealed record DebugSession
{
    public static readonly DebugSession Empty = new();

    public string           SessionId       { get; init; } = string.Empty;
    public DebugSessionState State          { get; init; } = DebugSessionState.Idle;
    public string           ProjectPath     { get; init; } = string.Empty;
    public int              ProcessId       { get; init; }
    public int              ActiveThreadId  { get; init; }
    public int              CurrentFrameId  { get; init; }
    public PauseReason      PauseReason     { get; init; }

    /// <summary>File path where execution is currently paused (null when Running).</summary>
    public string?          PausedFilePath  { get; init; }

    /// <summary>1-based line number where execution is paused.</summary>
    public int              PausedLine      { get; init; }

    public bool IsActive  => State is DebugSessionState.Running or DebugSessionState.Paused;
    public bool IsPaused  => State == DebugSessionState.Paused;
    public bool IsIdle    => State == DebugSessionState.Idle;
}
