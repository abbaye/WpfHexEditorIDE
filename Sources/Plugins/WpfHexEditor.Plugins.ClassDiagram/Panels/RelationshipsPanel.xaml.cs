// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Panels/RelationshipsPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for RelationshipsPanel.
//     Instantiates and exposes RelationshipsPanelViewModel.
//
// Architecture Notes:
//     Pattern: View (MVVM).
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;

namespace WpfHexEditor.Plugins.ClassDiagram.Panels;

/// <summary>
/// Dockable panel listing all directed relationships in the active diagram document.
/// </summary>
public partial class RelationshipsPanel : UserControl
{
    /// <summary>Gets the ViewModel backing this panel.</summary>
    public RelationshipsPanelViewModel ViewModel { get; }

    public RelationshipsPanel()
    {
        ViewModel   = new RelationshipsPanelViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
