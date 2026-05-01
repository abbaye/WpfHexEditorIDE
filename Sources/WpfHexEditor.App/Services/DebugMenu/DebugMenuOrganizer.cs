//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : DebugMenuOrganizer.cs
// Description  : Central orchestrator for the dynamic Debug menu.
//                Collects all Debug-menu-eligible items (built-in + plugin),
//                groups them by fixed logical sections, and rebuilds the WPF menu.
// Architecture : Single-responsibility service. Simplified variant of ViewMenuOrganizer
//                with fixed group ordering (no strategies/pins/classifier needed).
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Services.DebugMenu;

/// <summary>
/// Orchestrates the dynamic Debug menu:
/// collects entries, groups them by fixed section order,
/// and rebuilds the WPF <see cref="MenuItem"/> tree.
/// </summary>
public sealed class DebugMenuOrganizer
{
    private readonly MenuItem _debugMenuItem;
    private readonly MenuAdapter _menuAdapter;

    // Built-in entries registered by MainWindow.DebugMenu.cs
    private readonly List<DebugMenuEntry> _builtInEntries = [];

    // Segoe MDL2 font for icons
    private static readonly FontFamily SegoeIcons = new("Segoe MDL2 Assets");

    // Fixed group rendering order
    private static readonly string[] GroupOrder = ["Session", "Stepping", "Breakpoints", "Panels"];

    public DebugMenuOrganizer(MenuItem debugMenuItem, MenuAdapter menuAdapter)
    {
        _debugMenuItem = debugMenuItem ?? throw new ArgumentNullException(nameof(debugMenuItem));
        _menuAdapter   = menuAdapter ?? throw new ArgumentNullException(nameof(menuAdapter));
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a built-in (hardcoded) Debug menu entry.
    /// </summary>
    public void RegisterBuiltInEntry(DebugMenuEntry entry)
    {
        _builtInEntries.Add(entry);
    }

    /// <summary>
    /// Fully rebuilds the Debug menu from scratch.
    /// Groups entries by fixed section order with separators between groups.
    /// </summary>
    public void RebuildMenu()
    {
        var allEntries = CollectAllEntries();

        _debugMenuItem.Items.Clear();

        var isFirst = true;
        foreach (var group in GroupOrder)
        {
            var items = allEntries
                .Where(e => string.Equals(e.Group, group, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (items.Count == 0) continue;

            if (!isFirst)
                _debugMenuItem.Items.Add(new Separator());
            isFirst = false;

            foreach (var entry in items)
                _debugMenuItem.Items.Add(BuildMenuItem(entry));
        }

        // Any entries with unrecognized groups go at the end
        var knownGroups = new HashSet<string>(GroupOrder, StringComparer.OrdinalIgnoreCase);
        var extras = allEntries.Where(e => !knownGroups.Contains(e.Group)).ToList();
        if (extras.Count > 0)
        {
            _debugMenuItem.Items.Add(new Separator());
            foreach (var entry in extras)
                _debugMenuItem.Items.Add(BuildMenuItem(entry));
        }
    }

    // ── Collection ──────────────────────────────────────────────────────────

    private List<DebugMenuEntry> CollectAllEntries()
    {
        var entries = new List<DebugMenuEntry>(_builtInEntries);

        // Collect built-in headers for dedup (strip access key underscores)
        var builtInHeaders = new HashSet<string>(
            _builtInEntries.Select(e => StripAccessKey(e.Header)),
            StringComparer.OrdinalIgnoreCase);

        // Add plugin-contributed Debug items from MenuAdapter (dedup by header)
        foreach (var (uiId, descriptor) in _menuAdapter.GetAllDebugMenuItems())
        {
            var strippedHeader = StripAccessKey(descriptor.Header);
            if (builtInHeaders.Contains(strippedHeader))
                continue; // Built-in always wins — skip duplicate plugin item

            entries.Add(new DebugMenuEntry(
                Id:               uiId,
                Header:           descriptor.Header,
                GestureText:      descriptor.GestureText,
                IconGlyph:        descriptor.IconGlyph,
                Command:          descriptor.Command,
                CommandParameter: descriptor.CommandParameter,
                Group:            descriptor.Group ?? "Panels", // Default unrecognized to Panels
                ToolTip:          descriptor.ToolTip,
                IsBuiltIn:        false
            ));
        }

        return entries;
    }

    // ── MenuItem Factory ────────────────────────────────────────────────────

    private static MenuItem BuildMenuItem(DebugMenuEntry entry)
    {
        var resolvedHeader = entry.HeaderResourceKey is not null
            ? Application.Current.TryFindResource(entry.HeaderResourceKey) as string ?? entry.Header
            : entry.Header;

        var mi = new MenuItem
        {
            Header           = resolvedHeader,
            Command          = entry.Command,
            CommandParameter = entry.CommandParameter,
            ToolTip          = entry.ToolTip,
            Tag              = entry.Id,
        };

        if (!string.IsNullOrEmpty(entry.GestureText))
            mi.InputGestureText = entry.GestureText;

        if (!string.IsNullOrEmpty(entry.IconGlyph))
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

        return mi;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string StripAccessKey(string header)
        => header.Replace("_", string.Empty);
}
