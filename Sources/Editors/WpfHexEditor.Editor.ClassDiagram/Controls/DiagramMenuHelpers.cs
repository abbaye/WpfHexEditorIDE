// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramMenuHelpers.cs
// Description:
//     Shared context-menu factory helpers used by DiagramCanvas and
//     DiagramMinimapControl. Keeps styled menu creation in one place.
// Architecture Notes:
//     Static helpers only — no state, no WPF dependency.
//     MakeItem and StyledMenu are the canonical factory methods for
//     all context menus in the class diagram editor.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

internal static class DiagramMenuHelpers
{
    /// <summary>
    /// Creates a styled ContextMenu with theme-driven background/foreground and
    /// MousePoint placement so it opens at the cursor.
    /// </summary>
    public static ContextMenu StyledMenu(FrameworkElement? placementTarget = null)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = placementTarget,
            Placement       = PlacementMode.MousePoint,
        };
        menu.SetResourceReference(Control.BackgroundProperty, "DockMenuBackgroundBrush");
        menu.SetResourceReference(Control.ForegroundProperty, "DockMenuForegroundBrush");
        return menu;
    }

    /// <summary>
    /// Creates a MenuItem with a Segoe MDL2 Assets icon glyph and a click handler.
    /// Pass <c>null</c> for <paramref name="action"/> to get a submenu parent item.
    /// </summary>
    public static MenuItem MakeItem(string icon, string header, Action? action)
    {
        var iconBlock = new TextBlock
        {
            Text              = icon,
            FontFamily        = new FontFamily("Segoe MDL2 Assets, Segoe UI Symbol"),
            FontSize          = 13,
            Width             = 16,
            TextAlignment     = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        iconBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var item = new MenuItem { Header = header, Icon = iconBlock };
        if (action is not null)
            item.Click += (_, _) => action();
        return item;
    }
}
