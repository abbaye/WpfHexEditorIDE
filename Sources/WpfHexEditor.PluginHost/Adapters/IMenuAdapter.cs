// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: IMenuAdapter.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Adapter interface decoupling PluginHost/UIRegistry from the concrete
//     WPF menu system in WpfHexEditor.App/MainWindow.
//     Implemented by MenuAdapter in WpfHexEditor.App/Services.
//
// Architecture Notes:
//     Adapter pattern — PluginHost has no reference to App internals.
//     UIRegistry calls these methods to add/remove plugin menu contributions.
//
// ==========================================================

using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.PluginHost.Adapters;

/// <summary>
/// Abstracts menu management operations for plugin menu contributions.
/// </summary>
public interface IMenuAdapter
{
    /// <summary>
    /// Adds a menu item to the IDE menu hierarchy.
    /// </summary>
    /// <param name="uiId">Unique identifier used for later removal.</param>
    /// <param name="descriptor">Menu item configuration (header, parent path, command, etc.).</param>
    void AddMenuItem(string uiId, MenuItemDescriptor descriptor);

    /// <summary>
    /// Removes a previously added menu item by its UI ID.
    /// </summary>
    /// <param name="uiId">Unique identifier of the menu item to remove.</param>
    void RemoveMenuItem(string uiId);
}
