//////////////////////////////////////////////
// Project      : WpfHexEditor.Core.Options
// File         : ViewMenuSettings.cs
// Description  : Settings model for the dynamic View menu organization system.
// Architecture : Pure data — no WPF or framework dependencies.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Options;

/// <summary>
/// How the View menu items are organized visually.
/// </summary>
public enum ViewMenuOrganizationMode
{
    /// <summary>All items in a single flat list with group separators (classic behaviour).</summary>
    Flat,

    /// <summary>Items grouped into submenus by functional category (Analysis, Design, etc.).</summary>
    Categorized,

    /// <summary>Items grouped by their default dock side (Left, Right, Bottom, etc.).</summary>
    ByDockSide
}

/// <summary>
/// Sort order for items within each View menu category/group.
/// </summary>
public enum ViewMenuSortOrder
{
    /// <summary>A-Z by display header.</summary>
    Alphabetical,

    /// <summary>Most-frequently-used items first (based on Command Palette execution history).</summary>
    ByFrequency,

    /// <summary>User-defined custom order (via drag in Options page).</summary>
    Custom
}

/// <summary>
/// User-configurable settings for the dynamic View menu organization system.
/// Serialised as "viewMenu": { … } in settings.json.
/// </summary>
public sealed class ViewMenuSettings
{
    /// <summary>Current organization mode (default: Categorized).</summary>
    public ViewMenuOrganizationMode Mode { get; set; } = ViewMenuOrganizationMode.Categorized;

    /// <summary>Show icon glyphs next to menu items.</summary>
    public bool ShowIcons { get; set; } = true;

    /// <summary>Show keyboard shortcut gesture text on menu items.</summary>
    public bool ShowGestureText { get; set; } = true;

    /// <summary>When true, pinned/favourite items appear at the root level before submenus.</summary>
    public bool PinFavoritesToTop { get; set; } = true;

    /// <summary>
    /// Command IDs or UI IDs of items pinned to the View menu root level.
    /// Pinned items appear after Command Palette and before category submenus.
    /// </summary>
    public List<string> PinnedItemIds { get; set; } = new();

    /// <summary>Hide category submenus that contain zero visible items.</summary>
    public bool CollapseEmptyCategories { get; set; } = true;

    /// <summary>Sort order for items within each category submenu.</summary>
    public ViewMenuSortOrder SortOrder { get; set; } = ViewMenuSortOrder.Alphabetical;

    /// <summary>
    /// User-overridden category assignments keyed by item ID.
    /// When an item ID is present, its value overrides the auto-classified category.
    /// Example: { "View.EntropyAnalysis": "Data &amp; Files" }.
    /// </summary>
    public Dictionary<string, string> CategoryOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
