//////////////////////////////////////////////
// Apache 2.0  - 2026
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
}
