// ==========================================================
// Project: WpfHexEditor.Sample.Terminal
// File: App.xaml.cs
// Author: Auto
// Created: 2026-03-08
// Description:
//     Application class. Exposes SwitchTheme() to swap the active
//     theme ResourceDictionary at runtime without restarting the app.
//
// Architecture Notes:
//     Pattern: Strategy (theme selection via URI).
//     The active theme is identified by its Source URI containing "Theme".
//     Only one theme ResourceDictionary is active at a time.
// ==========================================================

using System.Windows;

namespace WpfHexEditor.Sample.Terminal;

public partial class App : Application
{
    /// <summary>
    /// Replaces the active theme ResourceDictionary with the one at <paramref name="themeUri"/>.
    /// All DynamicResource bindings in the application update automatically.
    /// </summary>
    /// <param name="themeUri">
    /// Pack URI of the theme, e.g.
    /// <c>pack://application:,,,/WpfHexEditor.Shell;component/Themes/OfficeTheme.xaml</c>
    /// </param>
    public static void SwitchTheme(Uri themeUri)
    {
        var merged = Current.Resources.MergedDictionaries;

        // Remove the existing theme (identified by its source URI containing "Theme").
        var existing = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Theme", StringComparison.OrdinalIgnoreCase) == true);

        if (existing is not null)
            merged.Remove(existing);

        merged.Add(new ResourceDictionary { Source = themeUri });
    }
}
