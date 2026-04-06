//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.PluginHost.Adapters;

/// <summary>
/// Abstracts docking engine operations for plugin panel management.
/// </summary>
public interface IDockingAdapter
{
    /// <summary>
    /// Adds a dockable panel to the IDE layout.
    /// </summary>
    /// <param name="uiId">Unique panel identifier (used as ContentId).</param>
    /// <param name="content">WPF element to dock.</param>
    /// <param name="descriptor">Panel configuration (title, dock side, etc.).</param>
    void AddDockablePanel(string uiId, UIElement content, PanelDescriptor descriptor);

    /// <summary>
    /// Removes a previously docked panel by its UI ID.
    /// </summary>
    /// <param name="uiId">Unique panel identifier to remove.</param>
    void RemoveDockablePanel(string uiId);

    /// <summary>
    /// Adds a document tab to the central document host area.
    /// </summary>
    void AddDocumentTab(string uiId, UIElement content, DocumentDescriptor descriptor);

    /// <summary>Removes a document tab by its UI ID.</summary>
    void RemoveDocumentTab(string uiId);

    /// <summary>Makes an existing dockable panel visible.</summary>
    void ShowDockablePanel(string uiId);

    /// <summary>Hides (collapses) an existing dockable panel.</summary>
    void HideDockablePanel(string uiId);

    /// <summary>Toggles visibility of an existing dockable panel.</summary>
    void ToggleDockablePanel(string uiId);

    /// <summary>Gives keyboard focus to an existing dockable panel.</summary>
    void FocusDockablePanel(string uiId);

    /// <summary>
    /// Returns true if the panel is currently registered in the layout and not hidden/closed.
    /// Docked, floating, and auto-hidden panels all return true; deferred (never-shown) or
    /// explicitly hidden panels return false.
    /// </summary>
    bool IsPanelVisible(string uiId);

    /// <summary>Raised when a panel transitions from hidden/closed to visible.</summary>
    event EventHandler<string>? PanelShown;

    /// <summary>Raised when a panel transitions from visible to hidden/closed.</summary>
    event EventHandler<string>? PanelHidden;

    // ── Tab Group extensions (default: no-op; overridden by DockingAdapter) ──

    /// <summary>Opens a new vertical tab group containing the active document.</summary>
    void SplitVertical() { }

    /// <summary>Opens a new horizontal tab group containing the active document.</summary>
    void SplitHorizontal() { }

    /// <summary>Moves the active document to the next available tab group.</summary>
    void MoveToNextTabGroup() { }

    /// <summary>Moves the active document to the previous available tab group.</summary>
    void MoveToPreviousTabGroup() { }

    /// <summary>Closes all secondary tab groups, returning documents to the primary group.</summary>
    void CloseAllTabGroups() { }

    /// <summary>Returns the number of active document tab groups (minimum 1).</summary>
    int GetTabGroupCount() => 1;
}
