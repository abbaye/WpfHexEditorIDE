// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/PluginDevLoaderOptionsPage.xaml.cs
// Description:
//     Code-behind for the Plugin Dev Loader options page.
//     Exposes EnablePluginDevLoader, WatchedFolders, DevLoaderDebounceMs
//     settings from AppSettings.PluginSystem.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Core.Options.Properties;

namespace WpfHexEditor.Core.Options.Pages;

public partial class PluginDevLoaderOptionsPage : UserControl, IOptionsPage
{
    private bool _loading;

    public PluginDevLoaderOptionsPage() => InitializeComponent();

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            ChkEnableDevLoader.IsChecked = settings.PluginSystem.EnablePluginDevLoader;
            TxtDebounceMs.Text           = settings.PluginSystem.DevLoaderDebounceMs.ToString();
            TxtWatchedFolders.Text       = settings.PluginSystem.WatchedFolders;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings settings)
    {
        settings.PluginSystem.EnablePluginDevLoader = ChkEnableDevLoader.IsChecked == true;
        settings.PluginSystem.WatchedFolders        = TxtWatchedFolders.Text.Trim();

        if (int.TryParse(TxtDebounceMs.Text.Trim(), out var ms) && ms >= 50)
            settings.PluginSystem.DevLoaderDebounceMs = ms;
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = OptionsResources.Opt_PluginDev_SelectOutputDir };
        if (dlg.ShowDialog() != true) return;

        var current = TxtWatchedFolders.Text.Trim();
        TxtWatchedFolders.Text = string.IsNullOrEmpty(current)
            ? dlg.FolderName
            : current + ";" + dlg.FolderName;
    }

    public event EventHandler? Changed;
}
