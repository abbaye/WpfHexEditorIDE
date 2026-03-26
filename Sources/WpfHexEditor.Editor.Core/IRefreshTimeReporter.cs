//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Opt-in interface for document editors that expose a render-time metric
/// in the IDE status bar ("Refresh: N ms").
/// The host (MainWindow) subscribes to <see cref="StatusBarItem.PropertyChanged"/>
/// on the returned item and mirrors its <see cref="StatusBarItem.Value"/> and
/// <see cref="StatusBarItem.IsVisible"/> to the right-aligned refresh-time panel.
/// </summary>
public interface IRefreshTimeReporter
{
    /// <summary>
    /// Returns the <see cref="StatusBarItem"/> that carries the latest render time.
    /// Null when the editor does not support this metric.
    /// </summary>
    StatusBarItem? RefreshTimeStatusBarItem { get; }
}
