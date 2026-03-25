// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/DebugEvents.cs
// Description:
//     IDE-level debug session events published on IIDEEventBus.
//     Subscribe via context.IDEEvents.Subscribe<DebugSessionStartedEvent>(...).
// Architecture: Leaf node — no dependencies on Core.Debugger or SDK.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>Published when a debug session starts (adapter initialized, process launched).</summary>
public sealed record DebugSessionStartedEvent : IDEEventBase
{
    /// <summary>Unique session identifier (GUID).</summary>
    public string SessionId   { get; init; } = string.Empty;

    /// <summary>Full path to the startup project.</summary>
    public string ProjectPath { get; init; } = string.Empty;

    /// <summary>OS process ID of the debuggee (0 if not yet known).</summary>
    public int    ProcessId   { get; init; }
}

/// <summary>Published when a debug session ends (process exited or user stopped).</summary>
public sealed record DebugSessionEndedEvent : IDEEventBase
{
    public string SessionId { get; init; } = string.Empty;
    public int    ExitCode  { get; init; }
}

/// <summary>
/// Published when the debuggee stops (breakpoint hit, step complete, exception, user pause).
/// Subscribers should refresh CallStack / Locals / Watches panels.
/// </summary>
public sealed record DebugSessionPausedEvent : IDEEventBase
{
    public string SessionId  { get; init; } = string.Empty;

    /// <summary>Absolute path of the source file where execution stopped.</summary>
    public string FilePath   { get; init; } = string.Empty;

    /// <summary>1-based line number.</summary>
    public int    Line       { get; init; }

    /// <summary>DAP stop reason (e.g. "breakpoint", "step", "exception").</summary>
    public string Reason     { get; init; } = string.Empty;

    /// <summary>Thread ID that triggered the stop.</summary>
    public int    ThreadId   { get; init; }
}

/// <summary>Published when the debuggee resumes execution after a pause.</summary>
public sealed record DebugSessionResumedEvent : IDEEventBase
{
    public string SessionId { get; init; } = string.Empty;
}

/// <summary>Published when a breakpoint is hit (subset of DebugSessionPausedEvent for convenience).</summary>
public sealed record BreakpointHitEvent : IDEEventBase
{
    public string FilePath { get; init; } = string.Empty;
    public int    Line     { get; init; }
    public int    ThreadId { get; init; }
}

/// <summary>Published after a step operation completes and the debuggee stops.</summary>
public sealed record StepCompletedEvent : IDEEventBase
{
    public string FilePath { get; init; } = string.Empty;
    public int    Line     { get; init; }
}

/// <summary>Published for each stdout/stderr/console output line from the debuggee.</summary>
public sealed record DebugOutputReceivedEvent : IDEEventBase
{
    /// <summary>DAP category: "console", "stdout", "stderr", "telemetry".</summary>
    public string Category { get; init; } = string.Empty;
    public string Output   { get; init; } = string.Empty;
}

/// <summary>Published when an unhandled exception is encountered during debugging.</summary>
public sealed record ExceptionHitEvent : IDEEventBase
{
    public string ExceptionType { get; init; } = string.Empty;
    public string Message       { get; init; } = string.Empty;
    public string FilePath      { get; init; } = string.Empty;
    public int    Line          { get; init; }
}
