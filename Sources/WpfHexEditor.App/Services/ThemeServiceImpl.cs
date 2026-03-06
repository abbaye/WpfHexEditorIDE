
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Adapts the App theme system to the IThemeService SDK contract.
/// MainWindow calls NotifyThemeChanged when the user switches themes.
/// </summary>
public sealed class ThemeServiceImpl : IThemeService
{
    private string _currentTheme = "Dark";

    public string CurrentTheme => _currentTheme;

    public event EventHandler<string>? ThemeChanged;

    public ResourceDictionary? GetThemeResources()
        => Application.Current?.Resources;

    public void RegisterThemeAwareControl(FrameworkElement element)
    {
        // Resources merge automatically through the WPF resource tree.
        // This hook is available for future fine-grained theme injection.
    }

    public void UnregisterThemeAwareControl(FrameworkElement element)
    {
        // No-op in the current tree-based merge model.
    }

    /// <summary>Called by MainWindow when the active theme changes.</summary>
    public void NotifyThemeChanged(string themeName)
    {
        _currentTheme = themeName;
        ThemeChanged?.Invoke(this, themeName);
    }
}
