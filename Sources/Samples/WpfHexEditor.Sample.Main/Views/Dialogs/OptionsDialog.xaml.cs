using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using WpfHexEditor.Sample.Main.Services;

namespace WpfHexEditor.Sample.Main.Views.Dialogs
{
    /// <summary>
    /// Options dialog for application settings
    /// </summary>
    public partial class OptionsDialog : Window
    {
        public CultureInfo SelectedCulture { get; private set; }

        public OptionsDialog()
        {
            // Resources are now loaded dynamically via DynamicResource
            // No need to restore culture here - it's handled globally by DynamicResourceManager
            InitializeComponent();
            LoadLanguages();
            LoadThemes();

            // Subscribe to selection changes for instant language switching
            LanguageListView.SelectionChanged += LanguageListView_SelectionChanged;

            // Subscribe to selection changes for instant theme switching
            ThemeListView.SelectionChanged += ThemeListView_SelectionChanged;
        }

        private void LoadLanguages()
        {
            // Current 9 languages supported in V2
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

                // Future languages (Phase 3 - will be added when resources are ready)
                /*
                new LanguageInfo { Flag = "🇩🇪", Code = "de-DE", Name = "German", NativeName = "Deutsch" },
                new LanguageInfo { Flag = "🇮🇹", Code = "it-IT", Name = "Italian", NativeName = "Italiano" },
                new LanguageInfo { Flag = "🇯🇵", Code = "ja-JP", Name = "Japanese", NativeName = "日本語" },
                new LanguageInfo { Flag = "🇰🇷", Code = "ko-KR", Name = "Korean", NativeName = "한국어" },
                new LanguageInfo { Flag = "🇳🇱", Code = "nl-NL", Name = "Dutch", NativeName = "Nederlands" },
                new LanguageInfo { Flag = "🇸🇪", Code = "sv-SE", Name = "Swedish", NativeName = "Svenska" },
                new LanguageInfo { Flag = "🇹🇷", Code = "tr-TR", Name = "Turkish", NativeName = "Türkçe" },
                new LanguageInfo { Flag = "🇮🇳", Code = "hi-IN", Name = "Hindi", NativeName = "हिन्दी" },
                new LanguageInfo { Flag = "🇦🇪", Code = "ar-SA", Name = "Arabic", NativeName = "العربية" },
                */
            };

            // Sort by name for easy navigation
            languages = languages.OrderBy(l => l.Name).ToList();

            LanguageListView.ItemsSource = languages;

            // Select current language
            var currentCulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
            System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadLanguages] Current UI Culture: {currentCulture.Name} ({currentCulture.NativeName})");
            var currentLang = languages.FirstOrDefault(l =>
                l.Code == currentCulture.Name ||
                l.Code.StartsWith(currentCulture.TwoLetterISOLanguageName) ||
                currentCulture.Name.StartsWith(l.Code));

            if (currentLang == null)
            {
                // Default to English if current culture not found
                System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadLanguages] No matching language found for {currentCulture.Name}, defaulting to English");
                currentLang = languages.FirstOrDefault(l => l.Code == "en") ?? languages.First();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LoadLanguages] Matched language: {currentLang.Code} ({currentLang.Name})");
            }

            LanguageListView.SelectedItem = currentLang;
            SelectedCulture = new CultureInfo(currentLang.Code);

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
                var newCulture = new CultureInfo(selected.Code);
                var oldCulture = DynamicResourceManager.CurrentCulture;

                // Only change if it's actually different
                if (newCulture.Name != oldCulture.Name)
                {
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.LanguageListView_SelectionChanged] Instantly changing culture from '{oldCulture.Name}' to '{newCulture.Name}'");

                    // Change culture instantly - no confirmation needed!
                    DynamicResourceManager.ChangeCulture(newCulture, persistent: true);

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
}
