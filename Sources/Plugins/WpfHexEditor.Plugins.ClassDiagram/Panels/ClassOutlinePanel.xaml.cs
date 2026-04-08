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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;

namespace WpfHexEditor.Plugins.ClassDiagram.Panels;

/// <summary>
/// Dockable panel that lists all class nodes in the active diagram document.
/// </summary>
public partial class ClassOutlinePanel : UserControl
{
    /// <summary>Gets the ViewModel backing this panel.</summary>
    public ClassOutlinePanelViewModel ViewModel { get; }

    /// <summary>Raised when the user double-clicks a member to navigate to source.</summary>
    public event EventHandler<(ClassNode Node, ClassMember? Member)>? NavigateToMemberRequested;

    public ClassOutlinePanel()
    {
        ViewModel   = new ClassOutlinePanelViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ClassNodeViewModel nodeVm)
            ViewModel.SelectedNode = nodeVm;
        else if (e.NewValue is ClassMemberViewModel memberVm)
        {
            // Find parent node
            var parentNode = ViewModel.Nodes.FirstOrDefault(n =>
                n.MemberViewModels.Contains(memberVm));
            if (parentNode is not null)
                ViewModel.SelectedNode = parentNode;
        }
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NodeTree.SelectedItem is ClassNodeViewModel nodeVm)
        {
            NavigateToMemberRequested?.Invoke(this, (nodeVm.Node, null));
        }
        else if (NodeTree.SelectedItem is ClassMemberViewModel memberVm)
        {
            var parentNode = ViewModel.Nodes.FirstOrDefault(n =>
                n.MemberViewModels.Contains(memberVm));
            if (parentNode is not null)
                NavigateToMemberRequested?.Invoke(this, (parentNode.Node, memberVm.Member));
        }
    }
}
