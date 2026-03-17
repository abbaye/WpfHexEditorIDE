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
    public event EventHandler<AssemblyNodeViewModel>?  HighlightInHexEditorRequested;
    public event EventHandler<AssemblyNodeViewModel>?  DecompileRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CopyNameRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CopyFullNameRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CopyOffsetRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CloseAssemblyRequested;
    public event EventHandler?                         CollapseAllRequested;
    public event EventHandler?                         CloseAllAssembliesRequested;
    public event EventHandler<AssemblyNodeViewModel>?  PinAssemblyRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CompareWithRequested;
    public event EventHandler<AssemblyNodeViewModel>?  ExtractToProjectRequested;

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
        if (sender is not ContextMenu menu) return;

        var node = InnerTreeView.SelectedItem as AssemblyNodeViewModel;
        var isRoot = node is AssemblyRootNodeViewModel;

        // "Highlight in Hex Editor" — requires a resolved PE offset.
        if (FindMenuItemByName(menu, "MenuHighlightInHex") is MenuItem menuHighlight)
            menuHighlight.IsEnabled = node?.PeOffset > 0;

        // "Open Assembly File in Hex Editor" — available for any node; uses OwnerFilePath.
        if (FindMenuItemByName(menu, "MenuOpenInHex") is MenuItem menuOpenInHex)
            menuOpenInHex.IsEnabled = node is not null;

        if (FindMenuItemByName(menu, "MenuCopyFull") is MenuItem menuCopyFull)
            menuCopyFull.IsEnabled = node is TypeNodeViewModel;

        if (FindMenuItemByName(menu, "MenuCopyOffset") is MenuItem menuCopyOffset)
            menuCopyOffset.IsEnabled = node is MethodNodeViewModel;

        if (FindMenuItemByName(menu, "MenuDecompile") is MenuItem menuDecompile)
            menuDecompile.IsEnabled = node is TypeNodeViewModel or MethodNodeViewModel or AssemblyRootNodeViewModel;

        if (FindMenuItemByName(menu, "MenuExtractToProject") is MenuItem menuExtract)
            menuExtract.IsEnabled = node is TypeNodeViewModel or MethodNodeViewModel or AssemblyRootNodeViewModel;

        // "Pin Assembly" — root nodes only; update header to reflect current pin state.
        if (FindMenuItemByName(menu, "MenuPin") is MenuItem menuPin)
        {
            menuPin.IsEnabled = isRoot;
            menuPin.Header    = isRoot && node is AssemblyRootNodeViewModel root
                ? (root.IsPinned ? "Unpin Assembly" : "Pin Assembly")
                : "Pin Assembly";
        }

        // "Compare with…" — root nodes only (two assemblies needed).
        if (FindMenuItemByName(menu, "MenuCompareWith") is MenuItem menuCompare)
            menuCompare.IsEnabled = isRoot;

        if (FindMenuItemByName(menu, "MenuCloseAssembly") is MenuItem menuCloseAssembly)
            menuCloseAssembly.IsEnabled = node is not null;
    }

    private static MenuItem? FindMenuItemByName(ContextMenu menu, string name)
    {
        foreach (var item in menu.Items)
        {
            if (item is MenuItem menuItem && menuItem.Name == name)
                return menuItem;
        }
        return null;
    }

    private void OnHighlightInHexEditor(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            HighlightInHexEditorRequested?.Invoke(this, node);
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

    private void OnCloseAssembly(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CloseAssemblyRequested?.Invoke(this, node);
    }

    private void OnPinAssembly(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            PinAssemblyRequested?.Invoke(this, node);
    }

    private void OnCompareWith(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CompareWithRequested?.Invoke(this, node);
    }

    private void OnExtractToProject(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            ExtractToProjectRequested?.Invoke(this, node);
    }

    private void OnCollapseAll(object sender, RoutedEventArgs e)
        => CollapseAllRequested?.Invoke(this, EventArgs.Empty);

    private void OnCloseAllAssemblies(object sender, RoutedEventArgs e)
        => CloseAllAssembliesRequested?.Invoke(this, EventArgs.Empty);
}
