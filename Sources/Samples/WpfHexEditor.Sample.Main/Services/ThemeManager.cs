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
                    System.Diagnostics.Debug.WriteLine($"[ThemeManager] Removed old theme: {oldTheme}");
                }

                // Load new theme ResourceDictionary
                var themeUri = new Uri(
                    $"pack://application:,,,/WpfHexEditor.Sample.Main;component/Resources/Themes/{themeName}.xaml",
                    UriKind.Absolute);
                var themeDictionary = new ResourceDictionary { Source = themeUri };

                // Add to merged dictionaries
                Application.Current.Resources.MergedDictionaries.Add(themeDictionary);
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Loaded new theme: {themeName}");

                // Update current theme
                CurrentTheme = themeName;

                // Save to user settings if persistent
                if (persistent)
                {
                    Properties.Settings.Default.PreferredTheme = themeName;
                    Properties.Settings.Default.Save();
                    System.Diagnostics.Debug.WriteLine($"[ThemeManager] Saved theme '{themeName}' to settings");
                }

                // Notify all subscribers that theme has changed
                ThemeChanged?.Invoke(null, new ThemeChangedEventArgs(oldTheme, themeName));
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Changed theme from '{oldTheme}' to '{themeName}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Failed to load theme '{themeName}': {ex.Message}");

                // If the requested theme fails, try to fallback to Office
                if (themeName != "Office")
                {
                    System.Diagnostics.Debug.WriteLine("[ThemeManager] Attempting fallback to Office theme");
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
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] No saved theme, using default: {themeName}");
            }

            try
            {
                ChangeTheme(themeName, persistent: false); // Don't save again, already loaded from settings
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Invalid theme '{themeName}': {ex.Message}");
                // Fallback to Office is handled in ChangeTheme
            }
        }

        /// <summary>
        /// Synchronizes HexEditor control colors with the current theme.
        /// Must be called after theme changes to update HexEditor DependencyProperties.
        /// </summary>
        /// <param name="hexEditor">The HexEditor control to synchronize</param>
        public static void SyncHexEditorColors(WpfHexaEditor.HexEditor hexEditor)
        {
            if (hexEditor == null)
                return;

            try
            {
                var app = Application.Current;

                // Helper function to safely get color from resources
                Color GetColorResource(string key, Color fallback)
                {
                    try
                    {
                        return (Color)(app.FindResource(key) ?? fallback);
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine($"[ThemeManager] Color resource '{key}' not found, using fallback");
                        return fallback;
                    }
                }

                // Synchronize all HexEditor color properties with theme colors
                hexEditor.SelectionFirstColor = GetColorResource("HexEditor_SelectionFirstColor",
                    Color.FromArgb(102, 0, 120, 212));

                hexEditor.SelectionSecondColor = GetColorResource("HexEditor_SelectionSecondColor",
                    Color.FromArgb(102, 0, 120, 212));

                hexEditor.ByteModifiedColor = GetColorResource("HexEditor_ByteModifiedColor",
                    Color.FromRgb(255, 165, 0));

                hexEditor.ByteDeletedColor = GetColorResource("HexEditor_ByteDeletedColor",
                    Color.FromRgb(244, 67, 54));

                hexEditor.ByteAddedColor = GetColorResource("HexEditor_ByteAddedColor",
                    Color.FromRgb(76, 175, 80));

                hexEditor.HighLightColor = GetColorResource("HexEditor_HighLightColor",
                    Colors.Gold);

                hexEditor.MouseOverColor = GetColorResource("HexEditor_MouseOverColor",
                    Color.FromRgb(227, 242, 253));

                hexEditor.ForegroundSecondColor = GetColorResource("HexEditor_ForegroundSecondColor",
                    Colors.Blue);

                hexEditor.ForegroundOffSetHeaderColor = GetColorResource("HexEditor_ForegroundOffSetHeaderColor",
                    Color.FromRgb(117, 117, 117));

                hexEditor.ForegroundHighLightOffSetHeaderColor = GetColorResource("HexEditor_ForegroundHighLightOffSetHeaderColor",
                    Colors.DarkBlue);

                hexEditor.ForegroundContrast = GetColorResource("HexEditor_ForegroundContrast",
                    Colors.White);

                // AutoHighLiteSelectionByteBrush is actually a Brush property, but we need to check its type
                // For now, just skip it or use the color directly if it's a Color property
                // hexEditor.AutoHighLiteSelectionByteBrush = GetColorResource("HexEditor_AutoHighLiteSelectionByteBrush", Colors.LightBlue);

                hexEditor.TblDteColor = GetColorResource("HexEditor_TblDteColor",
                    Colors.Red);

                hexEditor.TblMteColor = GetColorResource("HexEditor_TblMteColor",
                    Colors.Green);

                hexEditor.TblEndBlockColor = GetColorResource("HexEditor_TblEndBlockColor",
                    Colors.Yellow);

                hexEditor.TblEndLineColor = GetColorResource("HexEditor_TblEndLineColor",
                    Colors.Orange);

                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Synchronized HexEditor colors with theme '{CurrentTheme}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Failed to sync HexEditor colors: {ex.Message}");
            }
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
