// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/PluginActivatingEvent.cs
// Created: 2026-04-06
// Description:
//     Published by WpfPluginHost just before a dormant plugin starts loading
//     in response to a lazy-load trigger (file extension match or command invocation).
//     Consumed by PluginActivationToastService to show a brief "Loading X…" toast.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>
/// Published on the IDE event bus immediately before a dormant plugin begins activation.
/// </summary>
public sealed record PluginActivatingEvent : IDEEventBase
{
    /// <summary>The plugin ID being activated.</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>Human-readable display name of the plugin.</summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>
    /// What triggered the activation: "file:<ext>" or "command:<id>" or "manual".
    /// </summary>
    public string TriggerReason { get; init; } = string.Empty;
}
