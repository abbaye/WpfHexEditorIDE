// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: MacroSession.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Represents a recorded sequence of terminal commands (a macro).
//     Produced by MacroRecorder.StopRecording() and consumed by
//     MacroReplayEngine.ReplayAsync() and ExportToHxScript().
//
// Architecture Notes:
//     Pattern: Value Object / DTO — immutable after construction.
//     Feature #92: Macro recording / history replay.
//
// ==========================================================

namespace WpfHexEditor.Core.Terminal.Macros;

/// <summary>
/// An immutable snapshot of commands recorded during a macro session.
/// </summary>
public sealed class MacroSession
{
    /// <summary>Unique session identifier.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Display name for this macro (defaults to a timestamp-based label).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>UTC time at which recording started.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>UTC time at which recording was stopped.</summary>
    public DateTime StoppedAt { get; init; }

    /// <summary>Ordered list of captured command entries.</summary>
    public IReadOnlyList<MacroEntry> Entries { get; init; } = [];

    /// <summary>Returns true when no commands were recorded.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Total recording duration.</summary>
    public TimeSpan Duration => StoppedAt - StartedAt;
}
