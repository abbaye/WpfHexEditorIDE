//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Opt-in interface for document editors that expose interactive status bar items.
/// The host (e.g. MainWindow) binds its status bar ItemsControl to
/// <see cref="StatusBarItems"/> when the editor implementing this interface is active.
/// Editors that do not implement this interface simply leave the status bar empty.
/// </summary>
public interface IStatusBarContributor
{
    ObservableCollection<StatusBarItem> StatusBarItems { get; }

    /// <summary>
    /// Called by the host when this editor becomes the active document tab.
    /// Implementations must refresh all status bar item values to reflect current state.
    /// This ensures no stale values are shown after a tab switch.
    /// </summary>
    void RefreshStatusBarItems();
}
