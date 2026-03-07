// ==========================================================
// Project: WpfHexEditor.Sample.Docking
// File: Panels/ExplorerPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Code-behind for ExplorerPanel. Minimal — all content is declared in XAML.
//     Extend this class to load real file-system nodes or bind to a ViewModel.
//
// Architecture Notes:
//     Theme: inherits DockBackgroundBrush, DockTabTextBrush from active theme.
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Sample.Docking.Panels;

public partial class ExplorerPanel : UserControl
{
    public ExplorerPanel()
    {
        InitializeComponent();
    }
}
