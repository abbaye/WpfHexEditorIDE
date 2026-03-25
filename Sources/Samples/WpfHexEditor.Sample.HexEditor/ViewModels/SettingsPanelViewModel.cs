//////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// HexEditor V2 - Settings Panel ViewModel
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace WpfHexEditor.Sample.HexEditor.ViewModels
{
    /// <summary>
    /// ViewModel for Settings Panel - theme, language, search, and performance options
    /// </summary>
    public class SettingsPanelViewModel : INotifyPropertyChanged
    {
        #region Fields

        private string _selectedTheme = "VisualStudio";
        private string _selectedLanguage = "en-US";
        private bool _enableParallelSearch = true;
        private bool _useWildcardSearch = false;
        private int _searchResultLimit = 10000;
        private bool _highlightAllMatches = true;
        private bool _showLineNumbers = true;
        private bool _showScrollMarkers = true;

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<string> ThemeChanged;
        public event EventHandler<string> LanguageChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Available themes
        /// </summary>
        public List<ThemeOption> AvailableThemes { get; } = new List<ThemeOption>
        {
            new ThemeOption { Name = "Office", DisplayName = "Office", Description = "Light professional office theme" },
            new ThemeOption { Name = "VisualStudio", DisplayName = "Visual Studio", Description = "Professional clean theme inspired by VS 2022" },
            new ThemeOption { Name = "Light", DisplayName = "Light", Description = "Clean professional light theme" },
            new ThemeOption { Name = "DarkGlass", DisplayName = "Dark Glass", Description = "Modern glassmorphism dark theme" },
            new ThemeOption { Name = "Minimal", DisplayName = "Minimal", Description = "Ultra-clean minimalist theme" },
            new ThemeOption { Name = "Cyberpunk", DisplayName = "Cyberpunk", Description = "Vibrant neon cyberpunk theme" }
        };

        /// <summary>
        /// Available languages
        /// </summary>
        public List<LanguageOption> AvailableLanguages { get; } = new List<LanguageOption>
        {
            new LanguageOption { Code = "en-US", DisplayName = "English", NativeName = "English" },
            new LanguageOption { Code = "fr-CA", DisplayName = "French (Canada)", NativeName = "FranÃ§ais (Canada)" },
            new LanguageOption { Code = "pl-PL", DisplayName = "Polish", NativeName = "Polski" },
            new LanguageOption { Code = "pt-BR", DisplayName = "Portuguese (Brazil)", NativeName = "PortuguÃªs (Brasil)" },
            new LanguageOption { Code = "ru-RU", DisplayName = "Russian", NativeName = "Ð ÑƒÑÑÐºÐ¸Ð¹" },
            new LanguageOption { Code = "zh-CN", DisplayName = "Chinese", NativeName = "ä¸­æ–‡" }
        };

        /// <summary>
        /// Selected theme
        /// </summary>
        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged(nameof(SelectedTheme));

                    // Change theme instantly via ThemeManager
                    Services.ThemeManager.ChangeTheme(value, persistent: true);

                    ThemeChanged?.Invoke(this, value); // Keep event for backward compatibility
                }
            }
        }

        /// <summary>
        /// Selected language
        /// </summary>
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (_selectedLanguage != value)
                {
                    _selectedLanguage = value;
                    OnPropertyChanged(nameof(SelectedLanguage));
                    ApplyLanguage(value);
                }
            }
        }

        /// <summary>
        /// Enable parallel search for large files
        /// </summary>
        public bool EnableParallelSearch
        {
            get => _enableParallelSearch;
            set
            {
                _enableParallelSearch = value;
                OnPropertyChanged(nameof(EnableParallelSearch));
            }
        }

        /// <summary>
        /// Use wildcard search (?? notation)
        /// </summary>
        public bool UseWildcardSearch
        {
            get => _useWildcardSearch;
            set
            {
                _useWildcardSearch = value;
                OnPropertyChanged(nameof(UseWildcardSearch));
            }
        }

        /// <summary>
        /// Maximum number of search results
        /// </summary>
        public int SearchResultLimit
        {
            get => _searchResultLimit;
            set
            {
                _searchResultLimit = value;
                OnPropertyChanged(nameof(SearchResultLimit));
            }
        }

        /// <summary>
        /// Highlight all search matches
        /// </summary>
        public bool HighlightAllMatches
        {
            get => _highlightAllMatches;
            set
            {
                _highlightAllMatches = value;
                OnPropertyChanged(nameof(HighlightAllMatches));
            }
        }

        /// <summary>
        /// Show line numbers
        /// </summary>
        public bool ShowLineNumbers
        {
            get => _showLineNumbers;
            set
            {
                _showLineNumbers = value;
                OnPropertyChanged(nameof(ShowLineNumbers));
            }
        }

        /// <summary>
        /// Show scroll markers
        /// </summary>
        public bool ShowScrollMarkers
        {
            get => _showScrollMarkers;
            set
            {
                _showScrollMarkers = value;
                OnPropertyChanged(nameof(ShowScrollMarkers));
            }
        }

        #endregion

        #region Commands

        public ICommand ResetToDefaultsCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        #endregion

        #region Constructor

        public SettingsPanelViewModel()
        {
            ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
            SaveSettingsCommand = new RelayCommand(SaveSettings);

            // Load persisted settings
            LoadSettings();

            // Initialize theme from ThemeManager
            _selectedTheme = Services.ThemeManager.CurrentTheme;

            // Set initial language
            ApplyLanguage(SelectedLanguage);
        }

        #endregion

        #region Methods

        private void ApplyLanguage(string languageCode)
        {
            try
            {
                var culture = new CultureInfo(languageCode);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;

                LanguageChanged?.Invoke(this, languageCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply language {languageCode}: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            var settings = Properties.Settings.Default;

            EnableParallelSearch = settings.EnableParallelSearch;
            UseWildcardSearch = settings.UseWildcardSearch;
            SearchResultLimit = settings.SearchResultLimit;
            HighlightAllMatches = settings.HighlightAllMatches;
            ShowLineNumbers = settings.ShowLineNumbers;
            ShowScrollMarkers = settings.ShowScrollMarkers;

            if (!string.IsNullOrEmpty(settings.PreferredCulture))
            {
                _selectedLanguage = settings.PreferredCulture;
            }
        }

        private void ResetToDefaults()
        {
            SelectedTheme = "DarkGlass";
            SelectedLanguage = "en-US";
            EnableParallelSearch = true;
            UseWildcardSearch = false;
            SearchResultLimit = 10000;
            HighlightAllMatches = true;
            ShowLineNumbers = true;
            ShowScrollMarkers = true;
        }

        private void SaveSettings()
        {
            var settings = Properties.Settings.Default;

            settings.EnableParallelSearch = EnableParallelSearch;
            settings.UseWildcardSearch = UseWildcardSearch;
            settings.SearchResultLimit = SearchResultLimit;
            settings.HighlightAllMatches = HighlightAllMatches;
            settings.ShowLineNumbers = ShowLineNumbers;
            settings.ShowScrollMarkers = ShowScrollMarkers;

            settings.Save();
            System.Diagnostics.Debug.WriteLine("[SettingsPanelViewModel] Settings saved");
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Theme option model
    /// </summary>
    public class ThemeOption
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Language option model
    /// </summary>
    public class LanguageOption
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
        public string NativeName { get; set; }
    }
}
