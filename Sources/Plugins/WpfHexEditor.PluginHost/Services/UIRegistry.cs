//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Tracks and routes all UI contributions from plugins to the appropriate IDE adapters.
/// </summary>
public sealed class UIRegistry : IUIRegistry
{
    private readonly IDockingAdapter _dockingAdapter;
    private readonly IMenuAdapter _menuAdapter;
    private readonly IStatusBarAdapter _statusBarAdapter;

    private readonly object _lock = new();
    // Maps uiId -> registration info (pluginId + type tag)
    private readonly Dictionary<string, UIRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    // Maps pluginId -> Solution Explorer context menu contributor (one per plugin)
    private readonly Dictionary<string, ISolutionExplorerContextMenuContributor> _contextMenuContributors
        = new(StringComparer.OrdinalIgnoreCase);
    // Maps uiId -> title bar contributor (pluginId stored via _registrations)
    private readonly Dictionary<string, ITitleBarContributor> _titleBarContributors
        = new(StringComparer.OrdinalIgnoreCase);
    private int _idCounter;

    public UIRegistry(IDockingAdapter dockingAdapter, IMenuAdapter menuAdapter, IStatusBarAdapter statusBarAdapter)
    {
        _dockingAdapter = dockingAdapter ?? throw new ArgumentNullException(nameof(dockingAdapter));
        _menuAdapter = menuAdapter ?? throw new ArgumentNullException(nameof(menuAdapter));
        _statusBarAdapter = statusBarAdapter ?? throw new ArgumentNullException(nameof(statusBarAdapter));
    }

    /// <inheritdoc />
    public string GenerateUIId(string pluginId, string elementType, string elementName)
    {
        return $"{pluginId}.{elementType}.{elementName}";
    }

    /// <inheritdoc />
    public bool Exists(string uiId)
    {
        lock (_lock)
            return _registrations.ContainsKey(uiId);
    }

    /// <inheritdoc />
    public void RegisterPanel(string uiId, UIElement content, string pluginId, PanelDescriptor descriptor)
    {
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _dockingAdapter.AddDockablePanel(uiId, content, descriptor);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.Panel);
        }
    }

    /// <inheritdoc />
    public void RegisterMenuItem(string uiId, string pluginId, MenuItemDescriptor descriptor)
    {
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _menuAdapter.AddMenuItem(uiId, descriptor);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.MenuItem);
        }
    }

    /// <inheritdoc />
    public void RegisterToolbarItem(string uiId, string pluginId, ToolbarItemDescriptor descriptor)
    {
        // Toolbar items are deferred to a future toolbar adapter; record only.
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.ToolbarItem);
        }
    }

    /// <inheritdoc />
    public void UnregisterToolbarItem(string uiId)
    {
        lock (_lock)
        {
            if (!_registrations.TryGetValue(uiId, out var reg) || reg.Kind != UIElementKind.ToolbarItem) return;
            _registrations.Remove(uiId);
        }
    }

    /// <inheritdoc />
    public void RegisterDocumentTab(string uiId, UIElement content, string pluginId, DocumentDescriptor descriptor)
    {
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _dockingAdapter.AddDocumentTab(uiId, content, descriptor);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.DocumentTab);
        }
    }

    /// <inheritdoc />
    public void UnregisterDocumentTab(string uiId)
    {
        lock (_lock)
        {
            if (!_registrations.TryGetValue(uiId, out var reg) || reg.Kind != UIElementKind.DocumentTab) return;
            _dockingAdapter.RemoveDocumentTab(uiId);
            _registrations.Remove(uiId);
        }
    }

    /// <inheritdoc />
    public void RegisterStatusBarItem(string uiId, string pluginId, StatusBarItemDescriptor descriptor)
    {
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _statusBarAdapter.AddStatusBarItem(uiId, descriptor);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.StatusBarItem);
        }
    }

    /// <inheritdoc />
    public void UnregisterStatusBarItem(string uiId)
    {
        lock (_lock)
        {
            if (!_registrations.TryGetValue(uiId, out var reg) || reg.Kind != UIElementKind.StatusBarItem) return;
            _statusBarAdapter.RemoveStatusBarItem(uiId);
            _registrations.Remove(uiId);
        }
    }

    /// <inheritdoc />
    public void UnregisterPanel(string uiId)
    {
        lock (_lock)
        {
            if (!_registrations.TryGetValue(uiId, out var reg) || reg.Kind != UIElementKind.Panel) return;
            _dockingAdapter.RemoveDockablePanel(uiId);
            _registrations.Remove(uiId);
        }
    }

    /// <inheritdoc />
    public void UnregisterMenuItem(string uiId)
    {
        lock (_lock)
        {
            if (!_registrations.TryGetValue(uiId, out var reg) || reg.Kind != UIElementKind.MenuItem) return;
            _menuAdapter.RemoveMenuItem(uiId);
            _registrations.Remove(uiId);
        }
    }

    // -- Solution Explorer Context Menu Contributors --------------------------

    /// <inheritdoc />
    public void RegisterContextMenuContributor(string pluginId, ISolutionExplorerContextMenuContributor contributor)
    {
        lock (_lock)
            _contextMenuContributors[pluginId] = contributor;
    }

    /// <inheritdoc />
    public void UnregisterContextMenuContributor(string pluginId)
    {
        lock (_lock)
            _contextMenuContributors.Remove(pluginId);
    }

    /// <inheritdoc />
    public IReadOnlyList<ISolutionExplorerContextMenuContributor> GetContextMenuContributors()
    {
        lock (_lock)
            return [.. _contextMenuContributors.Values];
    }

    // -- Title Bar Registration -----------------------------------------------

    /// <inheritdoc />
    public void RegisterTitleBarItem(string uiId, string pluginId, ITitleBarContributor contributor)
    {
        lock (_lock)
        {
            _titleBarContributors[uiId] = contributor;
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.TitleBar);
        }
        TitleBarChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void UnregisterTitleBarItem(string uiId)
    {
        lock (_lock)
        {
            _titleBarContributors.Remove(uiId);
            _registrations.Remove(uiId);
        }
        TitleBarChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns all registered title bar contributors ordered by <see cref="ITitleBarContributor.Order"/>.
    /// Called by MainWindow to populate the TitleBarPluginZone.
    /// </summary>
    public IReadOnlyList<ITitleBarContributor> GetTitleBarContributors()
    {
        lock (_lock)
            return [.. _titleBarContributors.Values.OrderBy(c => c.Order)];
    }

    /// <summary>Raised when title bar contributors change (add/remove).</summary>
    public event EventHandler? TitleBarChanged;

    // -- Layout Anchors -------------------------------------------------------

    /// <summary>
    /// Delegate set by the host (MainWindow) to compute the command palette anchor point.
    /// </summary>
    public Func<Point?>? CommandPaletteAnchorProvider { get; set; }

    /// <inheritdoc />
    public Point? GetCommandPaletteAnchor() => CommandPaletteAnchorProvider?.Invoke();

    // -- Bulk Unregister (also removes contributor) ----------------------------

    /// <inheritdoc />
    public void UnregisterAllForPlugin(string pluginId)
    {
        lock (_lock)
        {
            _contextMenuContributors.Remove(pluginId);

            // Remove title bar contributors owned by this plugin
            var tbToRemove = _titleBarContributors
                .Where(kvp => _registrations.TryGetValue(kvp.Key, out var reg) && reg.PluginId == pluginId)
                .Select(kvp => kvp.Key).ToList();
            foreach (var key in tbToRemove)
                _titleBarContributors.Remove(key);

            var toRemove = _registrations
                .Where(kvp => kvp.Value.PluginId == pluginId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var uiId in toRemove)
            {
                var reg = _registrations[uiId];
                RemoveByKind(uiId, reg.Kind);
                _registrations.Remove(uiId);
            }
        }
    }

    // -- Stub activation helper -----------------------------------------------------------

    /// <summary>
    /// Returns the first <see cref="System.Windows.Input.ICommand"/> registered by
    /// <paramref name="pluginId"/> whose <see cref="MenuItemDescriptor.ParentPath"/>
    /// starts with "View". Used by the stub activation system to invoke the plugin's
    /// panel-toggle command immediately after the plugin finishes loading.
    /// </summary>
    public System.Windows.Input.ICommand? GetFirstViewCommandForPlugin(string pluginId)
    {
        var allItems = _menuAdapter.GetAllMenuItems();
        lock (_lock)
        {
            foreach (var kvp in _registrations)
            {
                if (!string.Equals(kvp.Value.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)) continue;
                if (kvp.Value.Kind != UIElementKind.MenuItem) continue;
                if (!allItems.TryGetValue(kvp.Key, out var desc)) continue;
                if (desc.ParentPath.StartsWith("View", StringComparison.OrdinalIgnoreCase)
                    && desc.Command is not null)
                    return desc.Command;
            }
        }
        return null;
    }

    // -- Panel visibility -----------------------------------------------------------------

    /// <summary>
    /// Returns true if at least one visible panel is registered by <paramref name="pluginId"/>.
    /// </summary>
    public bool HasVisiblePanelForPlugin(string pluginId)
    {
        lock (_lock)
        {
            foreach (var kvp in _registrations)
            {
                if (!string.Equals(kvp.Value.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)) continue;
                if (kvp.Value.Kind != UIElementKind.Panel) continue;
                if (_dockingAdapter.IsPanelVisible(kvp.Key)) return true;
            }
        }
        return false;
    }

    public void ShowPanel(string uiId)      => _dockingAdapter.ShowDockablePanel(uiId);
    public void HidePanel(string uiId)      => _dockingAdapter.HideDockablePanel(uiId);
    public void TogglePanel(string uiId)    => _dockingAdapter.ToggleDockablePanel(uiId);
    public void FocusPanel(string uiId)     => _dockingAdapter.FocusDockablePanel(uiId);
    public bool IsPanelVisible(string uiId) => _dockingAdapter.IsPanelVisible(uiId);

    /// <inheritdoc />
    public event EventHandler<string>? PanelShown
    {
        add    => _dockingAdapter.PanelShown += value;
        remove => _dockingAdapter.PanelShown -= value;
    }

    /// <inheritdoc />
    public event EventHandler<string>? PanelHidden
    {
        add    => _dockingAdapter.PanelHidden += value;
        remove => _dockingAdapter.PanelHidden -= value;
    }

    private void RemoveByKind(string uiId, UIElementKind kind)
    {
        switch (kind)
        {
            case UIElementKind.Panel:
                _dockingAdapter.RemoveDockablePanel(uiId);
                break;
            case UIElementKind.DocumentTab:
                _dockingAdapter.RemoveDocumentTab(uiId);
                break;
            case UIElementKind.MenuItem:
                _menuAdapter.RemoveMenuItem(uiId);
                break;
            case UIElementKind.StatusBarItem:
                _statusBarAdapter.RemoveStatusBarItem(uiId);
                break;
            case UIElementKind.ToolbarItem:
                // No adapter yet — nothing to clean up on the host side.
                break;
        }
    }

    private void ThrowIfDuplicate(string uiId)
    {
        if (_registrations.ContainsKey(uiId))
            throw new InvalidOperationException($"UI ID '{uiId}' is already registered. Each plugin UI element must have a unique ID.");
    }

    private sealed record UIRegistration(string PluginId, UIElementKind Kind);

    private enum UIElementKind
    {
        Panel,
        MenuItem,
        ToolbarItem,
        DocumentTab,
        StatusBarItem,
        TitleBar
    }
}
