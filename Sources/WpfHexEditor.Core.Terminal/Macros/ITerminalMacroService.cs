// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: ITerminalMacroService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Contract for macro recording, replay, and export.
//     MacroRecorder captures commands executed through TerminalCommandRegistry.
//     MacroReplayEngine replays them in order via the same registry.
//
// Architecture Notes:
//     Feature #92: Script execution (.hxscript) + macro recording / history replay.
//     Implemented by TerminalMacroService; consumed by TerminalPanelViewModel.
//
// ==========================================================

namespace WpfHexEditor.Core.Terminal.Macros;

/// <summary>
/// Manages macro recording, replay, and export for the Terminal panel.
/// </summary>
public interface ITerminalMacroService
{
    // -- Recording ----------------------------------------------------------------

    /// <summary>Starts a new recording session. No-op if already recording.</summary>
    void StartRecording();

    /// <summary>
    /// Stops the current recording and returns the captured session.
    /// Returns an empty session if not currently recording.
    /// </summary>
    MacroSession StopRecording();

    /// <summary>True while a recording is in progress.</summary>
    bool IsRecording { get; }

    /// <summary>Raised when recording state changes (true = started, false = stopped).</summary>
    event EventHandler<bool> RecordingStateChanged;

    // -- Replay -------------------------------------------------------------------

    /// <summary>
    /// Replays all entries of a <see cref="MacroSession"/> in order.
    /// Each command is dispatched through <see cref="TerminalCommandRegistry"/>.
    /// </summary>
    Task ReplayAsync(
        MacroSession session,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Replays the last <paramref name="count"/> commands from the provided history.
    /// Pass <c>int.MaxValue</c> to replay the full history.
    /// </summary>
    Task ReplayHistoryAsync(
        IEnumerable<string> commands,
        int count,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default);

    // -- Export -------------------------------------------------------------------

    /// <summary>
    /// Converts a <see cref="MacroSession"/> to an <c>.hxscript</c> source string
    /// that can be executed by <see cref="Scripting.HxScriptEngine"/>.
    /// </summary>
    string ExportToHxScript(MacroSession session);
}
