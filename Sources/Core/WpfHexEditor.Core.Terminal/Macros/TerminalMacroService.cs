// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: TerminalMacroService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Concrete implementation of ITerminalMacroService.
//     Orchestrates MacroRecorder (capture) and MacroReplayEngine (playback/export).
//
// Architecture Notes:
//     Pattern: Facade — delegates to MacroRecorder and MacroReplayEngine.
//     Feature #92: Macro recording / history replay / .hxscript export.
//
// ==========================================================

namespace WpfHexEditor.Core.Terminal.Macros;

/// <summary>
/// Orchestrates macro recording, replay, and .hxscript export for the Terminal panel.
/// </summary>
public sealed class TerminalMacroService : ITerminalMacroService
{
    private readonly MacroRecorder _recorder;
    private readonly MacroReplayEngine _replayEngine;

    public event EventHandler<bool>? RecordingStateChanged;

    public TerminalMacroService(TerminalCommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _recorder     = new MacroRecorder(registry);
        _replayEngine = new MacroReplayEngine(registry);
    }

    // -- ITerminalMacroService ----------------------------------------------------

    /// <inheritdoc/>
    public bool IsRecording => _recorder.IsRecording;

    /// <inheritdoc/>
    public void StartRecording()
    {
        if (_recorder.IsRecording) return;
        _recorder.Start();
        RecordingStateChanged?.Invoke(this, true);
    }

    /// <inheritdoc/>
    public MacroSession StopRecording()
    {
        if (!_recorder.IsRecording)
            return new MacroSession { StartedAt = DateTime.UtcNow, StoppedAt = DateTime.UtcNow };

        var session = _recorder.Stop();
        RecordingStateChanged?.Invoke(this, false);
        return session;
    }

    /// <inheritdoc/>
    public Task ReplayAsync(
        MacroSession session,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default)
        => _replayEngine.ReplayAsync(session, output, context, ct);

    /// <inheritdoc/>
    public Task ReplayHistoryAsync(
        IEnumerable<string> commands,
        int count,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default)
        => _replayEngine.ReplayHistoryAsync(commands, count, output, context, ct);

    /// <inheritdoc/>
    public string ExportToHxScript(MacroSession session)
        => _replayEngine.ExportToHxScript(session);
}
