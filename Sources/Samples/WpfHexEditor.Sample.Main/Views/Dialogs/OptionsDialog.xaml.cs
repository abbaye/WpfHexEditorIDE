using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using WpfHexaEditor.Core;
using WpfHexEditor.Sample.Main.Services;

namespace WpfHexEditor.Sample.Main.Views.Dialogs
{
    /// <summary>
    /// Options dialog for application settings
    /// </summary>
    public partial class OptionsDialog : Window
    {
        public CultureInfo SelectedCulture { get; private set; }
        public CopyPasteMode SelectedCopyMode { get; private set; }

        public OptionsDialog()
        {
            // Resources are now loaded dynamically via DynamicResource
            // No need to restore culture here - it's handled globally by DynamicResourceManager
            InitializeComponent();
            LoadLanguages();
            LoadThemes();
            LoadCopyModes();

            // Subscribe to selection changes for instant language switching
            LanguageListView.SelectionChanged += LanguageListView_SelectionChanged;

            // Subscribe to selection changes for instant theme switching
            ThemeListView.SelectionChanged += ThemeListView_SelectionChanged;

            // Subscribe to selection changes for instant copy mode switching
            CopyModeListView.SelectionChanged += CopyModeListView_SelectionChanged;
        }

        private void LoadLanguages()
        {
            // All 19 languages supported
            var languages = new List<LanguageInfo>
            {
                new LanguageInfo { Flag = "🇺🇸", Code = "en", Name = "English", NativeName = "English" },
                new LanguageInfo { Flag = "🇪🇸", Code = "es-ES", Name = "Spanish (Spain)", NativeName = "Español (España)" },
                new LanguageInfo { Flag = "🇲🇽", Code = "es-419", Name = "Spanish (Latin America)", NativeName = "Español (Latinoamérica)" },
                new LanguageInfo { Flag = "🇫🇷", Code = "fr-FR", Name = "French (France)", NativeName = "Français (France)" },
                new LanguageInfo { Flag = "🇨🇦", Code = "fr-CA", Name = "French (Canada)", NativeName = "Français (Canada)" },
                new LanguageInfo { Flag = "🇵🇱", Code = "pl-PL", Name = "Polish", NativeName = "Polski" },
                new LanguageInfo { Flag = "🇧🇷", Code = "pt-BR", Name = "Portuguese (Brazil)", NativeName = "Português (Brasil)" },
                new LanguageInfo { Flag = "🇷🇺", Code = "ru-RU", Name = "Russian", NativeName = "Русский" },
                new LanguageInfo { Flag = "🇨🇳", Code = "zh-CN", Name = "Chinese (Simplified)", NativeName = "简体中文" },

                // New languages added in localization expansion
                new LanguageInfo { Flag = "🇩🇪", Code = "de-DE", Name = "German", NativeName = "Deutsch" },
                new LanguageInfo { Flag = "🇮🇹", Code = "it-IT", Name = "Italian", NativeName = "Italiano" },
                new LanguageInfo { Flag = "🇯🇵", Code = "ja-JP", Name = "Japanese", NativeName = "日本語" },
                new LanguageInfo { Flag = "🇰🇷", Code = "ko-KR", Name = "Korean", NativeName = "한국어" },
                new LanguageInfo { Flag = "🇳🇱", Code = "nl-NL", Name = "Dutch", NativeName = "Nederlands" },
                new LanguageInfo { Flag = "🇸🇪", Code = "sv-SE", Name = "Swedish", NativeName = "Svenska" },
                new LanguageInfo { Flag = "🇹🇷", Code = "tr-TR", Name = "Turkish", NativeName = "Türkçe" },
                new LanguageInfo { Flag = "🇮🇳", Code = "hi-IN", Name = "Hindi", NativeName = "हिन्दी" },
                new LanguageInfo { Flag = "🇦🇪", Code = "ar-SA", Name = "Arabic", NativeName = "العربية" },
                new LanguageInfo { Flag = "🇵🇹", Code = "pt-PT", Name = "Portuguese (Portugal)", NativeName = "Português (Portugal)" },
            };

            // Sort by name for easy navigation
            languages = languages.OrderBy(l => l.Name).ToList();

            // Insert "System Language" at the top of the list
            var systemLanguage = new LanguageInfo
            {
                Flag = "🌐",
                Code = "system",
                Name = GetResourceString("Options_Language_SystemLanguage_Name", "System Language"),
                NativeName = GetResourceString("Options_Language_SystemLanguage_Description", "Use system language")
            };
            languages.Insert(0, systemLanguage);

            LanguageListView.ItemsSource = languages;

            // Check if user has a saved language preference
            var savedCulture = Properties.Settings.Default.PreferredCulture;
            LanguageInfo currentLang;

            if (string.IsNullOrEmpty(savedCulture))
            {
                // No saved preference - select "System Language"
                System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadLanguages] No saved culture preference, selecting System Language");
                currentLang = systemLanguage;
                SelectedCulture = CultureInfo.InstalledUICulture;
            }
            else
            {
                // Select current language
                var currentCulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
                System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadLanguages] Current UI Culture: {currentCulture.Name} ({currentCulture.NativeName})");
                currentLang = languages.FirstOrDefault(l =>
                    l.Code == currentCulture.Name ||
                    l.Code.StartsWith(currentCulture.TwoLetterISOLanguageName) ||
                    currentCulture.Name.StartsWith(l.Code));

                if (currentLang == null || currentLang.Code == "system")
                {
                    // Default to English if current culture not found
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadLanguages] No matching language found for {currentCulture.Name}, defaulting to English");
                    currentLang = languages.FirstOrDefault(l => l.Code == "en") ?? languages.First();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadLanguages] Matched language: {currentLang.Code} ({currentLang.Name})");
                }

                SelectedCulture = new CultureInfo(currentLang.Code);
            }

            LanguageListView.SelectedItem = currentLang;

            // Update current language display
            CurrentLanguageFlag.Text = currentLang.Flag;
            CurrentLanguageText.Text = $"{currentLang.Name} - {currentLang.NativeName}";
        }

        private void LoadThemes()
        {
            // All 6 themes supported in V2
            var themes = new List<ThemeInfo>
            {
                new ThemeInfo { Icon = "📄", Name = "Office", DisplayName = "Office", Description = "Light professional office theme" },
                new ThemeInfo { Icon = "🎨", Name = "VisualStudio", DisplayName = "Visual Studio", Description = "Professional clean theme inspired by VS 2022" },
                new ThemeInfo { Icon = "☀️", Name = "Light", DisplayName = "Light", Description = "Clean professional light theme" },
                new ThemeInfo { Icon = "🌙", Name = "DarkGlass", DisplayName = "Dark Glass", Description = "Modern glassmorphism dark theme" },
                new ThemeInfo { Icon = "⚪", Name = "Minimal", DisplayName = "Minimal", Description = "Ultra-clean minimalist theme" },
                new ThemeInfo { Icon = "🌆", Name = "Cyberpunk", DisplayName = "Cyberpunk", Description = "Vibrant neon cyberpunk theme" }
            };

            ThemeListView.ItemsSource = themes;

            // Select current theme
            var currentTheme = ThemeManager.CurrentTheme;
            System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadThemes] Current Theme: {currentTheme}");

            var currentThemeInfo = themes.FirstOrDefault(t => t.Name == currentTheme) ?? themes.First();
            System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadThemes] Matched theme: {currentThemeInfo.Name}");

            ThemeListView.SelectedItem = currentThemeInfo;

            // Update current theme display
            CurrentThemeIcon.Text = currentThemeInfo.Icon;
            CurrentThemeText.Text = $"{currentThemeInfo.DisplayName} - {currentThemeInfo.Description}";
        }

        /// <summary>
        /// Handles instant theme switching when user selects a theme from the list.
        /// </summary>
        private void ThemeListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ThemeListView.SelectedItem is ThemeInfo selected)
            {
                var oldTheme = ThemeManager.CurrentTheme;

                // Only change if it's actually different
                if (selected.Name != oldTheme)
                {
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.ThemeListView_SelectionChanged] Instantly changing theme from '{oldTheme}' to '{selected.Name}'");

                    // Change theme instantly - no confirmation needed!
                    ThemeManager.ChangeTheme(selected.Name, persistent: true);

                    // Update the current theme display immediately
                    CurrentThemeIcon.Text = selected.Icon;
                    CurrentThemeText.Text = $"{selected.DisplayName} - {selected.Description}";

                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.ThemeListView_SelectionChanged] Theme changed instantly! UI updated in real-time.");
                }
            }
        }

        /// <summary>
        /// Handles instant language switching when user selects a language from the list.
        /// </summary>
        private void LanguageListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LanguageListView.SelectedItem is LanguageInfo selected)
            {
                CultureInfo newCulture;

                // Check if "System Language" is selected
                if (selected.Code == "system")
                {
                    // Use the system's installed UI culture
                    newCulture = CultureInfo.InstalledUICulture;
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LanguageListView_SelectionChanged] System Language selected, using: {newCulture.Name}");

                    // Clear the saved culture preference so the system language is used
                    Properties.Settings.Default.PreferredCulture = string.Empty;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    newCulture = new CultureInfo(selected.Code);
                }

                var oldCulture = DynamicResourceManager.CurrentCulture;

                // Only change if it's actually different
                if (newCulture.Name != oldCulture.Name)
                {
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LanguageListView_SelectionChanged] Instantly changing culture from '{oldCulture.Name}' to '{newCulture.Name}'");

                    // Change culture instantly - no confirmation needed!
                    DynamicResourceManager.ChangeCulture(newCulture, persistent: selected.Code != "system");

                    // Update the current language display immediately
                    CurrentLanguageFlag.Text = selected.Flag;
                    CurrentLanguageText.Text = $"{selected.Name} - {selected.NativeName}";

                    SelectedCulture = newCulture;

                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LanguageListView_SelectionChanged] Language changed instantly! UI updated in real-time.");
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (LanguageListView.SelectedItem is LanguageInfo selected)
            {
                SelectedCulture = new CultureInfo(selected.Code);
                DialogResult = true;
            }
            else
            {
                MessageBox.Show(
                    Properties.Resources.Message_NoLanguageSelected_Text,
                    Properties.Resources.Message_NoLanguageSelected_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// Helper method to get a resource string with fallback
        /// </summary>
        private string GetResourceString(string key, string fallback)
        {
            try
            {
                var value = Properties.Resources.ResourceManager.GetString(key);
                return string.IsNullOrEmpty(value) ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        private void LoadCopyModes()
        {
            // All supported copy modes
            var copyModes = new List<CopyModeInfo>
            {
                new CopyModeInfo { Icon = "🔄", Mode = CopyPasteMode.Auto, DisplayName = "Auto (Default)", Description = "Automatically selects format based on context" },
                new CopyModeInfo { Icon = "📋", Mode = CopyPasteMode.HexaString, DisplayName = "Hexadecimal", Description = "Copy as hexadecimal string (e.g., \"48656C6C6F\")" },
                new CopyModeInfo { Icon = "📝", Mode = CopyPasteMode.AsciiString, DisplayName = "ASCII", Description = "Copy as ASCII text string" },
                new CopyModeInfo { Icon = "📊", Mode = CopyPasteMode.FormattedView, DisplayName = "Formatted View", Description = "Copy with offsets, hex bytes, and ASCII columns" },
                new CopyModeInfo { Icon = "💻", Mode = CopyPasteMode.CSharpCode, DisplayName = "C# Code", Description = "Copy as C# byte array code" },
                new CopyModeInfo { Icon = "🔧", Mode = CopyPasteMode.CCode, DisplayName = "C Code", Description = "Copy as C byte array code" },
                new CopyModeInfo { Icon = "📖", Mode = CopyPasteMode.TblString, DisplayName = "TBL String", Description = "Copy using loaded TBL character table" }
            };

            CopyModeListView.ItemsSource = copyModes;

            // Select current copy mode (default to Auto if not set)
            var currentMode = CopyPasteMode.Auto; // TODO: Load from settings
            System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadCopyModes] Current Copy Mode: {currentMode}");

            var currentModeInfo = copyModes.FirstOrDefault(m => m.Mode == currentMode) ?? copyModes.First();
            System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadCopyModes] Matched mode: {currentModeInfo.DisplayName}");

            CopyModeListView.SelectedItem = currentModeInfo;
            SelectedCopyMode = currentMode;

            // Update current copy mode display
            CurrentCopyModeIcon.Text = currentModeInfo.Icon;
            CurrentCopyModeText.Text = $"{currentModeInfo.DisplayName} - {currentModeInfo.Description}";
        }

        /// <summary>
        /// Handles instant copy mode switching when user selects a mode from the list.
        /// </summary>
        private void CopyModeListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CopyModeListView.SelectedItem is CopyModeInfo selected)
            {
                var oldMode = SelectedCopyMode;

                // Only change if it's actually different
                if (selected.Mode != oldMode)
                {
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.CopyModeListView_SelectionChanged] Instantly changing copy mode from '{oldMode}' to '{selected.Mode}'");

                    // Update the current copy mode display immediately
                    CurrentCopyModeIcon.Text = selected.Icon;
                    CurrentCopyModeText.Text = $"{selected.DisplayName} - {selected.Description}";

                    SelectedCopyMode = selected.Mode;

                    // TODO: Persist the setting
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.CopyModeListView_SelectionChanged] Copy mode changed instantly! UI updated in real-time.");
                }
            }
        }
    }

    /// <summary>
    /// Represents a language option with flag, code, and names
    /// </summary>
    public class LanguageInfo
    {
        /// <summary>
        /// Flag emoji for the language
        /// </summary>
        public string Flag { get; set; }

        /// <summary>
        /// Culture code (e.g., "en", "fr-CA", "zh-CN")
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// English name of the language
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Native name of the language
        /// </summary>
        public string NativeName { get; set; }
    }

    /// <summary>
    /// Represents a theme option with icon, name, and description
    /// </summary>
    public class ThemeInfo
    {
        /// <summary>
        /// Icon emoji for the theme
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Theme name (e.g., "Office", "DarkGlass", "Cyberpunk")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Display name of the theme
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Description of the theme
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Represents a copy mode option with icon, mode, and description
    /// </summary>
    public class CopyModeInfo
    {
        /// <summary>
        /// Icon emoji for the copy mode
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Copy/Paste mode enum value
        /// </summary>
        public CopyPasteMode Mode { get; set; }

        /// <summary>
        /// Display name of the copy mode
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Description of the copy mode
        /// </summary>
        public string Description { get; set; }
    }
}
