//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.PluginHost.Adapters;

/// <summary>
/// Abstracts menu management operations for plugin menu contributions.
/// </summary>
public interface IMenuAdapter
{
    /// <summary>
    /// Adds a menu item to the IDE menu hierarchy.
    /// </summary>
    /// <param name="uiId">Unique identifier used for later removal.</param>
    /// <param name="descriptor">Menu item configuration (header, parent path, command, etc.).</param>
    void AddMenuItem(string uiId, MenuItemDescriptor descriptor);

    /// <summary>
    /// Removes a previously added menu item by its UI ID.
    /// </summary>
    /// <param name="uiId">Unique identifier of the menu item to remove.</param>
    void RemoveMenuItem(string uiId);

    /// <summary>
    /// Returns a snapshot of all currently registered menu item descriptors keyed by their UI ID.
    /// Used by the Command Palette to enumerate plugin-contributed commands.
    /// </summary>
    IReadOnlyDictionary<string, MenuItemDescriptor> GetAllMenuItems();

    /// <summary>
    /// Raised when a View-parented menu item is added or removed.
    /// The <see cref="ViewMenuOrganizer"/> subscribes to trigger a rebuild.
    /// Default implementation is a no-op for backward compatibility.
    /// </summary>
    event Action? ViewItemsChanged { add { } remove { } }

    /// <summary>
    /// Returns only the View-parented menu item descriptors (intercepted for dynamic organization).
    /// Default implementation returns an empty dictionary for backward compatibility.
    /// </summary>
    IReadOnlyDictionary<string, MenuItemDescriptor> GetAllViewMenuItems()
        => new Dictionary<string, MenuItemDescriptor>();

    /// <summary>
    /// Raised when a Debug-parented menu item is added or removed.
    /// The <see cref="DebugMenuOrganizer"/> subscribes to trigger a rebuild.
    /// Default implementation is a no-op for backward compatibility.
    /// </summary>
    event Action? DebugItemsChanged { add { } remove { } }

    /// <summary>
    /// Returns only the Debug-parented menu item descriptors (intercepted for dynamic organization).
    /// Default implementation returns an empty dictionary for backward compatibility.
    /// </summary>
    IReadOnlyDictionary<string, MenuItemDescriptor> GetAllDebugMenuItems()
        => new Dictionary<string, MenuItemDescriptor>();
}
