// ==========================================================
// Project: WpfHexEditor.SDK
// File: Events/GrammarAppliedEvent.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     EventBus message published by SynalysisGrammarPlugin after successfully
//     executing a UFWB grammar against the active file.
//     ParsedFieldsPlugin subscribes to this event to populate the Parsed Fields
//     panel — same pattern as TemplateApplyRequestedEvent.
//
// Architecture Notes:
//     Decoupled via IPluginEventBus — SynalysisGrammarPlugin has no direct
//     reference to ParsedFieldsPanel or HexEditorControl.
//     SynalysisField and SynalysisColorRegion live in WpfHexEditor.Core
//     which is re-exported by the SDK, so consumers don't need an extra reference.
// ==========================================================

using WpfHexEditor.Core.SynalysisGrammar;

namespace WpfHexEditor.SDK.Events;

/// <summary>
/// EventBus message raised after SynalysisGrammarPlugin successfully parses
/// the active file with a UFWB grammar.
/// </summary>
public sealed class GrammarAppliedEvent
{
    /// <summary>Name of the grammar used (from UfwbGrammar.Name).</summary>
    public string GrammarName { get; init; } = string.Empty;

    /// <summary>Absolute path of the file that was parsed. May be empty for unsaved buffers.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// All parsed fields in document order.
    /// Consumers bridge these into ParsedFieldViewModel instances.
    /// </summary>
    public IReadOnlyList<SynalysisField> Fields { get; init; } = [];

    /// <summary>
    /// Coloured regions to overlay on the hex view.
    /// Consumers bridge these into CustomBackgroundBlock instances.
    /// </summary>
    public IReadOnlyList<SynalysisColorRegion> ColorRegions { get; init; } = [];

    /// <summary>Non-fatal parse warnings, if any.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
