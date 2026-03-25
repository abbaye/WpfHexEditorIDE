//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Core.Options.Pages;

public sealed partial class SolutionExplorerOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public SolutionExplorerOptionsPage() => InitializeComponent();

    // -- IOptionsPage ------------------------------------------------------

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

    // -- Control handlers -------------------------------------------------

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // -- Helpers ----------------------------------------------------------

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
