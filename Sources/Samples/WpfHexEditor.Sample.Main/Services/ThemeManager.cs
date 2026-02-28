//////////////////////////////////////////////
// Apache 2.0  2026
// HexEditor V2 - Theme Manager Service
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Sample.Main.Services
{
    /// <summary>
    /// Manages dynamic theme changes for the application without requiring restart.
    /// Provides event notification when theme changes so UI can update dynamically.
    /// Mirrors the DynamicResourceManager pattern for themes.
    /// </summary>
    public static class ThemeManager
    {
        /// <summary>
        /// Event raised when the application theme is changed.
        /// Subscribe to this event to refresh UI elements.
        /// </summary>
        public static event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        /// <summary>
        /// Gets the current application theme.
        /// </summary>
        public static string CurrentTheme { get; private set; } = "Office";

        /// <summary>
        /// Changes the application theme and notifies all subscribers.
        /// This allows for instant theme switching without application restart.
        /// </summary>
        /// <param name="themeName">The name of the theme to apply (e.g., "Office", "DarkGlass")</param>
        /// <param name="persistent">If true, saves the theme to user settings</param>
        public static void ChangeTheme(string themeName, bool persistent = true)
        {
            if (string.IsNullOrEmpty(themeName))
                throw new ArgumentNullException(nameof(themeName));

            var oldTheme = CurrentTheme;

            try
            {
                // Remove existing theme from merged dictionaries
                var existingTheme = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Themes/"));

                if (existingTheme != null)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
                }

                // Load new theme ResourceDictionary
                var themeUri = new Uri(
                    $"pack://application:,,,/WpfHexEditor.Sample.Main;component/Resources/Themes/{themeName}.xaml",
                    UriKind.Absolute);
                var themeDictionary = new ResourceDictionary { Source = themeUri };

                // Add to merged dictionaries
                Application.Current.Resources.MergedDictionaries.Add(themeDictionary);

                // Update current theme
                CurrentTheme = themeName;

                // Save to user settings if persistent
                if (persistent)
                {
                    Properties.Settings.Default.PreferredTheme = themeName;
                    Properties.Settings.Default.Save();
                }

                // Notify all subscribers that theme has changed
                ThemeChanged?.Invoke(null, new ThemeChangedEventArgs(oldTheme, themeName));
            }
            catch (Exception ex)
            {
                // If the requested theme fails, try to fallback to Office
                if (themeName != "Office")
                {
                    ChangeTheme("Office", persistent: false);
                }
                else
                {
                    throw new InvalidOperationException($"Failed to load default theme 'Office': {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Initializes the theme from user settings or default.
        /// Should be called at application startup.
        /// </summary>
        public static void Initialize()
        {
            var themeName = Properties.Settings.Default.PreferredTheme;

            if (string.IsNullOrEmpty(themeName))
            {
                // Use default theme
                themeName = "Office";
            }

            try
            {
                ChangeTheme(themeName, persistent: false); // Don't save again, already loaded from settings
            }
            catch (Exception ex)
            {
                // Fallback to Office is handled in ChangeTheme
            }
        }

        /// <summary>
        /// Synchronizes HexEditor control colors with the current theme.
        /// Delegates to HexEditor.ApplyThemeFromResources() which reads HexEditor_* keys
        /// from Application.Current.Resources.
        /// </summary>
        /// <param name="hexEditor">The HexEditor control to synchronize</param>
        public static void SyncHexEditorColors(WpfHexEditor.HexEditor.HexEditor hexEditor)
        {
            hexEditor?.ApplyThemeFromResources();
        }
    }

    /// <summary>
    /// Event args for theme change notification.
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous theme before the change.
        /// </summary>
        public string OldTheme { get; }

        /// <summary>
        /// Gets the new theme after the change.
        /// </summary>
        public string NewTheme { get; }

        public ThemeChangedEventArgs(string oldTheme, string newTheme)
        {
            OldTheme = oldTheme ?? throw new ArgumentNullException(nameof(oldTheme));
            NewTheme = newTheme ?? throw new ArgumentNullException(nameof(newTheme));
        }
    }
}
