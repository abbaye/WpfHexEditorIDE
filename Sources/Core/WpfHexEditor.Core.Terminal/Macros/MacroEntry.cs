// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: MacroEntry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Immutable record representing a single recorded terminal command.
//     Used by MacroRecorder to capture commands as they are executed.
//
// Architecture Notes:
//     Pattern: Value Object (record)
//     Feature #92: Macro recording / history replay.
//
// ==========================================================

namespace WpfHexEditor.Core.Terminal.Macros;

/// <summary>
/// A single recorded terminal command entry with its capture timestamp.
/// </summary>
/// <param name="Timestamp">UTC time at which the command was captured.</param>
/// <param name="RawCommand">Full raw command string as typed by the user (e.g. "open-file test.bin").</param>
public sealed record MacroEntry(DateTime Timestamp, string RawCommand);
