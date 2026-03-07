
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Controls;
using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Bridges the PluginHost IMenuAdapter contract to the MainWindow WPF menu.
/// Supports top-level "Tools", "View", "Edit" parent paths; creates new
/// top-level menus if the parent path is unrecognised.
/// </summary>
public sealed class MenuAdapter : IMenuAdapter
{
    private readonly Menu _mainMenu;

    // uiId → added MenuItem
    private readonly Dictionary<string, MenuItem> _addedItems = new(StringComparer.OrdinalIgnoreCase);

    // (normalised parentPath + group) → Separator element that heads that group block
    private readonly Dictionary<string, Separator> _groupSeparators = new(StringComparer.OrdinalIgnoreCase);

    // uiIds in each group block — used to clean up separators when a group is emptied
    private readonly Dictionary<string, List<string>> _groupMembers = new(StringComparer.OrdinalIgnoreCase);

    public MenuAdapter(Menu mainMenu)
    {
        _mainMenu = mainMenu ?? throw new ArgumentNullException(nameof(mainMenu));
    }

    /// <inheritdoc />
    public void AddMenuItem(string uiId, MenuItemDescriptor descriptor)
    {
        if (_addedItems.ContainsKey(uiId)) return;

        var item = new MenuItem
        {
            Header             = descriptor.Header,
            Command            = descriptor.Command,
            CommandParameter   = descriptor.CommandParameter,
            ToolTip            = descriptor.ToolTip
        };

        if (!string.IsNullOrEmpty(descriptor.GestureText))
            item.InputGestureText = descriptor.GestureText;

        if (!string.IsNullOrEmpty(descriptor.IconGlyph))
        {
            item.Icon = new TextBlock
            {
                Text       = descriptor.IconGlyph,
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize   = 12
            };
        }

        var parent = FindOrCreateParent(descriptor.ParentPath);

        if (!string.IsNullOrEmpty(descriptor.Group))
        {
            var groupKey = $"{descriptor.ParentPath}|{descriptor.Group}";

            if (!_groupSeparators.ContainsKey(groupKey))
            {
                // First item for this group — prepend a separator to start the block.
                var sep = new Separator();
                parent.Items.Add(sep);
                _groupSeparators[groupKey] = sep;
                _groupMembers[groupKey]    = new List<string>();
            }

            parent.Items.Add(item);
            _groupMembers[groupKey].Add(uiId);
        }
        else if (descriptor.InsertPosition >= 0 && descriptor.InsertPosition < parent.Items.Count)
        {
            parent.Items.Insert(descriptor.InsertPosition, item);
        }
        else
        {
            parent.Items.Add(item);
        }

        _addedItems[uiId] = item;
    }

    /// <inheritdoc />
    public void RemoveMenuItem(string uiId)
    {
        if (!_addedItems.TryGetValue(uiId, out var item)) return;

        if (item.Parent is ItemsControl parent)
            parent.Items.Remove(item);

        _addedItems.Remove(uiId);

        // Remove the group separator if this was the last item in the group.
        foreach (var kv in _groupMembers)
        {
            if (!kv.Value.Remove(uiId)) continue;
            if (kv.Value.Count == 0
                && _groupSeparators.TryGetValue(kv.Key, out var sep)
                && sep.Parent is ItemsControl sepParent)
            {
                sepParent.Items.Remove(sep);
                _groupSeparators.Remove(kv.Key);
                _groupMembers.Remove(kv.Key);
            }
            break;
        }
    }

    private ItemsControl FindOrCreateParent(string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath)) return _mainMenu;

        foreach (var topItem in _mainMenu.Items.OfType<MenuItem>())
        {
            // Strip leading underscore (WPF access key prefix, e.g. "_View" → "View").
            var headerText = topItem.Header?.ToString()?.TrimStart('_') ?? string.Empty;
            if (string.Equals(headerText, parentPath.TrimStart('_'), StringComparison.OrdinalIgnoreCase))
                return topItem;
        }

        // Parent not found — create a new top-level menu group.
        var newParent = new MenuItem { Header = parentPath };
        _mainMenu.Items.Add(newParent);
        return newParent;
    }
}
