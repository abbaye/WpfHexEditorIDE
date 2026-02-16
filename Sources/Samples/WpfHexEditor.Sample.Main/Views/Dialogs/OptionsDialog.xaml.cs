using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

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
            // CRITICAL: Restore culture BEFORE InitializeComponent to ensure XAML resources load in correct language
            var cultureName = Properties.Settings.Default.PreferredCulture;
            if (!string.IsNullOrEmpty(cultureName))
            {
                try
                {
                    var culture = new CultureInfo(cultureName);
                    System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                    System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.Constructor] Set culture to: {culture.Name}");
                }
                catch (CultureNotFoundException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OptionsDialog.Constructor] CultureNotFoundException: {ex.Message}");
                }
            }

            InitializeComponent();
            LoadLanguages();
        }

        private void LoadLanguages()
        {
            // Current 6 languages supported in V2
            var languages = new List<LanguageInfo>
            {
                new LanguageInfo { Flag = "🇺🇸", Code = "en", Name = "English", NativeName = "English" },
                new LanguageInfo { Flag = "🇨🇦", Code = "fr-CA", Name = "French (Canada)", NativeName = "Français (Canada)" },
                new LanguageInfo { Flag = "🇵🇱", Code = "pl-PL", Name = "Polish", NativeName = "Polski" },
                new LanguageInfo { Flag = "🇧🇷", Code = "pt-BR", Name = "Portuguese (Brazil)", NativeName = "Português (Brasil)" },
                new LanguageInfo { Flag = "🇷🇺", Code = "ru-RU", Name = "Russian", NativeName = "Русский" },
                new LanguageInfo { Flag = "🇨🇳", Code = "zh-CN", Name = "Chinese (Simplified)", NativeName = "简体中文" },

                // Future languages (Phase 2 - will be added when resources are ready)
                /*
                new LanguageInfo { Flag = "🇩🇪", Code = "de-DE", Name = "German", NativeName = "Deutsch" },
                new LanguageInfo { Flag = "🇪🇸", Code = "es-ES", Name = "Spanish", NativeName = "Español" },
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
}
