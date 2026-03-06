// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: IStatusBarAdapter.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Adapter interface decoupling PluginHost/UIRegistry from the concrete
//     WPF status bar in WpfHexEditor.App/MainWindow.
//     Implemented by StatusBarAdapter in WpfHexEditor.App/Services.
//
// Architecture Notes:
//     Adapter pattern — PluginHost has no reference to App internals.
//     UIRegistry calls these methods to add/remove plugin status bar contributions.
//
// ==========================================================

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
