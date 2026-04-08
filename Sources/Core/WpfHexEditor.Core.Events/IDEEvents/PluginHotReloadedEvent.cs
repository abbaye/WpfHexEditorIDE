// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/PluginHotReloadedEvent.cs
// Description:
//     Published by WpfPluginHost after a plugin is hot-reloaded
//     via Watch Mode (PluginDevLoader). Carries old/new version info
//     so UI can show a "Plugin X reloaded 1.0.0 → 1.0.1" toast.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>
/// Published when a plugin is successfully hot-reloaded via Watch Mode.
/// </summary>
public sealed record PluginHotReloadedEvent : IDEEventBase
{
    public string PluginId   { get; init; } = string.Empty;
    public string PluginName { get; init; } = string.Empty;
    public string OldVersion { get; init; } = string.Empty;
    public string NewVersion { get; init; } = string.Empty;
}

/// <summary>
/// Published when a hot-reload via Watch Mode fails.
/// </summary>
public sealed record PluginHotReloadFailedEvent : IDEEventBase
{
    public string PluginId   { get; init; } = string.Empty;
    public string PluginName { get; init; } = string.Empty;
    public string Error      { get; init; } = string.Empty;
}
