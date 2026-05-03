// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core.Localization.Services;
using HexEditorControl = WpfHexEditor.HexEditor.HexEditor;

namespace WpfHexEditor.Core.Options.Pages;

public sealed partial class EnvironmentGeneralPage : UserControl, IOptionsPage
{
    // -- Theme map : display name → .xaml file stem -----------------------
    private static readonly IReadOnlyDictionary<string, string> ThemeMap =
        new Dictionary<string, string>
        {
            // Dark IDE
            { "Dark",              "DarkTheme"             },
            { "VS2022 Dark",       "VS2022DarkTheme"       },
            { "Visual Studio",     "VisualStudioTheme"     },
            // Dark Atmospheric
            { "Dark Glass",        "DarkGlassTheme"        },
            { "Cyberpunk",         "CyberpunkTheme"        },
            { "Synthwave '84",     "Synthwave84Theme"      },
            { "Matrix",            "MatrixTheme"           },
            // Dark Legends
            { "Nord",              "NordTheme"             },
            { "Dracula",           "DraculaTheme"          },
            { "Gruvbox Dark",      "GruvboxDarkTheme"      },
            { "Catppuccin Mocha",  "CatppuccinMochaTheme"  },
            { "Tokyo Night",       "TokyoNightTheme"       },
            // Light
            { "Light",             "Generic"               },
            { "Office",            "OfficeTheme"           },
            { "Catppuccin Latte",  "CatppuccinLatteTheme"  },
            // Special
            { "Minimal",           "MinimalTheme"          },
            { "Forest",            "ForestTheme"           },
            { "High Contrast",     "HighContrastTheme"     },
        };

    // -- Language map : display name → BCP-47 culture name ----------------
    private static readonly IReadOnlyList<(string Display, string Culture)> LanguageList =
    [
        ("System default",   ""      ),
        ("Arabic (Saudi Arabia)",   "ar-SA"),
        ("German (Germany)",        "de-DE"),
        ("English (United States)", "en-US"),
        ("Spanish (Latin America)", "es-419"),
        ("Spanish (Spain)",         "es-ES"),
        ("French (Canada)",         "fr-CA"),
        ("French (France)",         "fr-FR"),
        ("Hindi (India)",           "hi-IN"),
        ("Italian (Italy)",         "it-IT"),
        ("Japanese (Japan)",        "ja-JP"),
        ("Korean (Korea)",          "ko-KR"),
        ("Dutch (Netherlands)",     "nl-NL"),
        ("Polish (Poland)",         "pl-PL"),
        ("Portuguese (Brazil)",     "pt-BR"),
        ("Portuguese (Portugal)",   "pt-PT"),
        ("Russian (Russia)",        "ru-RU"),
        ("Swedish (Sweden)",        "sv-SE"),
        ("Turkish (Turkey)",        "tr-TR"),
        ("Chinese Simplified",      "zh-CN"),
        ("Ukrainian (Ukraine)",      "uk-UA"),
        ("Czech (Czech Republic)",   "cs-CZ"),
        ("Vietnamese (Vietnam)",     "vi-VN"),
        ("Hungarian (Hungary)",      "hu-HU"),
        ("Romanian (Romania)",       "ro-RO"),
        ("Indonesian (Indonesia)",   "id-ID"),
        ("Thai (Thailand)",          "th-TH"),
        ("Greek (Greece)",           "el-GR"),
        ("Danish (Denmark)",         "da-DK"),
        ("Finnish (Finland)",        "fi-FI"),
    ];

    public event EventHandler? Changed;
    private bool _loading;

    public EnvironmentGeneralPage() => InitializeComponent();

    // -- IOptionsPage ------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            ThemeCombo.ItemsSource = ThemeMap.Keys.ToList();

            var displayName = ThemeMap
                .FirstOrDefault(kv => kv.Value == s.ActiveThemeName)
                .Key ?? ThemeMap.Keys.First();

            ThemeCombo.SelectedItem = displayName;

            // Language
            LanguageCombo.ItemsSource = LanguageList.Select(l => l.Display).ToList();
            var langDisplay = LanguageList
                .FirstOrDefault(l => l.Culture == s.PreferredLanguage)
                .Display ?? LanguageList[0].Display;
            LanguageCombo.SelectedItem = langDisplay;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        if (ThemeCombo.SelectedItem is string key && ThemeMap.TryGetValue(key, out var stem))
            s.ActiveThemeName = stem;

        if (LanguageCombo.SelectedItem is string langDisplay)
        {
            var entry = LanguageList.FirstOrDefault(l => l.Display == langDisplay);
            s.PreferredLanguage = entry.Culture ?? string.Empty;
        }
    }

    // -- Control handlers -------------------------------------------------

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        // Apply live preview immediately
        if (ThemeCombo.SelectedItem is string key && ThemeMap.TryGetValue(key, out var stem))
            ApplyThemeFile(stem);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        if (LanguageCombo.SelectedItem is string langDisplay)
        {
            var entry = LanguageList.FirstOrDefault(l => l.Display == langDisplay);
            var culture = string.IsNullOrEmpty(entry.Culture)
                ? CultureInfo.InstalledUICulture
                : new CultureInfo(entry.Culture);
            LocalizedResourceDictionary.ChangeCulture(culture);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // -- Helpers ----------------------------------------------------------

    private static void ApplyThemeFile(string stem)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/WpfHexEditor.Shell;component/Themes/{stem}.xaml")
            });

        // Sync all open HexEditor instances
        if (Application.Current.MainWindow is { } mainWindow)
            SyncHexEditors(mainWindow);
    }

    private static void SyncHexEditors(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is HexEditorControl hex)
                hex.ApplyThemeFromResources();
            SyncHexEditors(child);
        }
    }
}
