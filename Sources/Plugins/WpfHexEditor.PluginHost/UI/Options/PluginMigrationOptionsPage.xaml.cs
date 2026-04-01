// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginMigrationOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Code-behind for the Plugin Migration options page.
//     Registered in the IDE Options dialog under "Plugin System / Migration"
//     via OptionsPageRegistry.RegisterDynamic() in MainWindow.PluginSystem.cs.
//
// Architecture Notes:
//     No ViewModel — page is simple enough for direct code-behind wiring.
//     Reads / writes PluginMigrationPolicy via WpfPluginHost.UpdateMigrationPolicy().
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfHexEditor.PluginHost.Services;

namespace WpfHexEditor.PluginHost.UI.Options;

/// <summary>
/// Options page for the dynamic plugin migration feature.
/// Accessible via the IDE Options dialog: Plugin System → Migration.
/// </summary>
public sealed partial class PluginMigrationOptionsPage : UserControl
{
    private readonly WpfPluginHost _host;
    private DispatcherTimer? _savedFadeTimer;

    public PluginMigrationOptionsPage(WpfPluginHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // -- Initialisation -----------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PopulateFields(_host.MigrationPolicy);
    }

    private void PopulateFields(PluginMigrationPolicy policy)
    {
        // Mode ComboBox — match tag to enum name
        foreach (ComboBoxItem item in ModeComboBox.Items)
        {
            if (item.Tag?.ToString() == policy.Mode.ToString())
            {
                ModeComboBox.SelectedItem = item;
                break;
            }
        }

        MemorySuggestTextBox.Text = policy.MemorySuggestThresholdMb.ToString();
        MemoryAutoTextBox.Text    = policy.MemoryAutoMigrateThresholdMb.ToString();
        CpuSuggestTextBox.Text    = policy.CpuSuggestThresholdPercent.ToString("F0");
        CpuAutoTextBox.Text       = policy.CpuAutoMigrateThresholdPercent.ToString("F0");
        CpuWindowTextBox.Text     = policy.CpuSustainedWindowSeconds.ToString();
        CrashCountTextBox.Text    = policy.CrashCountThreshold.ToString();
    }

    // -- Event handlers -----------------------------------------------------------

    private void OnModeChanged(object sender, SelectionChangedEventArgs e) { /* live preview if needed */ }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        HideMessages();

        if (!TryBuildPolicy(out var policy, out var error))
        {
            ShowError(error);
            return;
        }

        _host.UpdateMigrationPolicy(policy!);
        ShowSaved();
    }

    private void OnResetDefaults(object sender, RoutedEventArgs e)
    {
        HideMessages();
        var defaults = PluginMigrationPolicy.CreateDefault();
        PopulateFields(defaults);
        _host.UpdateMigrationPolicy(defaults);
        ShowSaved();
    }

    // -- Parsing ------------------------------------------------------------------

    private bool TryBuildPolicy(out PluginMigrationPolicy? policy, out string error)
    {
        policy = null;

        var mode = ParseMode();
        if (mode is null) { error = "Invalid migration mode."; return false; }

        if (!int.TryParse(MemorySuggestTextBox.Text.Trim(), out var memSuggest) || memSuggest <= 0)
        { error = "Memory suggest threshold must be a positive integer (MB)."; return false; }

        if (!int.TryParse(MemoryAutoTextBox.Text.Trim(), out var memAuto) || memAuto < memSuggest)
        { error = "Memory auto-migrate threshold must be ≥ suggest threshold."; return false; }

        if (!double.TryParse(CpuSuggestTextBox.Text.Trim(), out var cpuSuggest) || cpuSuggest is < 0 or > 100)
        { error = "CPU suggest threshold must be between 0 and 100."; return false; }

        if (!double.TryParse(CpuAutoTextBox.Text.Trim(), out var cpuAuto) || cpuAuto < cpuSuggest)
        { error = "CPU auto-migrate threshold must be ≥ suggest threshold."; return false; }

        if (!int.TryParse(CpuWindowTextBox.Text.Trim(), out var cpuWindow) || cpuWindow <= 0)
        { error = "CPU sustained window must be a positive integer (seconds)."; return false; }

        if (!int.TryParse(CrashCountTextBox.Text.Trim(), out var crashCount) || crashCount <= 0)
        { error = "Crash count threshold must be a positive integer."; return false; }

        policy = new PluginMigrationPolicy
        {
            Mode                         = mode.Value,
            MemorySuggestThresholdMb     = memSuggest,
            MemoryAutoMigrateThresholdMb = memAuto,
            CpuSuggestThresholdPercent   = cpuSuggest,
            CpuAutoMigrateThresholdPercent = cpuAuto,
            CpuSustainedWindowSeconds    = cpuWindow,
            CrashCountThreshold          = crashCount
        };

        error = string.Empty;
        return true;
    }

    private PluginMigrationMode? ParseMode()
    {
        if (ModeComboBox.SelectedItem is not ComboBoxItem item) return null;
        if (Enum.TryParse<PluginMigrationMode>(item.Tag?.ToString(), out var mode))
            return mode;
        return null;
    }

    // -- UI helpers ---------------------------------------------------------------

    private void ShowError(string message)
    {
        ErrorMessage.Text       = message;
        ErrorMessage.Visibility = Visibility.Visible;
    }

    private void ShowSaved()
    {
        SavedMessage.Visibility = Visibility.Visible;
        // Auto-hide after 3 seconds
        _savedFadeTimer?.Stop();
        _savedFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _savedFadeTimer.Tick += (_, _) =>
        {
            SavedMessage.Visibility = Visibility.Collapsed;
            _savedFadeTimer?.Stop();
        };
        _savedFadeTimer.Start();
    }

    private void HideMessages()
    {
        ErrorMessage.Visibility = Visibility.Collapsed;
        SavedMessage.Visibility = Visibility.Collapsed;
    }
}
