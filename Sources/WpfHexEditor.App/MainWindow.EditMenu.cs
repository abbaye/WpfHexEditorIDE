// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.EditMenu.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Description:
//     Manages plugin-contributed Edit menu items.
//     Mirrors the ToolsMenuOrganizer pattern: MenuAdapter intercepts all
//     ParentPath="Edit" descriptors; this handler rebuilds the plugin
//     section of the EditMenu MenuItem on the UI thread whenever the
//     set of plugin items changes.
//
// Architecture Notes:
//     Partial class of MainWindow. EditMenu is x:Name in MainWindow.xaml.
//     Static items live in XAML and are never touched — only the plugin
//     section (appended after the last separator) is rebuilt.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    private const string EditPluginSeparatorTag = "edit-plugin-sep";

    private void InitEditMenuOrganizer()
    {
        if (_menuAdapter is null) return;
        _menuAdapter.EditItemsChanged += OnEditMenuItemsChanged;
    }

    private void OnEditMenuItemsChanged()
    {
        Dispatcher.InvokeAsync(RebuildEditPluginItems,
            System.Windows.Threading.DispatcherPriority.Normal);
    }

    private void RebuildEditPluginItems()
    {
        if (_menuAdapter is null) return;

        var pluginItems = _menuAdapter.GetAllEditMenuItems();

        var toRemove = EditMenu.Items
            .OfType<FrameworkElement>()
            .Where(fe => fe.Tag is string t && t == EditPluginSeparatorTag)
            .ToList();

        bool inPluginBlock = false;
        foreach (var fe in EditMenu.Items.OfType<FrameworkElement>().ToList())
        {
            if (fe.Tag is string t && t == EditPluginSeparatorTag)
            {
                inPluginBlock = true;
                toRemove.Add(fe);
                continue;
            }
            if (inPluginBlock) toRemove.Add(fe);
        }

        foreach (var fe in toRemove)
            EditMenu.Items.Remove(fe);

        if (pluginItems.Count == 0) return;

        EditMenu.Items.Add(new Separator { Tag = EditPluginSeparatorTag });

        foreach (var (_, descriptor) in pluginItems)
        {
            var item = BuildEditMenuItem(descriptor);
            item.Tag = EditPluginSeparatorTag;
            EditMenu.Items.Add(item);
        }
    }

    private static MenuItem BuildEditMenuItem(MenuItemDescriptor descriptor)
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
