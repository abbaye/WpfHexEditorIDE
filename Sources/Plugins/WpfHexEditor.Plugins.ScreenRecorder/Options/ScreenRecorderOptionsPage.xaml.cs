// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Options/ScreenRecorderOptionsPage.xaml.cs
// Description: Code-behind for ScreenRecorderOptionsPage.
//              DataContext is bound to ScreenRecorderOptions.Instance by constructor.
// ==========================================================

using System.Windows;
using Microsoft.Win32;
using WpfHexEditor.Plugins.ScreenRecorder.Services;

namespace WpfHexEditor.Plugins.ScreenRecorder.Options;

public partial class ScreenRecorderOptionsPage : System.Windows.Controls.UserControl
{
    public ScreenRecorderOptionsPage()
    {
        InitializeComponent();
        DataContext = ScreenRecorderOptions.Instance;
    }

    private void OnBrowseFfmpeg(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select ffmpeg executable",
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        ScreenRecorderOptions.Instance.FfmpegPath = dlg.FileName;
        FfmpegExportService.RefreshAvailability();
    }
}
