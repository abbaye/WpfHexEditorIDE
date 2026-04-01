// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/BuildEvents.cs
// Created: 2026-03-15
// Description:
//     Build lifecycle events published by the build system.
//     Three related events grouped in one file for cohesion.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>Published when a project build starts.</summary>
public sealed record BuildStartedEvent : IDEEventBase
{
    public string   ProjectPath   { get; init; } = string.Empty;
    public string   Configuration { get; init; } = string.Empty;
    public DateTime StartedAt     { get; init; } = DateTime.Now;
}

/// <summary>Published when a build completes successfully.</summary>
public sealed record BuildSucceededEvent : IDEEventBase
{
    public string   ProjectPath    { get; init; } = string.Empty;
    public TimeSpan Duration       { get; init; }
    public DateTime StartedAt      { get; init; }
    public int      WarningCount   { get; init; }
    public int      SucceededCount { get; init; }
    public int      FailedCount    { get; init; }
    public int      SkippedCount   { get; init; }
}

/// <summary>Published when a build completes with errors.</summary>
public sealed record BuildFailedEvent : IDEEventBase
{
    public string   ProjectPath    { get; init; } = string.Empty;
    public string   ErrorMessage   { get; init; } = string.Empty;
    public TimeSpan Duration       { get; init; }
    public DateTime StartedAt      { get; init; }
    public int      ErrorCount     { get; init; }
    public int      Warnings       { get; init; }
    public int      SucceededCount { get; init; }
    public int      FailedCount    { get; init; }
    public int      SkippedCount   { get; init; }
}

/// <summary>Published when the user cancels an active build.</summary>
public sealed record BuildCancelledEvent : IDEEventBase;

/// <summary>Published for each line of build output (log streaming).</summary>
public sealed record BuildOutputLineEvent : IDEEventBase
{
    public string Line    { get; init; } = string.Empty;

    /// <summary>When true the line is rendered in red (stderr / error output).</summary>
    public bool   IsError { get; init; }
}

/// <summary>Published as a build progresses (percentage and status text).</summary>
public sealed record BuildProgressUpdatedEvent : IDEEventBase
{
    public int    ProgressPercent { get; init; }
    public string StatusText      { get; init; } = string.Empty;
}

/// <summary>
/// Published immediately after the IDE launches a process via "Start Without Debugging".
/// DiagnosticTools plugin subscribes to attach via EventPipe.
/// </summary>
public sealed record ProcessLaunchedEvent : IDEEventBase
{
    /// <summary>OS process ID of the launched application.</summary>
    public int    ProcessId   { get; init; }

    /// <summary>Friendly name (file name without extension).</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>UTC time the process was started.</summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>Full path to the launched executable.</summary>
    public string OutputPath  { get; init; } = string.Empty;
}

/// <summary>
/// Published when a project's incremental-build dirty state changes.
/// DiagnosticTools / Solution Explorer subscribe to show a dirty indicator.
/// </summary>
public sealed record ProjectDirtyChangedEvent : IDEEventBase
{
    /// <summary>The project whose dirty state changed.</summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary><see langword="true"/> when the project is now dirty; <see langword="false"/> when it became clean.</summary>
    public bool IsDirty { get; init; }
}

/// <summary>
/// Published when a process that was started via "Start Without Debugging" exits.
/// </summary>
public sealed record ProcessExitedEvent : IDEEventBase
{
    /// <summary>OS process ID of the process that exited.</summary>
    public int      ProcessId { get; init; }

    /// <summary>Exit code returned by the process.</summary>
    public int      ExitCode  { get; init; }

    /// <summary>Total wall-clock time the process ran.</summary>
    public TimeSpan Duration  { get; init; }
}
