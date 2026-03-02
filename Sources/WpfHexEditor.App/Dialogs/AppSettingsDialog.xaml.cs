// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Windows;
using WpfHexEditor.App.Settings;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Simple settings dialog — edits a copy of <see cref="AppSettings"/>
/// and writes back to <see cref="AppSettingsService"/> on OK.
/// </summary>
public sealed partial class AppSettingsDialog : Window
{
    public AppSettingsDialog()
    {
        InitializeComponent();
        LoadFromService();
    }

    // ── Load / Commit ──────────────────────────────────────────────────────

    private void LoadFromService()
    {
        var s = AppSettingsService.Instance.Current;

        RadioDirect.IsChecked    = s.DefaultFileSaveMode == FileSaveMode.Direct;
        RadioTracked.IsChecked   = s.DefaultFileSaveMode == FileSaveMode.Tracked;
        CheckAutoSerialize.IsChecked = s.AutoSerializeEnabled;
        TxtInterval.Text         = s.AutoSerializeIntervalSeconds.ToString();
    }

    private void CommitToService()
    {
        var s = AppSettingsService.Instance.Current;

        s.DefaultFileSaveMode = RadioTracked.IsChecked == true
            ? FileSaveMode.Tracked
            : FileSaveMode.Direct;

        s.AutoSerializeEnabled = CheckAutoSerialize.IsChecked == true;

        if (int.TryParse(TxtInterval.Text, out int secs) && secs > 0)
            s.AutoSerializeIntervalSeconds = secs;

        AppSettingsService.Instance.Save();
    }

    // ── Buttons ────────────────────────────────────────────────────────────

    private void OnOk(object sender, RoutedEventArgs e)
    {
        CommitToService();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
