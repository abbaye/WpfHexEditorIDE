// ==========================================================
// Project: WpfHexEditor.Events
// File: IDEEvents/PluginLoadedEvent.cs
// Created: 2026-03-15
// Description:
//     Published by WpfPluginHost after a plugin is successfully loaded and initialized.
// ==========================================================

namespace WpfHexEditor.Events.IDEEvents;

/// <summary>Published when a plugin finishes initialization and is fully loaded.</summary>
public sealed record PluginLoadedEvent : IDEEventBase
{
    public string PluginId { get; init; } = string.Empty;
    public string PluginName { get; init; } = string.Empty;
    public string IsolationMode { get; init; } = string.Empty;
}
