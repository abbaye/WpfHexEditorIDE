//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Input;

namespace WpfHexEditor.SDK.Descriptors;

/// <summary>
/// Describes a menu item contributed by a plugin.
/// </summary>
public sealed class MenuItemDescriptor
{
    /// <summary>Display text for the menu item (supports access keys via underscore, e.g. "_Analyze").</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>
    /// Slash-separated path of the parent menu where this item is inserted.
    /// Examples: "Tools", "View/Panels", "Edit/Find".
    /// Root-level menus: "File", "Edit", "View", "Tools", "Help".
    /// </summary>
    public string ParentPath { get; init; } = "Tools";

    /// <summary>Keyboard shortcut gesture (optional, e.g. "Ctrl+Shift+P").</summary>
    public string? GestureText { get; init; }

    /// <summary>Command to execute when the item is clicked.</summary>
    public ICommand? Command { get; init; }

    /// <summary>Command parameter passed to Command.Execute.</summary>
    public object? CommandParameter { get; init; }

    /// <summary>Segoe MDL2 glyph character for the icon (optional, e.g. "\uE8A5").</summary>
    public string? IconGlyph { get; init; }

    /// <summary>Tooltip text displayed on hover.</summary>
    public string? ToolTip { get; init; }

    /// <summary>Insertion position in the parent menu (-1 = append at end).</summary>
    public int InsertPosition { get; init; } = -1;

    /// <summary>
    /// Optional group name used to cluster plugin menu items under a shared separator.
    /// All items sharing the same Group value in the same parent menu are placed together,
    /// separated from other groups by a <see cref="System.Windows.Controls.Separator"/>.
    /// Example: "Analysis", "FileTools", "Statistics".
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Functional category for View menu dynamic organization
    /// (e.g., "Analysis", "Design", "Data &amp; Files", "Core IDE").
    /// When null, the ViewMenuOrganizer infers the category from <see cref="Group"/>,
    /// header keyword matching, or falls back to "Other".
    /// </summary>
    public string? Category { get; init; }
}
