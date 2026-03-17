// ==========================================================
// Project: WpfHexEditor.Events
// File: IDEEvents/BuildEvents.cs
// Created: 2026-03-15
// Description:
//     Build lifecycle events published by the build system.
//     Three related events grouped in one file for cohesion.
// ==========================================================

namespace WpfHexEditor.Events.IDEEvents;

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
    public string Line { get; init; } = string.Empty;
}

/// <summary>Published as a build progresses (percentage and status text).</summary>
public sealed record BuildProgressUpdatedEvent : IDEEventBase
{
    public int    ProgressPercent { get; init; }
    public string StatusText      { get; init; } = string.Empty;
}
