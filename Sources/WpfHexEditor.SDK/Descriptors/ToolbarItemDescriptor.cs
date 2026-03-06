// ==========================================================
// Project: WpfHexEditor.SDK
// File: ToolbarItemDescriptor.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Metadata for a toolbar button contributed by a plugin via IUIRegistry.RegisterToolbarItem.
//
// Architecture Notes:
//     Consumed by the App layer's toolbar host to inject plugin toolbar buttons.
//     Must use Segoe MDL2 Assets for icons to comply with IDE theme standards.
//
// ==========================================================

using System.Windows.Input;

namespace WpfHexEditor.SDK.Descriptors;

/// <summary>
/// Describes a toolbar button or separator contributed by a plugin.
/// </summary>
public sealed class ToolbarItemDescriptor
{
    /// <summary>Segoe MDL2 Assets glyph character for the button icon (e.g. "\uE8A5").</summary>
    public string? IconGlyph { get; init; }

    /// <summary>Tooltip text displayed on hover (also used as accessibility label).</summary>
    public string ToolTip { get; init; } = string.Empty;

    /// <summary>Command bound to the button.</summary>
    public ICommand? Command { get; init; }

    /// <summary>Command parameter.</summary>
    public object? CommandParameter { get; init; }

    /// <summary>When true, renders as a separator instead of a button.</summary>
    public bool IsSeparator { get; init; }

    /// <summary>Preferred toolbar group (0 = default group, groups are visually separated).</summary>
    public int Group { get; init; }
}
