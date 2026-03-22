//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
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
    // -- ID Management --------------------------------------------------------

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

    // -- Panel Registration ---------------------------------------------------

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

    // -- Menu Registration ----------------------------------------------------

    /// <summary>
    /// Registers a menu item in the IDE main menu.
    /// </summary>
    void RegisterMenuItem(string uiId, string pluginId, MenuItemDescriptor descriptor);

    /// <summary>Unregisters a menu item by its UI ID.</summary>
    void UnregisterMenuItem(string uiId);

    // -- Toolbar Registration -------------------------------------------------

    /// <summary>Registers a toolbar button in the IDE main toolbar.</summary>
    void RegisterToolbarItem(string uiId, string pluginId, ToolbarItemDescriptor descriptor);

    /// <summary>Unregisters a toolbar button by its UI ID.</summary>
    void UnregisterToolbarItem(string uiId);

    // -- Document Tab Registration --------------------------------------------

    /// <summary>
    /// Registers a document tab in the central document host area.
    /// </summary>
    void RegisterDocumentTab(string uiId, UIElement content, string pluginId, DocumentDescriptor descriptor);

    /// <summary>Unregisters and closes a document tab by its UI ID.</summary>
    void UnregisterDocumentTab(string uiId);

    // -- Status Bar Registration ----------------------------------------------

    /// <summary>Registers a status bar item in the IDE status bar.</summary>
    void RegisterStatusBarItem(string uiId, string pluginId, StatusBarItemDescriptor descriptor);

    /// <summary>Unregisters a status bar item by its UI ID.</summary>
    void UnregisterStatusBarItem(string uiId);

    // -- Panel Visibility Control ----------------------------------------------------------

    /// <summary>Makes an already-registered panel visible in the dock layout.</summary>
    void ShowPanel(string uiId);

    /// <summary>Hides (collapses) an already-registered panel.</summary>
    void HidePanel(string uiId);

    /// <summary>Toggles visibility of a registered panel.</summary>
    void TogglePanel(string uiId);

    /// <summary>Gives keyboard focus to a registered panel.</summary>
    void FocusPanel(string uiId);

    /// <summary>
    /// Returns true if the panel is currently registered in the layout and not hidden/closed.
    /// Plugins should call this before performing expensive background I/O or analysis
    /// to avoid wasting resources when the user has closed the panel.
    /// Returns true (fail-open) when panel state cannot be determined (e.g. during startup).
    /// </summary>
    bool IsPanelVisible(string uiId);

    // -- Solution Explorer Context Menu Contributors --------------------------

    /// <summary>
    /// Registers a contributor that injects items into the Solution Explorer
    /// right-click context menu.  Only one contributor per pluginId is kept;
    /// calling again replaces the previous one.
    /// </summary>
    void RegisterContextMenuContributor(string pluginId, ISolutionExplorerContextMenuContributor contributor);

    /// <summary>Removes the context menu contributor registered for <paramref name="pluginId"/>.</summary>
    void UnregisterContextMenuContributor(string pluginId);

    /// <summary>
    /// Returns a snapshot of all currently-registered context menu contributors.
    /// Called on the UI thread during context menu opening — must be thread-safe.
    /// </summary>
    IReadOnlyList<ISolutionExplorerContextMenuContributor> GetContextMenuContributors();

    // -- Bulk Unregister ------------------------------------------------------

    /// <summary>
    /// Atomically unregisters and removes ALL UI elements owned by the specified plugin.
    /// Called during hot-unload to guarantee clean IDE state.
    /// </summary>
    /// <param name="pluginId">Plugin identifier whose elements to remove.</param>
    void UnregisterAllForPlugin(string pluginId);
}
