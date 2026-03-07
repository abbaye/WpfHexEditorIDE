// ==========================================================
// Project: WpfHexEditor.Sample.Docking
// File: Panels/WelcomePanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Code-behind for WelcomePanel. Minimal — all content is in XAML with
//     DynamicResource bindings that auto-update when the theme changes.
//
// Architecture Notes:
//     Theme: DockTabTextBrush, DockTabActiveBrush, DockBackgroundBrush (dynamic).
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Sample.Docking.Panels;

public partial class WelcomePanel : UserControl
{
    public WelcomePanel()
    {
        InitializeComponent();
    }
}
