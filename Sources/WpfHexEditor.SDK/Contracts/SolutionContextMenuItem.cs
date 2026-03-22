// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/SolutionContextMenuItem.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Represents a single menu item contributed by a plugin to the
//     Solution Explorer context menu.
// ==========================================================

using System.Windows.Input;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// A menu item (or separator) contributed by a plugin into the
/// Solution Explorer right-click context menu.
/// </summary>
public sealed record SolutionContextMenuItem
{
    /// <summary>Display text (e.g. "View Class Diagram"). Ignored when <see cref="IsSeparator"/> is true.</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>Segoe MDL2 glyph (e.g. "\uE8A5"). Optional.</summary>
    public string? IconGlyph { get; init; }

    /// <summary>Command to execute when clicked. Optional.</summary>
    public ICommand? Command { get; init; }

    /// <summary>Command parameter passed to <see cref="Command"/>. Optional.</summary>
    public object? CommandParameter { get; init; }

    /// <summary>When true, renders a <see cref="System.Windows.Controls.Separator"/> instead of a menu item.</summary>
    public bool IsSeparator { get; init; }

    // -- Convenience factories -------------------------------------------------

    /// <summary>Creates a normal clickable menu item.</summary>
    public static SolutionContextMenuItem Item(string header, ICommand command, object? parameter = null, string? iconGlyph = null)
        => new() { Header = header, Command = command, CommandParameter = parameter, IconGlyph = iconGlyph };

    /// <summary>Creates a separator line.</summary>
    public static SolutionContextMenuItem Separator()
        => new() { IsSeparator = true };
}
