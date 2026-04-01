// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: MacroRecorder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Observes TerminalCommandRegistry.CommandExecuted events and captures
//     each raw command string as a MacroEntry while recording is active.
//
// Architecture Notes:
//     Pattern: Observer.
//     Feature #92: Macro recording.
//     MacroRecorder subscribes to the registry event in StartRecording()
//     and unsubscribes in StopRecording() to avoid memory leaks.
//
// ==========================================================

namespace WpfHexEditor.Core.Terminal.Macros;

/// <summary>
/// Captures terminal commands executed through <see cref="TerminalCommandRegistry"/>
/// into an ordered list of <see cref="MacroEntry"/> records.
/// </summary>
public sealed class MacroRecorder
{
    private readonly TerminalCommandRegistry _registry;
    private readonly List<MacroEntry> _recorded = [];
    private readonly object _lock = new();

    private DateTime _startedAt;
    private bool _isRecording;

    public MacroRecorder(TerminalCommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>Returns true while recording is active.</summary>
    public bool IsRecording
    {
        get { lock (_lock) return _isRecording; }
    }

    /// <summary>Starts capturing commands. No-op if already recording.</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isRecording) return;
            _recorded.Clear();
            _startedAt = DateTime.UtcNow;
            _isRecording = true;
        }

        _registry.CommandExecuted += OnCommandExecuted;
    }

    /// <summary>
    /// Stops capturing and returns the collected session.
    /// Returns an empty session if not currently recording.
    /// </summary>
    public MacroSession Stop()
    {
        List<MacroEntry> snapshot;
        DateTime startedAt;

        lock (_lock)
        {
            if (!_isRecording)
                return new MacroSession { StartedAt = DateTime.UtcNow, StoppedAt = DateTime.UtcNow };

            _isRecording = false;
            snapshot = [.._recorded];
            startedAt = _startedAt;
        }

        _registry.CommandExecuted -= OnCommandExecuted;

        return new MacroSession
        {
            Name      = $"Macro {startedAt:HH:mm:ss}",
            StartedAt = startedAt,
            StoppedAt = DateTime.UtcNow,
            Entries   = snapshot
        };
    }

    // -- Observer -----------------------------------------------------------------

    private void OnCommandExecuted(object? sender, string rawCommand)
    {
        lock (_lock)
        {
            if (!_isRecording) return;
            _recorded.Add(new MacroEntry(DateTime.UtcNow, rawCommand));
        }
    }
}
