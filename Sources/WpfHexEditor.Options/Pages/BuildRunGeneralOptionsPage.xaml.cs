// ==========================================================
// Project: WpfHexEditor.Options
// File: Pages/BuildRunGeneralOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the Build & Run > General options page.
// ==========================================================

using System;
using System.Windows.Controls;

namespace WpfHexEditor.Options.Pages;

public sealed partial class BuildRunGeneralOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public BuildRunGeneralOptionsPage() => InitializeComponent();

    // -- IOptionsPage ----------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            var b = s.BuildRun;
            CheckSaveBeforeBuild.IsChecked = b.SaveBeforeBuilding;
            CheckRunInProcess.IsChecked    = b.RunInProcess;
            CheckShowOnStart.IsChecked     = b.ShowOutputOnBuildStart;
            CheckShowOnError.IsChecked     = b.ShowOutputOnBuildError;
            TxtMaxParallel.Text            = b.MaxParallelProjects.ToString();
            SelectComboByTag(CbVerbosity, b.OutputVerbosity);
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        var b = s.BuildRun;
        b.SaveBeforeBuilding    = CheckSaveBeforeBuild.IsChecked == true;
        b.RunInProcess          = CheckRunInProcess.IsChecked == true;
        b.ShowOutputOnBuildStart = CheckShowOnStart.IsChecked == true;
        b.ShowOutputOnBuildError = CheckShowOnError.IsChecked == true;
        b.OutputVerbosity       = (CbVerbosity.SelectedItem as ComboBoxItem)?.Tag as string ?? "Minimal";

        if (int.TryParse(TxtMaxParallel.Text, out var n) && n > 0)
            b.MaxParallelProjects = n;
    }

    // -- Handlers --------------------------------------------------------------

    private void OnCheckChanged(object s, System.Windows.RoutedEventArgs e)
    { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); }

    private void OnComboChanged(object s, SelectionChangedEventArgs e)
    { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); }

    private void OnTextLostFocus(object s, System.Windows.RoutedEventArgs e)
    { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); }

    // -- Helpers ---------------------------------------------------------------

    private static void SelectComboByTag(ComboBox cb, string tag)
    {
        foreach (ComboBoxItem item in cb.Items)
        {
            if (item.Tag as string == tag)
            {
                cb.SelectedItem = item;
                return;
            }
        }
        if (cb.Items.Count > 0) cb.SelectedIndex = 0;
    }
}
