// ==========================================================
// Project: WpfHexEditor.App
// File: PluginQuickStatusPopup.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Code-behind for PluginQuickStatusPopup UserControl.
//     The UserControl is embedded inside a WPF Popup anchored
//     to the StatusBar plugin indicator and closes on lost focus.
//
// Architecture Notes:
//     - Popup is created and owned by MainWindow.PluginSystem.cs
//     - DataContext is PluginQuickStatusViewModel
//     - Popup.StaysOpen = false handles auto-close on focus loss
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// Lightweight non-modal popup control displaying a compact plugin status list.
/// </summary>
public partial class PluginQuickStatusPopup : UserControl
{
    public PluginQuickStatusPopup()
    {
        InitializeComponent();
        SetResourceReference(ForegroundProperty, "DockForegroundBrush");
    }
}
