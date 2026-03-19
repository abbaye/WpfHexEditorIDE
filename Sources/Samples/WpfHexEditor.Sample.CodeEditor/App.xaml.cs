// ==========================================================
// Project: WpfHexEditor.Sample.CodeEditor
// File: App.xaml.cs
// Author: Auto
// Created: 2026-03-18
// Description:
//     Application class. Exposes SwitchTheme(name) to swap the active
//     theme ResourceDictionary at runtime without restarting the app.
//     Maintains a name->URI dictionary for all available themes.
//
// Architecture Notes:
//     Pattern: Strategy (theme selection via name key → URI).
//     The active theme is identified by its Source URI containing "Theme".
//     Only one theme ResourceDictionary is active at a time.
// ==========================================================

using System.Windows;

namespace WpfHexEditor.Sample.CodeEditor;

public partial class App : Application
{
    // -- Theme name → pack URI map ------------------------------------------------

    /// <summary>All available themes keyed by their canonical name.</summary>
    public static readonly IReadOnlyDictionary<string, Uri> Themes = new Dictionary<string, Uri>
    {
        ["DarkTheme"]       = new("pack://application:,,,/WpfHexEditor.Shell;component/Themes/DarkTheme.xaml"),
        ["OfficeTheme"]     = new("pack://application:,,,/WpfHexEditor.Shell;component/Themes/OfficeTheme.xaml"),
        ["VS2022DarkTheme"] = new("pack://application:,,,/WpfHexEditor.Shell;component/Themes/VS2022DarkTheme.xaml"),
        ["MinimalTheme"]    = new("pack://application:,,,/WpfHexEditor.Shell;component/Themes/MinimalTheme.xaml"),
        ["CyberpunkTheme"]  = new("pack://application:,,,/WpfHexEditor.Shell;component/Themes/CyberpunkTheme.xaml"),
    };

    // -- Theme switching ----------------------------------------------------------

    /// <summary>
    /// Replaces the active theme ResourceDictionary with the one identified by
    /// <paramref name="themeName"/>. All DynamicResource bindings update automatically.
    /// </summary>
    /// <param name="themeName">Key in <see cref="Themes"/>, e.g. "DarkTheme".</param>
    public static void SwitchTheme(string themeName)
    {
        if (!Themes.TryGetValue(themeName, out var uri))
            return;

        var merged = Current.Resources.MergedDictionaries;

        // Remove the existing theme (identified by Source URI containing "Theme").
        var existing = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Theme", StringComparison.OrdinalIgnoreCase) == true);

        if (existing is not null)
            merged.Remove(existing);

        merged.Add(new ResourceDictionary { Source = uri });
    }
}
