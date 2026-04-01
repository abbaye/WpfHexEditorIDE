//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.PluginHost.Adapters;

/// <summary>
/// Abstracts status bar management operations for plugin status bar contributions.
/// </summary>
public interface IStatusBarAdapter
{
    /// <summary>
    /// Adds a status bar item to the IDE status bar.
    /// </summary>
    /// <param name="uiId">Unique identifier used for later removal.</param>
    /// <param name="descriptor">Status bar item configuration (text, alignment, order).</param>
    void AddStatusBarItem(string uiId, StatusBarItemDescriptor descriptor);

    /// <summary>
    /// Removes a previously added status bar item by its UI ID.
    /// </summary>
    /// <param name="uiId">Unique identifier of the status bar item to remove.</param>
    void RemoveStatusBarItem(string uiId);
}
