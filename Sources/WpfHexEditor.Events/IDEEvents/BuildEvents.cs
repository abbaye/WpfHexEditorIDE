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
    public string ProjectPath { get; init; } = string.Empty;
    public string Configuration { get; init; } = string.Empty;
}

/// <summary>Published when a project build succeeds.</summary>
public sealed record BuildSucceededEvent : IDEEventBase
{
    public string ProjectPath { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public int WarningCount { get; init; }
}

/// <summary>Published when a project build fails.</summary>
public sealed record BuildFailedEvent : IDEEventBase
{
    public string ProjectPath { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public int ErrorCount { get; init; }
    public TimeSpan Duration { get; init; }
}
