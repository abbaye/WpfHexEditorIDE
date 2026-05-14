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
    public static Task<CaptureRegion?> SelectRegionAsync() =>
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var main         = Application.Current.MainWindow;
            var prevState    = main?.WindowState ?? WindowState.Normal;
            var prevTopmost  = main?.Topmost ?? false;

            // Hide the IDE so the user can select a region over any application.
            if (main is not null)
            {
                main.Topmost      = false;
                main.WindowState  = WindowState.Minimized;
            }

            try
            {
                var win = new RegionSelectorWindow();
                win.ShowDialog();
                return win.SelectedRegion;
            }
            finally
            {
                if (main is not null)
                {
                    main.WindowState = prevState;
                    main.Topmost     = prevTopmost;
                }
            }
        }).Task;
}
