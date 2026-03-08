// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Controls/AssemblyTreeView.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Code-behind for the AssemblyTreeView composite control.
//     Forwards SelectedItemChanged as a public event (NodeSelected).
//     Handles context menu item clicks, delegating to the parent panel.
// ==========================================================

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Controls;

/// <summary>
/// Custom TreeView wrapper for the Assembly Explorer.
/// Raises <see cref="NodeSelected"/> when the selected item changes.
/// Context menu items delegate via events back to the hosting panel.
/// </summary>
public partial class AssemblyTreeView : UserControl
{
    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<AssemblyNodeViewModel>?  NodeSelected;
    public event EventHandler<AssemblyNodeViewModel>?  OpenInHexEditorRequested;
    public event EventHandler<AssemblyNodeViewModel>?  DecompileRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CopyNameRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CopyFullNameRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CopyOffsetRequested;

    // ── ItemsSource passthrough ───────────────────────────────────────────────

    public IEnumerable? ItemsSource
    {
        get => InnerTreeView.ItemsSource;
        set => InnerTreeView.ItemsSource = value;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public AssemblyTreeView()
        => InitializeComponent();

    // ── Selection ─────────────────────────────────────────────────────────────

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is AssemblyNodeViewModel node)
            NodeSelected?.Invoke(this, node);
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        var node = InnerTreeView.SelectedItem as AssemblyNodeViewModel;
        MenuOpenInHex.IsEnabled  = node?.PeOffset > 0;
        MenuCopyFull.IsEnabled   = node is TypeNodeViewModel;
        MenuCopyOffset.IsEnabled = node is MethodNodeViewModel;
        MenuDecompile.IsEnabled  = node is TypeNodeViewModel or MethodNodeViewModel or AssemblyRootNodeViewModel;
    }

    private void OnOpenInHexEditor(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            OpenInHexEditorRequested?.Invoke(this, node);
    }

    private void OnCopyName(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CopyNameRequested?.Invoke(this, node);
    }

    private void OnCopyFullName(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CopyFullNameRequested?.Invoke(this, node);
    }

    private void OnCopyOffset(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CopyOffsetRequested?.Invoke(this, node);
    }

    private void OnDecompile(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            DecompileRequested?.Invoke(this, node);
    }
}
