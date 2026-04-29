// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.ToolsMenu.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Description:
//     Manages plugin-contributed Tools menu items.
//     Mirrors the ViewMenuOrganizer pattern: MenuAdapter intercepts all
//     ParentPath="Tools" descriptors; this handler rebuilds the plugin
//     section of the ToolsMenu MenuItem on the UI thread whenever the
//     set of plugin items changes.
//
// Architecture Notes:
//     Partial class of MainWindow. ToolsMenu is x:Name in MainWindow.xaml.
//     Static items (Terminal, Separator, Options) live in XAML and are
//     never touched — only the plugin section (after the separator) is rebuilt.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // Tag stamped on the separator that precedes the plugin block.
    private const string ToolsPluginSeparatorTag = "tools-plugin-sep";

    private void InitToolsMenuOrganizer()
    {
        if (_menuAdapter is null) return;
        _menuAdapter.ToolsItemsChanged += OnToolsMenuItemsChanged;
    }

    private void OnToolsMenuItemsChanged()
    {
        Dispatcher.InvokeAsync(RebuildToolsPluginItems,
            System.Windows.Threading.DispatcherPriority.Normal);
    }

    /// <summary>
    /// Rebuilds the plugin-contributed section of the Tools menu.
    /// Preserves the static XAML items (Terminal, Separator, Options) and
    /// appends a fresh separator + plugin items after them.
    /// </summary>
    private void RebuildToolsPluginItems()
    {
        if (_menuAdapter is null) return;

        var pluginItems = _menuAdapter.GetAllToolsMenuItems();

        // Remove the old plugin separator + all items that were dynamically added.
        var toRemove = ToolsMenu.Items
            .OfType<FrameworkElement>()
            .Where(fe => fe.Tag is string t && t == ToolsPluginSeparatorTag)
            .ToList();

        // Also collect any MenuItem that follows the plugin separator.
        bool inPluginBlock = false;
        foreach (var fe in ToolsMenu.Items.OfType<FrameworkElement>().ToList())
        {
            if (fe.Tag is string t && t == ToolsPluginSeparatorTag)
            {
                inPluginBlock = true;
                toRemove.Add(fe);
                continue;
            }
            if (inPluginBlock) toRemove.Add(fe);
        }

        foreach (var fe in toRemove)
            ToolsMenu.Items.Remove(fe);

        if (pluginItems.Count == 0) return;

        // Add separator that marks the start of the plugin block.
        ToolsMenu.Items.Add(new Separator { Tag = ToolsPluginSeparatorTag });

        foreach (var (_, descriptor) in pluginItems)
        {
            var item = BuildMenuItem(descriptor);
            item.Tag = ToolsPluginSeparatorTag; // reuse tag so cleanup finds all
            ToolsMenu.Items.Add(item);
        }
    }

    private static MenuItem BuildMenuItem(MenuItemDescriptor descriptor)
    {
        var item = new MenuItem
        {
            Header           = descriptor.Header,
            Command          = descriptor.Command,
            CommandParameter = descriptor.CommandParameter,
            ToolTip          = descriptor.ToolTip,
        };

        if (!string.IsNullOrEmpty(descriptor.GestureText))
            item.InputGestureText = descriptor.GestureText;

        if (!string.IsNullOrEmpty(descriptor.IconGlyph))
        {
            item.Icon = new TextBlock
            {
                Text        = descriptor.IconGlyph,
                FontFamily  = new FontFamily("Segoe MDL2 Assets"),
                FontSize    = 12,
            };
        }

        return item;
    }
}
