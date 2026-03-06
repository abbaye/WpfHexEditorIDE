// ==========================================================
// Project: WpfHexEditor.SDK
// File: StatusBarItemDescriptor.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Metadata for a status bar item contributed by a plugin via IUIRegistry.RegisterStatusBarItem.
//
// Architecture Notes:
//     Consumed by StatusBarAdapter to insert the item in the App status bar.
//     Content must bind to DynamicResource theme brushes — no hardcoded colors.
//
// ==========================================================

namespace WpfHexEditor.SDK.Descriptors;

/// <summary>
/// Alignment of a status bar item contributed by a plugin.
/// </summary>
public enum StatusBarAlignment
{
    /// <summary>Item is placed in the left region of the status bar.</summary>
    Left,

    /// <summary>Item is placed in the center region of the status bar.</summary>
    Center,

    /// <summary>Item is placed in the right region of the status bar.</summary>
    Right
}

/// <summary>
/// Describes a status bar item contributed by a plugin.
/// </summary>
public sealed class StatusBarItemDescriptor
{
    /// <summary>Text content displayed in the status bar item.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Alignment of the item in the status bar.</summary>
    public StatusBarAlignment Alignment { get; init; } = StatusBarAlignment.Right;

    /// <summary>Tooltip text displayed on hover.</summary>
    public string? ToolTip { get; init; }

    /// <summary>Display order within the alignment group (lower = closer to edge).</summary>
    public int Order { get; init; }
}
