//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : ViewMenuOrganizer.cs
// Description  : Central orchestrator for the dynamic View menu organization system.
//                Collects all View-menu-eligible items (built-in + plugin),
//                applies the active organization strategy, and rebuilds the WPF menu.
// Architecture : Single-responsibility service. Composition of strategies.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.Options;
using WpfHexEditor.App.Properties;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Services.ViewMenu;

/// <summary>
/// Orchestrates the dynamic View menu:
/// collects entries, classifies them, applies the active organization strategy,
/// and rebuilds the WPF <see cref="MenuItem"/> tree.
/// </summary>
public sealed class ViewMenuOrganizer
{
    private readonly MenuItem _viewMenuItem;
    private readonly MenuAdapter _menuAdapter;
    private readonly AppSettingsService _settingsService;
    private readonly Func<string, string?> _dockSideResolver;

    // Built-in entries registered by MainWindow.ViewMenu.cs
    private readonly List<ViewMenuEntry> _builtInEntries = [];

    // The special Command Palette entry (always first in menu)
    private ViewMenuEntry? _commandPaletteEntry;

    // Strategy map
    private readonly Dictionary<ViewMenuOrganizationMode, Func<IViewMenuOrganizationStrategy>> _strategyFactories;

    // Segoe MDL2 font for icons
    private static readonly FontFamily SegoeIcons = new("Segoe MDL2 Assets");

    public ViewMenuOrganizer(
        MenuItem viewMenuItem,
        MenuAdapter menuAdapter,
        AppSettingsService settingsService,
        Func<string, string?> dockSideResolver)
    {
        _viewMenuItem     = viewMenuItem ?? throw new ArgumentNullException(nameof(viewMenuItem));
        _menuAdapter      = menuAdapter ?? throw new ArgumentNullException(nameof(menuAdapter));
        _settingsService  = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _dockSideResolver = dockSideResolver ?? throw new ArgumentNullException(nameof(dockSideResolver));

        _strategyFactories = new()
        {
            [ViewMenuOrganizationMode.Flat]        = () => new FlatOrganizationStrategy(),
            [ViewMenuOrganizationMode.Categorized] = () => new CategorizedOrganizationStrategy(Settings),
            [ViewMenuOrganizationMode.ByDockSide]  = () => new DockSideOrganizationStrategy(Settings),
        };
    }

    private ViewMenuSettings Settings => _settingsService.Current.ViewMenu;

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Registers the Command Palette entry (always rendered first, outside categories).
    /// </summary>
    public void RegisterCommandPaletteEntry(ViewMenuEntry entry)
    {
        _commandPaletteEntry = entry;
    }

    /// <summary>
    /// Registers a built-in (hardcoded) View menu entry.
    /// </summary>
    public void RegisterBuiltInEntry(ViewMenuEntry entry)
    {
        _builtInEntries.Add(entry);
    }

    /// <summary>
    /// Fully rebuilds the View menu from scratch using the current organization mode.
    /// Designed to complete in &lt; 16ms for ~40 items.
    /// </summary>
    public void RebuildMenu()
    {
        var settings = Settings;
        var allEntries = CollectAllEntries();
        var strategy = GetStrategy(settings.Mode);
        var categories = strategy.Organize(allEntries);

        // Sort within categories
        categories = ApplySorting(categories, settings.SortOrder);

        // Rebuild WPF menu
        _viewMenuItem.Items.Clear();

        // 1. Command Palette (always first)
        if (_commandPaletteEntry is not null)
        {
            _viewMenuItem.Items.Add(BuildMenuItem(_commandPaletteEntry));
            _viewMenuItem.Items.Add(new Separator());
        }

        // 2. Pinned items (if enabled)
        if (settings.PinFavoritesToTop && settings.PinnedItemIds.Count > 0)
        {
            var pinnedCount = 0;
            foreach (var pinnedId in settings.PinnedItemIds)
            {
                var entry = FindEntry(allEntries, pinnedId);
                if (entry is null) continue;
                var mi = BuildMenuItem(entry);
                AddPinIcon(mi);
                _viewMenuItem.Items.Add(mi);
                pinnedCount++;
            }
            if (pinnedCount > 0)
                _viewMenuItem.Items.Add(new Separator());
        }

        // 3. Main content (mode-dependent)
        if (settings.Mode == ViewMenuOrganizationMode.Flat)
        {
            RenderFlat(categories, allEntries);
        }
        else
        {
            RenderCategorized(categories, settings);
        }

        // 4. Footer: "Organize By…" submenu
        _viewMenuItem.Items.Add(new Separator());
        _viewMenuItem.Items.Add(BuildOrganizeBySubmenu(settings.Mode));
    }

    /// <summary>
    /// Switches the organization mode, persists, and rebuilds.
    /// </summary>
    public void SetMode(ViewMenuOrganizationMode mode)
    {
        if (Settings.Mode == mode) return;
        Settings.Mode = mode;
        _settingsService.Save();
        RebuildMenu();
    }

    /// <summary>
    /// Pins an item to the View menu root level.
    /// </summary>
    public void PinItem(string itemId)
    {
        if (Settings.PinnedItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase)) return;
        Settings.PinnedItemIds.Add(itemId);
        _settingsService.Save();
        RebuildMenu();
    }

    /// <summary>
    /// Unpins an item from the View menu root level.
    /// </summary>
    public void UnpinItem(string itemId)
    {
        var idx = Settings.PinnedItemIds.FindIndex(
            id => string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;
        Settings.PinnedItemIds.RemoveAt(idx);
        _settingsService.Save();
        RebuildMenu();
    }

    /// <summary>
    /// Returns all View menu entries (for Command Palette "view:" prefix filtering).
    /// </summary>
    public IReadOnlyList<ViewMenuEntry> GetAllEntries()
    {
        var entries = CollectAllEntries();
        if (_commandPaletteEntry is not null)
            entries.Insert(0, _commandPaletteEntry);
        return entries;
    }

    // ── Collection ──────────────────────────────────────────────────────────

    private List<ViewMenuEntry> CollectAllEntries()
    {
        var entries = new List<ViewMenuEntry>(_builtInEntries);

        // Add plugin-contributed View items from MenuAdapter
        foreach (var (uiId, descriptor) in _menuAdapter.GetAllViewMenuItems())
        {
            var dockSide = _dockSideResolver(uiId);
            entries.Add(new ViewMenuEntry(
                Id:               uiId,
                Header:           descriptor.Header,
                GestureText:      descriptor.GestureText,
                IconGlyph:        descriptor.IconGlyph,
                Command:          descriptor.Command,
                CommandParameter: descriptor.CommandParameter,
                Group:            descriptor.Group,
                Category:         descriptor.Category,
                DockSide:         dockSide,
                ToolTip:          descriptor.ToolTip,
                IsBuiltIn:        false
            ));
        }

        return entries;
    }

    // ── Strategy ────────────────────────────────────────────────────────────

    private IViewMenuOrganizationStrategy GetStrategy(ViewMenuOrganizationMode mode)
        => _strategyFactories.TryGetValue(mode, out var factory) ? factory() : new FlatOrganizationStrategy();

    // ── Sorting ─────────────────────────────────────────────────────────────

    private IReadOnlyList<ViewMenuCategory> ApplySorting(
        IReadOnlyList<ViewMenuCategory> categories,
        ViewMenuSortOrder sortOrder)
    {
        if (sortOrder == ViewMenuSortOrder.Custom)
            return categories; // Custom order preserved as-is

        return categories.Select(cat =>
        {
            if (cat.Items.Count <= 1) return cat;

            var sorted = sortOrder switch
            {
                ViewMenuSortOrder.Alphabetical => cat.Items.OrderBy(e => StripAccessKey(e.Header), StringComparer.OrdinalIgnoreCase).ToList(),
                ViewMenuSortOrder.ByFrequency  => ApplySortByFrequency(cat.Items),
                _                              => cat.Items.ToList(),
            };

            return cat with { Items = sorted };
        }).ToList();
    }

    private List<ViewMenuEntry> ApplySortByFrequency(IReadOnlyList<ViewMenuEntry> items)
    {
        var history = _settingsService.Current.CommandPalette.CommandHistory;
        return items
            .OrderByDescending(e => history.TryGetValue(e.Header, out var rec) ? rec.Count : 0)
            .ToList();
    }

    // ── Rendering ───────────────────────────────────────────────────────────

    private void RenderFlat(IReadOnlyList<ViewMenuCategory> categories, IReadOnlyList<ViewMenuEntry> allEntries)
    {
        if (allEntries.Count == 0) return;

        var settings = Settings;

        // Group items by classified category, sorted alphabetically within each group.
        // One separator between category boundaries — clean flat layout.
        var orderedCats = ViewMenuClassifier.GetOrderedCategories();
        var grouped = allEntries
            .GroupBy(e => ViewMenuClassifier.Classify(e, settings), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g =>
            {
                // Use canonical category order so Core IDE comes first
                for (int i = 0; i < orderedCats.Count; i++)
                    if (string.Equals(orderedCats[i], g.Key, StringComparison.OrdinalIgnoreCase))
                        return i;
                return 999;
            })
            .ToList();

        var isFirst = true;
        foreach (var group in grouped)
        {
            if (!isFirst)
                _viewMenuItem.Items.Add(new Separator());
            isFirst = false;

            foreach (var entry in group.OrderBy(e => StripAccessKey(e.Header), StringComparer.OrdinalIgnoreCase))
                _viewMenuItem.Items.Add(BuildMenuItem(entry));
        }
    }

    private void RenderCategorized(IReadOnlyList<ViewMenuCategory> categories, ViewMenuSettings settings)
    {
        // Phase 1: "Core IDE" items render at root level (VS-style — essentials always visible)
        var coreCategory = categories.FirstOrDefault(c =>
            string.Equals(c.Name, ViewMenuClassifier.CoreIDE, StringComparison.OrdinalIgnoreCase));

        if (coreCategory is not null && coreCategory.Items.Count > 0)
        {
            foreach (var entry in coreCategory.Items)
                _viewMenuItem.Items.Add(BuildMenuItem(entry));
            _viewMenuItem.Items.Add(new Separator());
        }

        // Phase 2: All other categories as submenus
        foreach (var cat in categories)
        {
            // Skip Core IDE — already rendered at root
            if (string.Equals(cat.Name, ViewMenuClassifier.CoreIDE, StringComparison.OrdinalIgnoreCase))
                continue;

            if (cat.Items.Count == 0 && settings.CollapseEmptyCategories)
                continue;

            var submenu = new MenuItem { Header = cat.Name };

            if (cat.IconGlyph is not null && settings.ShowIcons)
            {
                submenu.Icon = new TextBlock
                {
                    Text                = cat.IconGlyph,
                    FontFamily          = SegoeIcons,
                    FontSize            = 13,
                    Foreground          = (Brush)Application.Current.FindResource("DockMenuForegroundBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                };
            }

            foreach (var entry in cat.Items)
                submenu.Items.Add(BuildMenuItem(entry));

            _viewMenuItem.Items.Add(submenu);
        }
    }

    // ── MenuItem Factory ────────────────────────────────────────────────────

    private MenuItem BuildMenuItem(ViewMenuEntry entry)
    {
        var settings = Settings;

        var resolvedHeader = entry.HeaderResourceKey is not null
            ? Application.Current.TryFindResource(entry.HeaderResourceKey) as string ?? entry.Header
            : entry.Header;

        var resolvedToolTip = entry.ToolTipResourceKey is not null
            ? Application.Current.TryFindResource(entry.ToolTipResourceKey) as string ?? entry.ToolTip
            : entry.ToolTip;

        var mi = new MenuItem
        {
            Header           = resolvedHeader,
            Command          = entry.Command,
            CommandParameter = entry.CommandParameter,
            ToolTip          = resolvedToolTip,
            Tag              = entry.Id, // Store ID for pin/unpin lookup
        };

        if (settings.ShowGestureText && !string.IsNullOrEmpty(entry.GestureText))
            mi.InputGestureText = entry.GestureText;

        if (settings.ShowIcons && !string.IsNullOrEmpty(entry.IconGlyph))
        {
            mi.Icon = new TextBlock
            {
                Text                = entry.IconGlyph,
                FontFamily          = SegoeIcons,
                FontSize            = 13,
                Foreground          = (Brush)Application.Current.FindResource("DockMenuForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            };
        }

        // Context menu for pin/unpin (skip for Command Palette entry)
        if (entry.Id != _commandPaletteEntry?.Id)
        {
            mi.ContextMenu = BuildPinContextMenu(entry.Id);
        }

        return mi;
    }

    // ── Pin Context Menu ────────────────────────────────────────────────────

    private ContextMenu BuildPinContextMenu(string itemId)
    {
        var isPinned = Settings.PinnedItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase);

        var ctx = new ContextMenu();

        var pinItem = new MenuItem
        {
            Header = isPinned ? AppResources.App_ViewMenu_UnpinFromViewMenu : AppResources.App_ViewMenu_PinToViewMenu,
            Icon = new TextBlock
            {
                Text       = isPinned ? "\uE77A" : "\uE718", // Unpin / Pin
                FontFamily = SegoeIcons,
                FontSize   = 12,
            },
        };

        var capturedId = itemId;
        pinItem.Click += (_, _) =>
        {
            if (isPinned)
                UnpinItem(capturedId);
            else
                PinItem(capturedId);
        };

        ctx.Items.Add(pinItem);
        return ctx;
    }

    private static void AddPinIcon(MenuItem mi)
    {
        // Prefix the existing icon with a small pin indicator
        if (mi.Icon is TextBlock existing)
        {
            // Replace with a StackPanel containing pin + original icon
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text       = "\uE718",
                FontFamily = SegoeIcons,
                FontSize   = 9,
                VerticalAlignment = VerticalAlignment.Center,
                Margin     = new Thickness(0, 0, 2, 0),
                Opacity    = 0.6,
            });
            sp.Children.Add(new TextBlock
            {
                Text                = existing.Text,
                FontFamily          = existing.FontFamily,
                FontSize            = existing.FontSize,
                Foreground          = existing.Foreground,
                HorizontalAlignment = existing.HorizontalAlignment,
                VerticalAlignment   = existing.VerticalAlignment,
            });
            mi.Icon = sp;
        }
    }

    // ── "Organize By…" Submenu ──────────────────────────────────────────────

    private MenuItem BuildOrganizeBySubmenu(ViewMenuOrganizationMode currentMode)
    {
        var submenu = new MenuItem
        {
            Header = AppResources.App_ViewMenu_OrganizeBy,
            Icon = new TextBlock
            {
                Text       = "\uE700", // GlobalNavButton
                FontFamily = SegoeIcons,
                FontSize   = 13,
                Foreground = (Brush)Application.Current.FindResource("DockMenuForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };

        submenu.Items.Add(BuildModeItem(AppResources.App_ViewMenu_Flat,        ViewMenuOrganizationMode.Flat, currentMode));
        submenu.Items.Add(BuildModeItem(AppResources.App_ViewMenu_Categorized, ViewMenuOrganizationMode.Categorized, currentMode));
        submenu.Items.Add(BuildModeItem(AppResources.App_ViewMenu_ByDockSide,  ViewMenuOrganizationMode.ByDockSide, currentMode));

        return submenu;
    }

    private MenuItem BuildModeItem(string header, ViewMenuOrganizationMode mode, ViewMenuOrganizationMode currentMode)
    {
        var mi = new MenuItem
        {
            Header      = header,
            IsCheckable = true,
            IsChecked   = mode == currentMode,
        };

        mi.Click += (_, _) => SetMode(mode);
        return mi;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ViewMenuEntry? FindEntry(IReadOnlyList<ViewMenuEntry> entries, string id)
        => entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    private static string StripAccessKey(string header)
        => header.StartsWith('_') ? header[1..] : header;
}
