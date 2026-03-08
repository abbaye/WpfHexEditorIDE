// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: RecordMacroCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Built-in terminal command: record [start | stop | save <path>]
//     Provides CLI access to macro recording without requiring toolbar interaction.
//
// Architecture Notes:
//     Feature #92: Macro recording.
//     Pattern: Command.
//
// ==========================================================

using WpfHexEditor.Core.Terminal.Macros;

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

/// <summary>
/// Built-in command: <c>record [start | stop | save &lt;path&gt;]</c>
/// Controls macro recording from the command line.
/// </summary>
public sealed class RecordMacroCommand : ITerminalCommandProvider
{
    private readonly ITerminalMacroService _macroService;

    public RecordMacroCommand(ITerminalMacroService macroService)
    {
        _macroService = macroService ?? throw new ArgumentNullException(nameof(macroService));
    }

    public string CommandName => "record";
    public string Description => "Start, stop, or save macro recording.";
    public string Usage       => "record [start | stop | save <path>]";

    public Task<int> ExecuteAsync(
        string[] args,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default)
    {
        var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "start";

        switch (sub)
        {
            case "start":
                if (_macroService.IsRecording)
                {
                    output.WriteWarning("Macro recording is already in progress.");
                    return Task.FromResult(1);
                }
                _macroService.StartRecording();
                output.WriteInfo("Macro recording started. Type 'record stop' to finish.");
                break;

            case "stop":
                if (!_macroService.IsRecording)
                {
                    output.WriteWarning("No macro recording in progress.");
                    return Task.FromResult(1);
                }
                var session = _macroService.StopRecording();
                output.WriteInfo($"Macro recording stopped. {session.Entries.Count} command(s) captured.");
                break;

            case "save":
                var path = args.Length > 1 ? string.Join(" ", args[1..]) : string.Empty;
                if (string.IsNullOrWhiteSpace(path))
                {
                    output.WriteError("Usage: record save <path.hxscript>");
                    return Task.FromResult(1);
                }
                // Stop recording first if active.
                var s = _macroService.IsRecording ? _macroService.StopRecording() : null;
                if (s is null || s.IsEmpty)
                {
                    output.WriteWarning("No commands recorded to save.");
                    return Task.FromResult(1);
                }
                var script = _macroService.ExportToHxScript(s);
                File.WriteAllText(path, script, System.Text.Encoding.UTF8);
                output.WriteInfo($"Macro saved to: {path}");
                break;

            default:
                output.WriteError($"Unknown sub-command '{sub}'. {Usage}");
                return Task.FromResult(1);
        }

        return Task.FromResult(0);
    }
}
