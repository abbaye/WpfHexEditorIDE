using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using WpfHexaEditor.Services;

namespace WpfHexEditor.Sample.Main.Services
{
    /// <summary>
    /// Manages dynamic culture changes for the application without requiring restart.
    /// Provides event notification when culture changes so UI can update dynamically.
    /// </summary>
    public static class DynamicResourceManager
    {
        /// <summary>
        /// Event raised when the application culture is changed.
        /// Subscribe to this event to refresh UI elements.
        /// </summary>
        public static event EventHandler<CultureChangedEventArgs>? CultureChanged;

        /// <summary>
        /// Gets the current application culture.
        /// </summary>
        public static CultureInfo CurrentCulture => Thread.CurrentThread.CurrentUICulture;

        /// <summary>
        /// Changes the application culture and notifies all subscribers.
        /// This allows for instant language switching without application restart.
        /// </summary>
        /// <param name="newCulture">The new culture to apply</param>
        /// <param name="persistent">If true, saves the culture to user settings</param>
        public static void ChangeCulture(CultureInfo newCulture, bool persistent = true)
        {
            if (newCulture == null)
                throw new ArgumentNullException(nameof(newCulture));

            var oldCulture = CurrentCulture;

            // Set culture for current thread
            Thread.CurrentThread.CurrentCulture = newCulture;
            Thread.CurrentThread.CurrentUICulture = newCulture;

            // Set default culture for new threads
            CultureInfo.DefaultThreadCurrentCulture = newCulture;
            CultureInfo.DefaultThreadCurrentUICulture = newCulture;

            // NOTE: We don't call OverrideMetadata here because it can only be called ONCE per type
            // It's already been called in Initialize() at application startup

            // Save to user settings if persistent
            if (persistent)
            {
                Properties.Settings.Default.PreferredCulture = newCulture.Name;
                Properties.Settings.Default.Save();
                System.Diagnostics.Debug.WriteLine($"[DynamicResourceManager] Saved culture '{newCulture.Name}' to settings");
            }

            System.Diagnostics.Debug.WriteLine($"[DynamicResourceManager] Changed culture from '{oldCulture.Name}' to '{newCulture.Name}'");

            // Synchronize with HexEditor control's LocalizedResourceDictionary
            WpfHexaEditor.Services.LocalizedResourceDictionary.ChangeCulture(newCulture);
            System.Diagnostics.Debug.WriteLine($"[DynamicResourceManager] Synchronized HexEditor control culture to '{newCulture.Name}'");

            // Notify all subscribers that culture has changed
            CultureChanged?.Invoke(null, new CultureChangedEventArgs(oldCulture, newCulture));
        }

        /// <summary>
        /// Initializes the culture from user settings or system default.
        /// Should be called at application startup.
        /// </summary>
        public static void Initialize()
        {
            var cultureName = Properties.Settings.Default.PreferredCulture;

            if (string.IsNullOrEmpty(cultureName))
            {
                // Use system default culture
                cultureName = CultureInfo.CurrentUICulture.Name;
                System.Diagnostics.Debug.WriteLine($"[DynamicResourceManager] No saved culture, using system default: {cultureName}");
            }

            try
            {
                var culture = new CultureInfo(cultureName);

                // IMPORTANT: OverrideMetadata can only be called ONCE per type, so we do it here at startup
                // This ensures WPF respects the culture setting for all FrameworkElements
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(
                        System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

                ChangeCulture(culture, persistent: false); // Don't save again, already loaded from settings
            }
            catch (CultureNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DynamicResourceManager] Invalid culture '{cultureName}': {ex.Message}");
                // Fallback to English
                var fallbackCulture = new CultureInfo("en");

                // Set OverrideMetadata for fallback culture
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(
                        System.Windows.Markup.XmlLanguage.GetLanguage(fallbackCulture.IetfLanguageTag)));

                ChangeCulture(fallbackCulture, persistent: false);
            }
        }
    }

    /// <summary>
    /// Event args for culture change notification.
    /// </summary>
    public class CultureChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous culture before the change.
        /// </summary>
        public CultureInfo OldCulture { get; }

        /// <summary>
        /// Gets the new culture after the change.
        /// </summary>
        public CultureInfo NewCulture { get; }

        public CultureChangedEventArgs(CultureInfo oldCulture, CultureInfo newCulture)
        {
            OldCulture = oldCulture ?? throw new ArgumentNullException(nameof(oldCulture));
            NewCulture = newCulture ?? throw new ArgumentNullException(nameof(newCulture));
        }
    }
}
