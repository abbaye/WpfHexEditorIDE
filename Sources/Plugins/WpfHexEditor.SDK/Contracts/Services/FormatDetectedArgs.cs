// ==========================================================
// Project: WpfHexEditor.SDK
// File: FormatDetectedArgs.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     SDK-local DTO for the IHexEditorService.FormatDetected event.
//     Kept separate from WpfHexEditor.Core.Events.FormatDetectedEventArgs
//     so that SDK consumers have zero dependency on Core.
//
// Architecture Notes:
//     Immutable record — use init-only properties.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Event arguments raised when the active HexEditor completes format auto-detection.
/// </summary>
public sealed class FormatDetectedArgs : EventArgs
{
    /// <summary>True if a format was successfully identified.</summary>
    public bool    Success    { get; init; }

    /// <summary>Machine-readable format identifier (e.g. "PE_EXE", "PNG"), or null on failure.</summary>
    public string? FormatId   { get; init; }

    /// <summary>Human-readable format name (e.g. "Portable Executable"), or null on failure.</summary>
    public string? FormatName { get; init; }

    /// <summary>
    /// The raw <c>WpfHexEditor.Core.FormatDetection.FormatDefinition</c> object.
    /// Populated only by the built-in <c>HexEditorServiceImpl</c>.
    /// Third-party / sandboxed plugins must use the string properties above.
    /// Bundled first-party plugins may safely cast to <c>FormatDefinition</c>.
    /// </summary>
    public object? RawFormatDefinition { get; init; }
}
