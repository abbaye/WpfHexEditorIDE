// ==========================================================
// Project: WpfHexEditor.SDK
// File: IPanel.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Abstraction representing the currently active panel in the IDE.
//     Provides plugin-safe access without exposing WPF internals.
//
// Architecture Notes:
//     Used by IFocusContextService to notify plugins of panel focus changes.
//     Implemented by the App layer adapting the concrete dockable panel.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Focus;

/// <summary>
/// Represents an active dockable panel visible to plugins.
/// </summary>
public interface IPanel
{
    /// <summary>Gets the panel unique content identifier (e.g. "panel-solution-explorer").</summary>
    string ContentId { get; }

    /// <summary>Gets the panel display title.</summary>
    string Title { get; }

    /// <summary>Gets whether the panel is currently visible.</summary>
    bool IsVisible { get; }
}
