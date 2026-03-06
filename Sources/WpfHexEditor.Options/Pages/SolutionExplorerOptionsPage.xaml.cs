// ==========================================================
// Project: WpfHexEditor.Options
// File: SolutionExplorerOptionsPage.xaml.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Code-behind for the Solution Explorer options page.
//     Implements IOptionsPage — load/flush pattern with auto-save.
//
// Architecture Notes:
//     Pattern: IOptionsPage (Load / Flush / Changed)
//     Theme: inherits DynamicResource brushes from OptionsEditorControl
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Options.Pages;

public sealed partial class SolutionExplorerOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public SolutionExplorerOptionsPage() => InitializeComponent();

    // ── IOptionsPage ──────────────────────────────────────────────────────

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            CheckTrackActive.IsChecked    = s.SolutionExplorer.TrackActiveDocument;
            CheckPersistCollapse.IsChecked = s.SolutionExplorer.PersistCollapseState;
            CheckNotifications.IsChecked  = s.SolutionExplorer.ShowContextualNotifications;
            SelectComboByTag(SortCombo,   s.SolutionExplorer.DefaultSortMode);
            SelectComboByTag(FilterCombo, s.SolutionExplorer.DefaultFilterMode);
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        s.SolutionExplorer.TrackActiveDocument         = CheckTrackActive.IsChecked    == true;
        s.SolutionExplorer.PersistCollapseState        = CheckPersistCollapse.IsChecked == true;
        s.SolutionExplorer.ShowContextualNotifications = CheckNotifications.IsChecked   == true;
        s.SolutionExplorer.DefaultSortMode   = ReadComboTag(SortCombo,   "None");
        s.SolutionExplorer.DefaultFilterMode = ReadComboTag(FilterCombo, "All");
    }

    // ── Control handlers ─────────────────────────────────────────────────

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static string ReadComboTag(ComboBox combo, string fallback)
        => combo.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? fallback
            : fallback;
}
