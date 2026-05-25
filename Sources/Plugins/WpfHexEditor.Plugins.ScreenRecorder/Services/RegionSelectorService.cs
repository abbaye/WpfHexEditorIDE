// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Services/RegionSelectorService.cs
// Description: Shows the full-screen rubber-band selector and returns the chosen region.
//              Minimizes the IDE main window before showing the selector so the user
//              can draw over any application — not just the IDE itself.

using System.Windows;
using WpfHexEditor.Plugins.ScreenRecorder.Models;
using WpfHexEditor.Plugins.ScreenRecorder.Overlay;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public static class RegionSelectorService
{
    public static async Task<CaptureRegion?> SelectRegionAsync()
    {
        // Hide all open windows so the selector overlays the full desktop cleanly.
        var hiddenWindows = await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var windows = Application.Current.Windows
                .OfType<Window>()
                .Where(w => w.IsVisible)
                .ToList();
            foreach (var w in windows)
                w.Hide();
            return windows;
        });

        // Give Windows time to actually remove the windows from screen.
        await Task.Delay(200);

        var region = await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var win = new RegionSelectorWindow();
            win.ShowDialog();
            return win.SelectedRegion;
        });

        // Restore all hidden windows.
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var w in hiddenWindows)
                w.Show();
            Application.Current.MainWindow?.Activate();
        });

        return region;
    }
}
