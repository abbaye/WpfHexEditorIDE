//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// API for plugins to register and unregister UI elements in the IDE.
/// </summary>
public interface IUIRegistry
{
    // â”€â”€ ID Management â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Generates a unique UI element identifier following the pattern:
    /// <c>{pluginId}.{elementType}.{elementName}</c>.
    /// </summary>
    /// <param name="pluginId">Owning plugin identifier.</param>
    /// <param name="elementType">Element type (e.g. "Panel", "MenuItem", "ToolbarItem").</param>
    /// <param name="elementName">Element name within the plugin (e.g. "Main", "Export").</param>
    /// <returns>A unique UI identifier string.</returns>
    string GenerateUIId(string pluginId, string elementType, string elementName);

    /// <summary>Returns true if a UI element with this ID is already registered.</summary>
    bool Exists(string uiId);

    // â”€â”€ Panel Registration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Registers a dockable panel in the IDE.
    /// Handles duplicate ID by logging a warning and appending a suffix.
    /// </summary>
    /// <param name="uiId">Unique UI element ID (from GenerateUIId).</param>
    /// <param name="panel">WPF element to dock.</param>
    /// <param name="pluginId">Owning plugin identifier.</param>
    /// <param name="descriptor">Panel configuration (title, dock side, etc.).</param>
    void RegisterPanel(string uiId, UIElement panel, string pluginId, PanelDescriptor descriptor);

    /// <summary>Unregisters and removes a dockable panel by its UI ID.</summary>
    void UnregisterPanel(string uiId);

    // â”€â”€ Menu Registration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Registers a menu item in the IDE main menu.
    /// </summary>
    void RegisterMenuItem(string uiId, string pluginId, MenuItemDescriptor descriptor);

    /// <summary>Unregisters a menu item by its UI ID.</summary>
    void UnregisterMenuItem(string uiId);

    // â”€â”€ Toolbar Registration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Registers a toolbar button in the IDE main toolbar.</summary>
    void RegisterToolbarItem(string uiId, string pluginId, ToolbarItemDescriptor descriptor);

    /// <summary>Unregisters a toolbar button by its UI ID.</summary>
    void UnregisterToolbarItem(string uiId);

    // â”€â”€ Document Tab Registration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Registers a document tab in the central document host area.
    /// </summary>
    void RegisterDocumentTab(string uiId, UIElement content, string pluginId, DocumentDescriptor descriptor);

    /// <summary>Unregisters and closes a document tab by its UI ID.</summary>
    void UnregisterDocumentTab(string uiId);

    // â”€â”€ Status Bar Registration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Registers a status bar item in the IDE status bar.</summary>
    void RegisterStatusBarItem(string uiId, string pluginId, StatusBarItemDescriptor descriptor);

    /// <summary>Unregisters a status bar item by its UI ID.</summary>
    void UnregisterStatusBarItem(string uiId);

    // ── Panel Visibility Control ──────────────────────────────────────────────────────────

    /// <summary>Makes an already-registered panel visible in the dock layout.</summary>
    void ShowPanel(string uiId);

    /// <summary>Hides (collapses) an already-registered panel.</summary>
    void HidePanel(string uiId);

    /// <summary>Toggles visibility of a registered panel.</summary>
    void TogglePanel(string uiId);

    /// <summary>Gives keyboard focus to a registered panel.</summary>
    void FocusPanel(string uiId);

    // â”€â”€ Bulk Unregister â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Atomically unregisters and removes ALL UI elements owned by the specified plugin.
    /// Called during hot-unload to guarantee clean IDE state.
    /// </summary>
    /// <param name="pluginId">Plugin identifier whose elements to remove.</param>
    void UnregisterAllForPlugin(string pluginId);
}
