// ==========================================================
// Project: WpfHexEditor.SDK
// File: FormatParsingEvents.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     EventBus events for format parsing communication between
//     the ParsedFieldsPanel, FormatParsingService, and editors.
//     These replace the direct 5-event wiring between HexEditor and panel.
//
// Architecture Notes:
//     All events are immutable records. Published via IPluginEventBus.
//     Editors subscribe only to the events they care about.
// ==========================================================

namespace WpfHexEditor.SDK.Events
{
    /// <summary>
    /// Published when the user clicks a parsed field in the panel.
    /// The active editor should highlight the corresponding byte range.
    /// </summary>
    public sealed class FormatFieldSelectedEvent
    {
        public long Offset { get; init; }
        public int Length { get; init; }
    }

    /// <summary>
    /// Published when the user requests a format field refresh (e.g. after edits).
    /// </summary>
    public sealed class FormatRefreshRequestedEvent { }

    /// <summary>
    /// Published when the user switches the value display formatter.
    /// </summary>
    public sealed class FormatFormatterChangedEvent
    {
        public string FormatterType { get; init; } = "mixed";
    }

    /// <summary>
    /// Published when the user edits a field value in the panel.
    /// The active editor should write the new bytes.
    /// </summary>
    public sealed class FormatFieldEditedEvent
    {
        public long Offset { get; init; }
        public byte[] NewBytes { get; init; } = System.Array.Empty<byte>();
    }

    /// <summary>
    /// Published when the user switches to a different format candidate.
    /// </summary>
    public sealed class FormatCandidateSwitchedEvent
    {
        /// <summary>Machine-readable format name of the selected candidate.</summary>
        public string FormatName { get; init; } = "";
    }

    /// <summary>
    /// Published when the user clicks a navigation bookmark in the panel.
    /// The active editor should scroll to the specified offset.
    /// </summary>
    public sealed class FormatBookmarkNavigateEvent
    {
        public long Offset { get; init; }
    }
}
