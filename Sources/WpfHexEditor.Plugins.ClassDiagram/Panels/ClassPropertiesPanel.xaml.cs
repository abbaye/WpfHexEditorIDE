// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Panels/ClassPropertiesPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for ClassPropertiesPanel.
//     Instantiates and exposes the ClassPropertiesPanelViewModel.
//
// Architecture Notes:
//     Pattern: View (MVVM).
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;

namespace WpfHexEditor.Plugins.ClassDiagram.Panels;

/// <summary>
/// F4-like properties panel for the selected class node or member.
/// </summary>
public partial class ClassPropertiesPanel : UserControl
{
    /// <summary>Gets the ViewModel backing this panel.</summary>
    public ClassPropertiesPanelViewModel ViewModel { get; }

    public ClassPropertiesPanel()
    {
        ViewModel   = new ClassPropertiesPanelViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
