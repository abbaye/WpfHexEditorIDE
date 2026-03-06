// ==========================================================
// Project: WpfHexEditor.SDK
// File: IThemeService.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Service providing IDE theme information and dynamic theme integration to plugins.
//     All plugin WPF controls must use this service — hardcoded colors are forbidden.
//
// Architecture Notes:
//     Plugins must NOT hardcode brush/color values. They must request theme resources
//     via GetThemeResources() and use SetResourceReference / DynamicResource.
//     RegisterThemeAwareControl() auto-updates a FrameworkElement when theme changes.
//
// ==========================================================

using System.Windows;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Provides theme integration for plugins.
/// All plugin WPF controls must respect the IDE theme via this service.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the active theme name (e.g. "DarkTheme", "LightTheme", "VS2022Dark").
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Raised when the user switches the IDE theme.
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler ThemeChanged;

    /// <summary>
    /// Returns the merged <see cref="ResourceDictionary"/> containing all
    /// active theme brushes and styles for the current theme.
    /// Plugins should merge this into their control's Resources.
    /// </summary>
    ResourceDictionary GetThemeResources();

    /// <summary>
    /// Registers a plugin <see cref="FrameworkElement"/> for automatic theme updates.
    /// The element's Resources will be re-merged when the theme changes.
    /// </summary>
    /// <param name="element">The plugin root control to keep theme-aware.</param>
    void RegisterThemeAwareControl(FrameworkElement element);

    /// <summary>
    /// Unregisters a previously registered theme-aware control.
    /// Must be called when the plugin panel is removed.
    /// </summary>
    void UnregisterThemeAwareControl(FrameworkElement element);
}
