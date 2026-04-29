
//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Controls;
using WpfHexEditor.App.Properties;
using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Bridges the PluginHost IMenuAdapter contract to the MainWindow WPF menu.
/// Items with <c>ParentPath == "View"</c> are intercepted and stored for the
/// <see cref="ViewMenu.ViewMenuOrganizer"/> instead of being added directly to the menu.
/// All other parent paths (Tools, Debug, etc.) are handled normally.
/// </summary>
public sealed class MenuAdapter : IMenuAdapter
{
    private readonly Menu _mainMenu;

    // uiId → added MenuItem (non-View items only)
    private readonly Dictionary<string, MenuItem> _addedItems = new(StringComparer.OrdinalIgnoreCase);

    // uiId → original descriptor (all items, for Command Palette enumeration)
    private readonly Dictionary<string, MenuItemDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);

    // uiId → original descriptor (View-parented items only, for ViewMenuOrganizer)
    private readonly Dictionary<string, MenuItemDescriptor> _viewDescriptors = new(StringComparer.OrdinalIgnoreCase);

    // uiId → original descriptor (Debug-parented items only, for DebugMenuOrganizer)
    private readonly Dictionary<string, MenuItemDescriptor> _debugDescriptors = new(StringComparer.OrdinalIgnoreCase);

    // (normalised parentPath + group) → Separator element that heads that group block
    private readonly Dictionary<string, Separator> _groupSeparators = new(StringComparer.OrdinalIgnoreCase);

    // uiIds in each group block — used to clean up separators when a group is emptied
    private readonly Dictionary<string, List<string>> _groupMembers = new(StringComparer.OrdinalIgnoreCase);

    public MenuAdapter(Menu mainMenu)
    {
        _mainMenu = mainMenu ?? throw new ArgumentNullException(nameof(mainMenu));
    }

    /// <inheritdoc />
    public event Action? ViewItemsChanged;

    /// <inheritdoc />
    public event Action? DebugItemsChanged;

    /// <inheritdoc />
    public void AddMenuItem(string uiId, MenuItemDescriptor descriptor)
    {
        if (_descriptors.ContainsKey(uiId)) return;

        _descriptors[uiId] = descriptor;

        // View-parented items are intercepted — store but do not create WPF MenuItems.
        // The ViewMenuOrganizer will build the View menu dynamically.
        if (IsViewParent(descriptor.ParentPath))
        {
            _viewDescriptors[uiId] = descriptor;
            ViewItemsChanged?.Invoke();
            return;
        }

        // Debug-parented items are intercepted — store but do not create WPF MenuItems.
        // The DebugMenuOrganizer will build the Debug menu dynamically.
        if (IsDebugParent(descriptor.ParentPath))
        {
            _debugDescriptors[uiId] = descriptor;
            DebugItemsChanged?.Invoke();
            return;
        }

        // Non-View items: create WPF MenuItem and add to parent as before
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
        _descriptors.Remove(uiId);

        // View-parented item?
        if (_viewDescriptors.Remove(uiId))
        {
            ViewItemsChanged?.Invoke();
            return;
        }

        // Debug-parented item?
        if (_debugDescriptors.Remove(uiId))
        {
            DebugItemsChanged?.Invoke();
            return;
        }

        // Non-View item: remove WPF MenuItem
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

    /// <inheritdoc />
    public IReadOnlyDictionary<string, MenuItemDescriptor> GetAllMenuItems() => _descriptors;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, MenuItemDescriptor> GetAllViewMenuItems() => _viewDescriptors;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, MenuItemDescriptor> GetAllDebugMenuItems() => _debugDescriptors;

    private static bool IsViewParent(string parentPath)
        => string.Equals(parentPath?.TrimStart('_'), "View", StringComparison.OrdinalIgnoreCase);

    private static bool IsDebugParent(string parentPath)
        => string.Equals(parentPath?.TrimStart('_'), "Debug", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps canonical (English) top-level menu names to their localized equivalents.
    /// Used to merge plugin-contributed items (which always use canonical English names)
    /// into the correctly localized main menu group rather than creating a duplicate entry.
    /// </summary>
    private static readonly Dictionary<string, Func<string>> _canonicalToLocalized =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["File"]       = () => AppResources.App_Menu_File,
            ["Edit"]       = () => AppResources.App_Menu_Edit,
            ["View"]       = () => AppResources.App_Menu_View,
            ["Build"]      = () => AppResources.App_Menu_Build,
            ["Debug"]      = () => AppResources.App_Menu_Debug,
            ["Tools"]      = () => AppResources.App_Menu_Tools,
        };

    private ItemsControl FindOrCreateParent(string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath)) return _mainMenu;

        var canonical = parentPath.TrimStart('_');

        // Resolve localized label for this canonical name (if known).
        var localizedLabel = _canonicalToLocalized.TryGetValue(canonical, out var factory)
            ? factory()
            : canonical;

        foreach (var topItem in _mainMenu.Items.OfType<MenuItem>())
        {
            // Strip leading underscore (WPF access key prefix, e.g. "_View" → "View").
            var headerText = topItem.Header?.ToString()?.TrimStart('_') ?? string.Empty;

            // Match against localized label first (e.g. "Outils"), then canonical (e.g. "Tools").
            if (string.Equals(headerText, localizedLabel, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(headerText, canonical,       StringComparison.OrdinalIgnoreCase))
                return topItem;
        }

        // Parent not found — create a new top-level menu group using the localized label.
        var newParent = new MenuItem { Header = localizedLabel };
        _mainMenu.Items.Add(newParent);
        return newParent;
    }
}
