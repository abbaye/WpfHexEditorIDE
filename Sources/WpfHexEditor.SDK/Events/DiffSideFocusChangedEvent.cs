// ==========================================================
// Project: WpfHexEditor.SDK
// File: DiffSideFocusChangedEvent.cs
// Description:
//     EventBus event published when the user hovers a different side (left/right)
//     in the Binary Diff viewer. Consumed by ParsedFieldsPlugin to switch
//     the preview format parsing to the focused file.
//
// Architecture Notes:
//     Published via IPluginEventBus by FileComparisonPlugin.
//     Consumed by ParsedFieldsPlugin (ActivatePreview path).
// ==========================================================

namespace WpfHexEditor.SDK.Events;

/// <summary>
/// Published when the focused side (left/right) changes in a DiffViewerDocument.
/// ParsedFieldsPlugin uses this to show format fields for the focused file.
/// </summary>
public sealed class DiffSideFocusChangedEvent
{
    /// <summary>Absolute path of the file on the focused side.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Document content ID of the diff viewer tab (for dedup).</summary>
    public string DocumentContentId { get; init; } = string.Empty;
}
