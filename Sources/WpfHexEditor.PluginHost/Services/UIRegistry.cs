//////////////////////////////////////////////
// Apache 2.0  - 2026
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

    /// <inheritdoc />
    public void UnregisterAllForPlugin(string pluginId)
    {
        lock (_lock)
        {
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

    // -- Panel visibility -----------------------------------------------------------------

    public void ShowPanel(string uiId)      => _dockingAdapter.ShowDockablePanel(uiId);
    public void HidePanel(string uiId)      => _dockingAdapter.HideDockablePanel(uiId);
    public void TogglePanel(string uiId)    => _dockingAdapter.ToggleDockablePanel(uiId);
    public void FocusPanel(string uiId)     => _dockingAdapter.FocusDockablePanel(uiId);
    public bool IsPanelVisible(string uiId) => _dockingAdapter.IsPanelVisible(uiId);

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
                // No adapter yet â€” nothing to clean up on the host side.
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
        StatusBarItem
    }
}
