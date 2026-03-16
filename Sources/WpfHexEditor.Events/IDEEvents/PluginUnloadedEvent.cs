// ==========================================================
// Project: WpfHexEditor.Events
// File: IDEEvents/PluginUnloadedEvent.cs
// Created: 2026-03-15
// Description:
//     Published by WpfPluginHost after a plugin is successfully unloaded.
// ==========================================================

namespace WpfHexEditor.Events.IDEEvents;

/// <summary>Published when a plugin is unloaded from the IDE.</summary>
public sealed record PluginUnloadedEvent : IDEEventBase
{
    public string PluginId { get; init; } = string.Empty;
    public string PluginName { get; init; } = string.Empty;
}
