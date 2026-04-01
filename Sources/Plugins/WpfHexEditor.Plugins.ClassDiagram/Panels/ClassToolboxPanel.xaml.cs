// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Panels/ClassToolboxPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for ClassToolboxPanel.
//     Instantiates and exposes ClassToolboxPanelViewModel.
//
// Architecture Notes:
//     Pattern: View (MVVM).
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;

namespace WpfHexEditor.Plugins.ClassDiagram.Panels;

/// <summary>
/// Dockable toolbox panel listing draggable diagram element types.
/// </summary>
public partial class ClassToolboxPanel : UserControl
{
    /// <summary>Gets the ViewModel backing this panel.</summary>
    public ClassToolboxPanelViewModel ViewModel { get; }

    public ClassToolboxPanel()
    {
        ViewModel   = new ClassToolboxPanelViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
