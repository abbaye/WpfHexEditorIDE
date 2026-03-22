// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Panels/ClassOutlinePanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for ClassOutlinePanel.
//     Creates and exposes the ClassOutlinePanelViewModel as DataContext.
//     All logic lives in the ViewModel; this file only handles
//     initialisation and the public ViewModel accessor.
//
// Architecture Notes:
//     Pattern: View (MVVM). The ViewModel is instantiated here so the
//     plugin can bind it to the active ClassDiagramSplitHost without
//     needing a DI container.
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;

namespace WpfHexEditor.Plugins.ClassDiagram.Panels;

/// <summary>
/// Dockable panel that lists all class nodes in the active diagram document.
/// </summary>
public partial class ClassOutlinePanel : UserControl
{
    /// <summary>Gets the ViewModel backing this panel.</summary>
    public ClassOutlinePanelViewModel ViewModel { get; }

    public ClassOutlinePanel()
    {
        ViewModel   = new ClassOutlinePanelViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
