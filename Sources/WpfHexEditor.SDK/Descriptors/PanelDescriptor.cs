// ==========================================================
// Project: WpfHexEditor.SDK
// File: PanelDescriptor.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Metadata passed to IUIRegistry.RegisterPanel describing how a plugin panel
//     should be docked in the IDE (title, default position, close behaviour).
//
// Architecture Notes:
//     Consumed by DockingAdapter to configure the DockItem in the WPF docking engine.
//
// ==========================================================

namespace WpfHexEditor.SDK.Descriptors;

/// <summary>
/// Describes a dockable panel contributed by a plugin.
/// </summary>
public sealed class PanelDescriptor
{
    /// <summary>Panel title displayed in the tab header.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Default dock side when first shown.
    /// Valid values: "Left", "Right", "Bottom", "Top", "Center".
    /// </summary>
    public string DefaultDockSide { get; init; } = "Right";

    /// <summary>Whether the user can close this panel (default: true).</summary>
    public bool CanClose { get; init; } = true;

    /// <summary>Whether the panel starts as a floating window (default: false).</summary>
    public bool IsFloating { get; init; }

    /// <summary>Preferred width when docked horizontally (0 = auto).</summary>
    public double PreferredWidth { get; init; }

    /// <summary>Preferred height when docked vertically (0 = auto).</summary>
    public double PreferredHeight { get; init; }
}
