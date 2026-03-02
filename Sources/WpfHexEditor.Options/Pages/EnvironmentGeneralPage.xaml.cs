// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HexEditorControl = WpfHexEditor.HexEditor.HexEditor;

namespace WpfHexEditor.Options.Pages;

public sealed partial class EnvironmentGeneralPage : UserControl, IOptionsPage
{
    // ── Theme map : display name → .xaml file stem ───────────────────────
    private static readonly IReadOnlyDictionary<string, string> ThemeMap =
        new Dictionary<string, string>
        {
            { "Dark",            "DarkTheme"         },
            { "Light",           "Generic"            },
            { "VS2022 Dark",     "VS2022DarkTheme"    },
            { "Dark Glass",      "DarkGlassTheme"     },
            { "Visual Studio",   "VisualStudioTheme"  },
            { "Cyberpunk",       "CyberpunkTheme"     },
            { "Minimal",         "MinimalTheme"       },
            { "Office",          "OfficeTheme"        },
        };

    public event EventHandler? Changed;
    private bool _loading;

    public EnvironmentGeneralPage() => InitializeComponent();

    // ── IOptionsPage ──────────────────────────────────────────────────────

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
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        if (ThemeCombo.SelectedItem is string key && ThemeMap.TryGetValue(key, out var stem))
            s.ActiveThemeName = stem;
    }

    // ── Control handlers ─────────────────────────────────────────────────

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        // Apply live preview immediately
        if (ThemeCombo.SelectedItem is string key && ThemeMap.TryGetValue(key, out var stem))
            ApplyThemeFile(stem);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void ApplyThemeFile(string stem)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/WpfHexEditor.Docking.Wpf;component/Themes/{stem}.xaml")
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
