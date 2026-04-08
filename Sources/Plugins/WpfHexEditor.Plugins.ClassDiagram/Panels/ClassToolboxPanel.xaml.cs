// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Panels/ClassToolboxPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for ClassToolboxPanel.
//     Instantiates and exposes ClassToolboxPanelViewModel.
//     Implements drag-and-drop initiation: PreviewMouseMove detects
//     a left-button drag and starts DragDrop with the selected
//     ToolboxEntry as the payload ("ClassDiagramToolboxEntry").
//
// Architecture Notes:
//     Pattern: View (MVVM).
//     Drop target is DiagramCanvas (AllowDrop=true, OnDrop handles the entry).
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.ClassDiagram.Services;
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

    // ── Drag initiation ──────────────────────────────────────────────────────

    private void OnListBoxPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not ListBox lb) return;

        // Resolve the item under the cursor
        if (lb.SelectedItem is not ToolboxEntry entry) return;

        var data = new DataObject("ClassDiagramToolboxEntry", entry);
        DragDrop.DoDragDrop(lb, data, DragDropEffects.Copy);
    }
}
