// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: UIRegistry.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Implementation of IUIRegistry. Manages all UI contributions from plugins
//     (panels, menu items, toolbar items, document tabs, status bar items).
//     Delegates to adapter interfaces that are implemented by App/Services.
//     Thread-safe; all mutations are serialized via a lock.
//
// Architecture Notes:
//     Adapter pattern — delegates to IDockingAdapter, IMenuAdapter, IStatusBarAdapter.
//     Hot-load safe — UnregisterAllForPlugin removes all contributions atomically.
//     ID collision detection prevents duplicate registrations.
//
// ==========================================================

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
    public void RegisterPanel(string uiId, UIElement content, PanelDescriptor descriptor, string pluginId)
    {
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _dockingAdapter.AddDockablePanel(uiId, content, descriptor);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.Panel);
        }
    }

    /// <inheritdoc />
    public void RegisterMenuItem(string uiId, MenuItemDescriptor descriptor, string pluginId)
    {
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _menuAdapter.AddMenuItem(uiId, descriptor);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.MenuItem);
        }
    }

    /// <inheritdoc />
    public void RegisterToolbarItem(string uiId, ToolbarItemDescriptor descriptor, string pluginId)
    {
        // Toolbar items are deferred to a future toolbar adapter; record only.
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.ToolbarItem);
        }
    }

    /// <inheritdoc />
    public void RegisterDocumentTab(string uiId, UIElement content, DocumentDescriptor descriptor, string pluginId)
    {
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _dockingAdapter.AddDocumentTab(uiId, content, descriptor);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.DocumentTab);
        }
    }

    /// <inheritdoc />
    public void RegisterStatusBarItem(string uiId, StatusBarItemDescriptor descriptor, string pluginId)
    {
        lock (_lock)
        {
            ThrowIfDuplicate(uiId);
            _statusBarAdapter.AddStatusBarItem(uiId, descriptor);
            _registrations[uiId] = new UIRegistration(pluginId, UIElementKind.StatusBarItem);
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
        StatusBarItem
    }
}
