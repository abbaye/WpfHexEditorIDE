// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Options.Pages;

public sealed partial class EnvironmentSavePage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public EnvironmentSavePage() => InitializeComponent();

    // -- IOptionsPage ------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            RadioDirect.IsChecked        = s.DefaultFileSaveMode == FileSaveMode.Direct;
            RadioTracked.IsChecked       = s.DefaultFileSaveMode == FileSaveMode.Tracked;
            CheckAutoSerialize.IsChecked = s.AutoSerializeEnabled;
            TxtInterval.Text             = s.AutoSerializeIntervalSeconds.ToString();
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        s.DefaultFileSaveMode = RadioTracked.IsChecked == true
            ? FileSaveMode.Tracked
            : FileSaveMode.Direct;

        s.AutoSerializeEnabled = CheckAutoSerialize.IsChecked == true;

        if (int.TryParse(TxtInterval.Text, out int secs) && secs > 0)
            s.AutoSerializeIntervalSeconds = secs;
    }

    // -- Control handlers -------------------------------------------------

    private void OnSaveModeChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnAutoSerializeChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnIntervalLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }
}
