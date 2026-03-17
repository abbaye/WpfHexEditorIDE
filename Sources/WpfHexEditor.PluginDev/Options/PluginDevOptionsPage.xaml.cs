// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Options/PluginDevOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the Plugin Development options page.
// ==========================================================

using System;
using System.Windows.Controls;
using WpfHexEditor.Options;

namespace WpfHexEditor.PluginDev.Options;

/// <summary>
/// Options page for Plugin Development settings.
/// </summary>
public sealed partial class PluginDevOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public PluginDevOptionsPage() => InitializeComponent();

    // -- IOptionsPage ----------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            var p = s.PluginDev;
            CheckAutoRebuild.IsChecked      = p.AutoRebuildOnSave;
            CheckCopyOnBuild.IsChecked      = p.CopyOutputOnBuild;
            CheckStatusBarIndicator.IsChecked = p.ShowStatusBarIndicator;
            TbTimeout.Text                  = p.LifecycleTimeoutMs.ToString();
            SelectComboByTag(CbLogLevel,    p.LogLevel);
            SelectComboByTag(CbSandboxMode, p.SandboxMode);
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        var p = s.PluginDev;
        p.AutoRebuildOnSave      = CheckAutoRebuild.IsChecked      == true;
        p.CopyOutputOnBuild      = CheckCopyOnBuild.IsChecked      == true;
        p.ShowStatusBarIndicator = CheckStatusBarIndicator.IsChecked == true;

        if (int.TryParse(TbTimeout.Text, out var ms) && ms > 0)
            p.LifecycleTimeoutMs = ms;

        if (CbLogLevel.SelectedItem is ComboBoxItem lItem)
            p.LogLevel = lItem.Tag as string ?? "Info";

        if (CbSandboxMode.SelectedItem is ComboBoxItem sItem)
            p.SandboxMode = sItem.Tag as string ?? "Light";
    }

    // -- Handlers --------------------------------------------------------------

    private void OnChanged(object s, System.Windows.RoutedEventArgs e)
    { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); }

    // -- Helpers ---------------------------------------------------------------

    private static void SelectComboByTag(ComboBox cb, string tag)
    {
        foreach (ComboBoxItem item in cb.Items)
        {
            if (item.Tag as string == tag)
            {
                cb.SelectedItem = item;
                return;
            }
        }
        if (cb.Items.Count > 0) cb.SelectedIndex = 0;
    }
}
