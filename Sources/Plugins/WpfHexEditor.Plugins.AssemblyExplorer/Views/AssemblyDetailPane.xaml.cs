// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/AssemblyDetailPane.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Code-behind for the detail pane. Minimal — all state is in ViewModel.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

/// <summary>
/// Detail pane showing stub decompiled text and metadata for the selected tree node.
/// </summary>
public partial class AssemblyDetailPane : UserControl
{
    public AssemblyDetailPane()
    {
        InitializeComponent();

        // Add BoolToVisibilityConverter as a resource accessible from XAML.
        Resources.Add("BoolToVisibilityConverter", new BooleanToVisibilityConverter());
    }
}
