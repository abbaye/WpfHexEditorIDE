// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.Options.Pages;

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

            // Standalone file save preferences
            HexEditorDirectSaveCheck.IsChecked   = s.StandaloneFileSave.HexEditorDirectSave;
            CodeEditorDirectSaveCheck.IsChecked  = s.StandaloneFileSave.CodeEditorDirectSave;
            TextEditorDirectSaveCheck.IsChecked  = s.StandaloneFileSave.TextEditorDirectSave;
            TblEditorDirectSaveCheck.IsChecked   = s.StandaloneFileSave.TblEditorDirectSave;
            ImageViewerDirectSaveCheck.IsChecked = s.StandaloneFileSave.ImageViewerDirectSave;
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

        // Standalone file save preferences
        s.StandaloneFileSave.HexEditorDirectSave   = HexEditorDirectSaveCheck.IsChecked   == true;
        s.StandaloneFileSave.CodeEditorDirectSave  = CodeEditorDirectSaveCheck.IsChecked  == true;
        s.StandaloneFileSave.TextEditorDirectSave  = TextEditorDirectSaveCheck.IsChecked  == true;
        s.StandaloneFileSave.TblEditorDirectSave   = TblEditorDirectSaveCheck.IsChecked   == true;
        s.StandaloneFileSave.ImageViewerDirectSave = ImageViewerDirectSaveCheck.IsChecked == true;
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

    private void OnStandaloneSaveChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }
}
