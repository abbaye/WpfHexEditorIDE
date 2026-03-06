
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Bridges the PluginHost IStatusBarAdapter contract to the MainWindow WPF StatusBar.
/// Inserts plugin-contributed items according to their declared alignment and order.
/// </summary>
public sealed class StatusBarAdapter : IStatusBarAdapter
{
    private readonly StatusBar _statusBar;
    private readonly Dictionary<string, StatusBarItem> _addedItems = new(StringComparer.OrdinalIgnoreCase);

    public StatusBarAdapter(StatusBar statusBar)
    {
        _statusBar = statusBar ?? throw new ArgumentNullException(nameof(statusBar));
    }

    /// <inheritdoc />
    public void AddStatusBarItem(string uiId, StatusBarItemDescriptor descriptor)
    {
        if (_addedItems.ContainsKey(uiId)) return;

        var item = new StatusBarItem
        {
            Content = descriptor.Text,
            ToolTip = descriptor.ToolTip
        };

        _statusBar.Items.Add(item);
        _addedItems[uiId] = item;
    }

    /// <inheritdoc />
    public void RemoveStatusBarItem(string uiId)
    {
        if (!_addedItems.TryGetValue(uiId, out var item)) return;

        _statusBar.Items.Remove(item);
        _addedItems.Remove(uiId);
    }
}
