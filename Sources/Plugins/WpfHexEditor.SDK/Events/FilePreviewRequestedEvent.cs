// ==========================================================
// Project: WpfHexEditor.SDK
// File: FilePreviewRequestedEvent.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     EventBus event published when a panel (Solution Explorer, file browser, etc.)
//     selects a file that should be previewed in the Parsed Fields panel.
//     Allows any file-browsing panel to trigger format detection without opening a tab.
//
// Architecture Notes:
//     Published via IPluginEventBus. Consumed by ParsedFieldsPlugin.
//     Carries only the file path — no binary data (the consumer opens the file).
// ==========================================================

namespace WpfHexEditor.SDK.Events
{
    /// <summary>
    /// Published when a file is selected in an explorer panel (e.g. Solution Explorer)
    /// and should be previewed in the Parsed Fields panel.
    /// </summary>
    public sealed class FilePreviewRequestedEvent
    {
        /// <summary>Absolute path of the selected file.</summary>
        public string FilePath { get; init; } = "";

        /// <summary>Source panel that triggered the event (for dedup/logging).</summary>
        public string SourcePanelId { get; init; } = "";
    }
}
