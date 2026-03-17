// ==========================================================
// Project: WpfHexEditor.Options
// File: Pages/BuildCompilerOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the Build & Run > Compiler options page.
// ==========================================================

using System;
using System.Windows.Controls;

namespace WpfHexEditor.Options.Pages;

public sealed partial class BuildCompilerOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public BuildCompilerOptionsPage() => InitializeComponent();

    // -- IOptionsPage ----------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            var b = s.BuildRun;
            CheckNullableErrors.IsChecked  = b.TreatNullableWarningsAsErrors;
            CheckImplicitUsings.IsChecked  = b.EnableImplicitUsings;
            CheckGenerateDocs.IsChecked    = b.GenerateDocumentation;
            SelectComboByTag(CbWarnLevel, b.DefaultWarningLevel.ToString());
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        var b = s.BuildRun;
        b.TreatNullableWarningsAsErrors = CheckNullableErrors.IsChecked == true;
        b.EnableImplicitUsings          = CheckImplicitUsings.IsChecked == true;
        b.GenerateDocumentation         = CheckGenerateDocs.IsChecked == true;

        if (CbWarnLevel.SelectedItem is ComboBoxItem item
         && int.TryParse(item.Tag as string, out var level))
            b.DefaultWarningLevel = level;
    }

    // -- Handlers --------------------------------------------------------------

    private void OnCheckChanged(object s, System.Windows.RoutedEventArgs e)
    { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); }

    private void OnComboChanged(object s, SelectionChangedEventArgs e)
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
