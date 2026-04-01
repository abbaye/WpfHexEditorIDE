//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WpfHexEditor.Core.Options.Pages;

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

            // Memory alert thresholds
            CheckEnableMemoryAlerts.IsChecked        = s.PluginSystem.EnableMemoryAlerts;
            CheckShowMemoryColorGradation.IsChecked  = s.PluginSystem.ShowMemoryColorGradation;
            TextMemoryWarningThreshold.Text          = s.PluginSystem.MemoryWarningThresholdMB.ToString();
            TextMemoryHighThreshold.Text             = s.PluginSystem.MemoryHighThresholdMB.ToString();
            TextMemoryCriticalThreshold.Text         = s.PluginSystem.MemoryCriticalThresholdMB.ToString();

            // Memory alert colors - load into ColorPickers
            if (FindName("PickerMemoryNormal") is ColorPicker.Controls.ColorPicker pickerNormal)
                pickerNormal.SelectedColor = ParseHexColor(s.PluginSystem.MemoryNormalColor);

            if (FindName("PickerMemoryWarning") is ColorPicker.Controls.ColorPicker pickerWarning)
                pickerWarning.SelectedColor = ParseHexColor(s.PluginSystem.MemoryWarningColor);

            if (FindName("PickerMemoryHigh") is ColorPicker.Controls.ColorPicker pickerHigh)
                pickerHigh.SelectedColor = ParseHexColor(s.PluginSystem.MemoryHighColor);

            if (FindName("PickerMemoryCritical") is ColorPicker.Controls.ColorPicker pickerCritical)
                pickerCritical.SelectedColor = ParseHexColor(s.PluginSystem.MemoryCriticalColor);
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

        // Memory alert thresholds
        s.PluginSystem.EnableMemoryAlerts           = CheckEnableMemoryAlerts.IsChecked == true;
        s.PluginSystem.ShowMemoryColorGradation     = CheckShowMemoryColorGradation.IsChecked == true;
        s.PluginSystem.MemoryWarningThresholdMB     = ParseInt(TextMemoryWarningThreshold.Text, 500);
        s.PluginSystem.MemoryHighThresholdMB        = ParseInt(TextMemoryHighThreshold.Text, 750);
        s.PluginSystem.MemoryCriticalThresholdMB    = ParseInt(TextMemoryCriticalThreshold.Text, 1000);

        // Memory alert colors - get from ColorPickers
        if (FindName("PickerMemoryNormal") is ColorPicker.Controls.ColorPicker pickerNormal)
            s.PluginSystem.MemoryNormalColor = ColorToHex(pickerNormal.SelectedColor);

        if (FindName("PickerMemoryWarning") is ColorPicker.Controls.ColorPicker pickerWarning)
            s.PluginSystem.MemoryWarningColor = ColorToHex(pickerWarning.SelectedColor);

        if (FindName("PickerMemoryHigh") is ColorPicker.Controls.ColorPicker pickerHigh)
            s.PluginSystem.MemoryHighColor = ColorToHex(pickerHigh.SelectedColor);

        if (FindName("PickerMemoryCritical") is ColorPicker.Controls.ColorPicker pickerCritical)
            s.PluginSystem.MemoryCriticalColor = ColorToHex(pickerCritical.SelectedColor);
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

    // -- Memory Alert Color Pickers -----------------------------------------------

    private void OnColorPickerChanged(object sender, System.Windows.Media.Color e)
    {
        if (_loading) return;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnResetMemoryColors(object sender, RoutedEventArgs e)
    {
        _loading = true;
        try
        {
            if (FindName("PickerMemoryNormal") is ColorPicker.Controls.ColorPicker pickerNormal)
                pickerNormal.SelectedColor = ParseHexColor("#22C55E");
            if (FindName("PickerMemoryWarning") is ColorPicker.Controls.ColorPicker pickerWarning)
                pickerWarning.SelectedColor = ParseHexColor("#EAB308");
            if (FindName("PickerMemoryHigh") is ColorPicker.Controls.ColorPicker pickerHigh)
                pickerHigh.SelectedColor = ParseHexColor("#F97316");
            if (FindName("PickerMemoryCritical") is ColorPicker.Controls.ColorPicker pickerCritical)
                pickerCritical.SelectedColor = ParseHexColor("#EF4444");
        }
        finally
        {
            _loading = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static System.Windows.Media.Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            hex = "FF" + hex; // Add alpha if missing

        return System.Windows.Media.Color.FromArgb(
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16),
            Convert.ToByte(hex.Substring(6, 2), 16));
    }

    private static string ColorToHex(System.Windows.Media.Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
