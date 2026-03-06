// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginManagerControl.xaml.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Code-behind for the Plugin Manager document tab.
//     Sets theme-aware foreground and wires ViewModel lifecycle.
//
// Architecture Notes:
//     Theme compliance: SetResourceReference for foreground text (rule 7b).
//     ViewModel is created externally and passed in via constructor to allow
//     the PluginHost to control lifecycle (dispose on panel close).
//
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// Plugin Manager document tab — lists all plugins with live metrics and lifecycle actions.
/// </summary>
public sealed partial class PluginManagerControl : UserControl
{
    public PluginManagerControl(PluginManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        // Theme-aware foreground (rule 7b)
        SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable d) d.Dispose();
        Unloaded -= OnUnloaded;
    }
}
