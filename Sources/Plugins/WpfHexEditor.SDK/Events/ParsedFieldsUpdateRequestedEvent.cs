// ==========================================================
// Project: WpfHexEditor.SDK
// File: ParsedFieldsUpdateRequestedEvent.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Internal event representing a queued ParsedFields update request.
//     Used by the lazy update system to defer parsing when the panel is hidden.
//
// Architecture Notes:
//     Not published on the EventBus — used internally by ParsedFieldsPlugin
//     as a queue item. Stored in _pendingUpdate, flushed on PanelShown.
// ==========================================================

namespace WpfHexEditor.SDK.Events
{
    /// <summary>
    /// Represents a deferred ParsedFields update request.
    /// Queued when the panel is hidden, executed when it becomes visible.
    /// </summary>
    public sealed class ParsedFieldsUpdateRequestedEvent
    {
        /// <summary>File path to analyze, or null for assembly-based updates.</summary>
        public string? FilePath { get; init; }

        /// <summary>Source kind: "document" (tab switch), "explorer" (file preview), "assembly" (PE member).</summary>
        public string SourceKind { get; init; } = "document";

        /// <summary>PE offset for assembly member navigation (-1 = not applicable).</summary>
        public long PeOffset { get; init; } = -1;
    }
}
