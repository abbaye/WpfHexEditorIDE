//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

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
