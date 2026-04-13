//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using WpfHexEditor.Core;
using WpfHexEditor.Sample.HexEditor.Services;

namespace WpfHexEditor.Sample.HexEditor.Views.Dialogs
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
                new LanguageInfo { Flag = "ðŸ‡ºðŸ‡¸", Code = "en", Name = "English", NativeName = "English" },
                new LanguageInfo { Flag = "ðŸ‡ªðŸ‡¸", Code = "es-ES", Name = "Spanish (Spain)", NativeName = "EspaÃ±ol (EspaÃ±a)" },
                new LanguageInfo { Flag = "ðŸ‡²ðŸ‡½", Code = "es-419", Name = "Spanish (Latin America)", NativeName = "EspaÃ±ol (LatinoamÃ©rica)" },
                new LanguageInfo { Flag = "ðŸ‡«ðŸ‡·", Code = "fr-FR", Name = "French (France)", NativeName = "FranÃ§ais (France)" },
                new LanguageInfo { Flag = "ðŸ‡¨ðŸ‡¦", Code = "fr-CA", Name = "French (Canada)", NativeName = "FranÃ§ais (Canada)" },
                new LanguageInfo { Flag = "ðŸ‡µðŸ‡±", Code = "pl-PL", Name = "Polish", NativeName = "Polski" },
                new LanguageInfo { Flag = "ðŸ‡§ðŸ‡·", Code = "pt-BR", Name = "Portuguese (Brazil)", NativeName = "PortuguÃªs (Brasil)" },
                new LanguageInfo { Flag = "ðŸ‡·ðŸ‡º", Code = "ru-RU", Name = "Russian", NativeName = "Ð ÑƒÑÑÐºÐ¸Ð¹" },
                new LanguageInfo { Flag = "ðŸ‡¨ðŸ‡³", Code = "zh-CN", Name = "Chinese (Simplified)", NativeName = "ç®€ä½“ä¸­æ–‡" },

                // New languages added in localization expansion
                new LanguageInfo { Flag = "ðŸ‡©ðŸ‡ª", Code = "de-DE", Name = "German", NativeName = "Deutsch" },
                new LanguageInfo { Flag = "ðŸ‡®ðŸ‡¹", Code = "it-IT", Name = "Italian", NativeName = "Italiano" },
                new LanguageInfo { Flag = "ðŸ‡¯ðŸ‡µ", Code = "ja-JP", Name = "Japanese", NativeName = "æ—¥æœ¬èªž" },
                new LanguageInfo { Flag = "ðŸ‡°ðŸ‡·", Code = "ko-KR", Name = "Korean", NativeName = "í•œêµ­ì–´" },
                new LanguageInfo { Flag = "ðŸ‡³ðŸ‡±", Code = "nl-NL", Name = "Dutch", NativeName = "Nederlands" },
                new LanguageInfo { Flag = "ðŸ‡¸ðŸ‡ª", Code = "sv-SE", Name = "Swedish", NativeName = "Svenska" },
                new LanguageInfo { Flag = "ðŸ‡¹ðŸ‡·", Code = "tr-TR", Name = "Turkish", NativeName = "TÃ¼rkÃ§e" },
                new LanguageInfo { Flag = "ðŸ‡®ðŸ‡³", Code = "hi-IN", Name = "Hindi", NativeName = "à¤¹à¤¿à¤¨à¥à¤¦à¥€" },
                new LanguageInfo { Flag = "ðŸ‡¦ðŸ‡ª", Code = "ar-SA", Name = "Arabic", NativeName = "Ø§Ù„Ø¹Ø±Ø¨ÙŠØ©" },
                new LanguageInfo { Flag = "ðŸ‡µðŸ‡¹", Code = "pt-PT", Name = "Portuguese (Portugal)", NativeName = "PortuguÃªs (Portugal)" },
            };

            // Sort by name for easy navigation
            languages = languages.OrderBy(l => l.Name).ToList();

            // Insert "System Language" at the top of the list
            var systemLanguage = new LanguageInfo
            {
                Flag = "ðŸŒ",
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
                currentLang = systemLanguage;
                SelectedCulture = CultureInfo.InstalledUICulture;
            }
            else
            {
                // Select current language
                var currentCulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
                currentLang = languages.FirstOrDefault(l =>
                    l.Code == currentCulture.Name ||
                    l.Code.StartsWith(currentCulture.TwoLetterISOLanguageName) ||
                    currentCulture.Name.StartsWith(l.Code));

                if (currentLang == null || currentLang.Code == "system")
                {
                    // Default to English if current culture not found
                    currentLang = languages.FirstOrDefault(l => l.Code == "en") ?? languages.First();
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
                new ThemeInfo { Icon = "ðŸ“„", Name = "Office", DisplayName = "Office", Description = "Light professional office theme" },
                new ThemeInfo { Icon = "ðŸŽ¨", Name = "VisualStudio", DisplayName = "Visual Studio", Description = "Professional clean theme inspired by VS 2022" },
                new ThemeInfo { Icon = "â˜€ï¸", Name = "Light", DisplayName = "Light", Description = "Clean professional light theme" },
                new ThemeInfo { Icon = "ðŸŒ™", Name = "DarkGlass", DisplayName = "Dark Glass", Description = "Modern glassmorphism dark theme" },
                new ThemeInfo { Icon = "âšª", Name = "Minimal", DisplayName = "Minimal", Description = "Ultra-clean minimalist theme" },
                new ThemeInfo { Icon = "ðŸŒ†", Name = "Cyberpunk", DisplayName = "Cyberpunk", Description = "Vibrant neon cyberpunk theme" }
            };

            ThemeListView.ItemsSource = themes;

            // Select current theme
            var currentTheme = ThemeManager.CurrentTheme;
            var currentThemeInfo = themes.FirstOrDefault(t => t.Name == currentTheme) ?? themes.First();

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
                    ThemeManager.ChangeTheme(selected.Name, persistent: true);

                    CurrentThemeIcon.Text = selected.Icon;
                    CurrentThemeText.Text = $"{selected.DisplayName} - {selected.Description}";
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
                    newCulture = CultureInfo.InstalledUICulture;

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
                    DynamicResourceManager.ChangeCulture(newCulture, persistent: selected.Code != "system");

                    CurrentLanguageFlag.Text = selected.Flag;
                    CurrentLanguageText.Text = $"{selected.Name} - {selected.NativeName}";

                    SelectedCulture = newCulture;
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
                new CopyModeInfo { Icon = "ðŸ”„", Mode = CopyPasteMode.Auto, DisplayName = "Auto (Default)", Description = "Automatically selects format based on context" },
                new CopyModeInfo { Icon = "ðŸ“‹", Mode = CopyPasteMode.HexaString, DisplayName = "Hexadecimal", Description = "Copy as hexadecimal string (e.g., \"48656C6C6F\")" },
                new CopyModeInfo { Icon = "ðŸ“", Mode = CopyPasteMode.AsciiString, DisplayName = "ASCII", Description = "Copy as ASCII text string" },
                new CopyModeInfo { Icon = "ðŸ“Š", Mode = CopyPasteMode.FormattedView, DisplayName = "Formatted View", Description = "Copy with offsets, hex bytes, and ASCII columns" },
                new CopyModeInfo { Icon = "ðŸ’»", Mode = CopyPasteMode.CSharpCode, DisplayName = "C# Code", Description = "Copy as C# byte array code" },
                new CopyModeInfo { Icon = "ðŸ”§", Mode = CopyPasteMode.CCode, DisplayName = "C Code", Description = "Copy as C byte array code" },
                new CopyModeInfo { Icon = "ðŸ“–", Mode = CopyPasteMode.TblString, DisplayName = "TBL String", Description = "Copy using loaded TBL character table" }
            };

            CopyModeListView.ItemsSource = copyModes;

            // Select current copy mode (loaded from user settings, default Auto)
            var savedMode = Properties.Settings.Default.PreferredCopyMode;
            var currentMode = Enum.TryParse<CopyPasteMode>(savedMode, out var parsedMode)
                ? parsedMode
                : CopyPasteMode.Auto;
            var currentModeInfo = copyModes.FirstOrDefault(m => m.Mode == currentMode) ?? copyModes.First();

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
                    CurrentCopyModeIcon.Text = selected.Icon;
                    CurrentCopyModeText.Text = $"{selected.DisplayName} - {selected.Description}";

                    SelectedCopyMode = selected.Mode;

                    Properties.Settings.Default.PreferredCopyMode = selected.Mode.ToString();
                    Properties.Settings.Default.Save();
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
