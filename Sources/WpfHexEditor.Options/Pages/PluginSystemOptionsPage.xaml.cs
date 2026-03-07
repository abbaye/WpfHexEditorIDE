//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WpfHexEditor.Options.Pages;

/// <summary>
/// Options page for the Plugin System.
/// Covers: general load behaviour, plugin directory (with Browse), watchdog,
/// diagnostics sampling interval, and slow-plugin detection thresholds.
/// </summary>
public sealed partial class PluginSystemOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public PluginSystemOptionsPage() => InitializeComponent();

    // -- IOptionsPage -------------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            CheckAutoLoad.IsChecked         = s.PluginSystem.AutoLoadPlugins;
            TextPluginsDir.Text             = s.PluginSystem.PluginsDirectory;
            CheckEnableWatchdog.IsChecked   = s.PluginSystem.EnableWatchdog;
            TextWatchdogTimeout.Text        = s.PluginSystem.WatchdogTimeoutSeconds.ToString();
            TextDiagnosticSampling.Text     = s.PluginSystem.DiagnosticSamplingSeconds.ToString();
            TextMonitoringInterval.Text     = s.PluginSystem.MonitoringIntervalSeconds.ToString();
            TextResponseTime.Text           = s.PluginSystem.ResponseTimeThresholdMs.ToString();
            TextCpuThreshold.Text           = s.PluginSystem.CpuThresholdPercent.ToString("F1");
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        s.PluginSystem.AutoLoadPlugins           = CheckAutoLoad.IsChecked == true;
        s.PluginSystem.PluginsDirectory          = TextPluginsDir.Text.Trim();
        s.PluginSystem.EnableWatchdog            = CheckEnableWatchdog.IsChecked == true;
        s.PluginSystem.WatchdogTimeoutSeconds    = ParseInt(TextWatchdogTimeout.Text, 5);
        s.PluginSystem.DiagnosticSamplingSeconds = ParseInt(TextDiagnosticSampling.Text, 5);
        s.PluginSystem.MonitoringIntervalSeconds = ParseInt(TextMonitoringInterval.Text, 5);
        s.PluginSystem.ResponseTimeThresholdMs   = ParseInt(TextResponseTime.Text, 500);
        s.PluginSystem.CpuThresholdPercent       = ParseDouble(TextCpuThreshold.Text, 25.0);
    }

    // -- Control handlers ---------------------------------------------------------

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnBrowsePluginsDir(object sender, RoutedEventArgs e)
    {
        // OpenFolderDialog is available in WPF on .NET 8+
        var dialog = new OpenFolderDialog
        {
            Title = "Select additional plugins directory"
        };

        if (!string.IsNullOrWhiteSpace(TextPluginsDir.Text) && Directory.Exists(TextPluginsDir.Text))
            dialog.InitialDirectory = TextPluginsDir.Text;

        if (dialog.ShowDialog() == true)
        {
            TextPluginsDir.Text = dialog.FolderName;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // -- Helpers ------------------------------------------------------------------

    private static int ParseInt(string text, int fallback)
        => int.TryParse(text, out var v) && v > 0 ? v : fallback;

    private static double ParseDouble(string text, double fallback)
        => double.TryParse(text, System.Globalization.NumberStyles.Any,
                           System.Globalization.CultureInfo.InvariantCulture,
                           out var v) && v > 0 ? v : fallback;
}
