// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentStructure
// File: Views/DocumentStructurePanel.xaml.cs
// Created: 2026-04-05
// Updated: 2026-04-06
// Description:
//     Code-behind for the Document Structure panel XAML.
//     Handles tree selection, double-click navigation, responsive toolbar
//     (overflow collapse at < 260px), and refresh.
//
// Architecture Notes:
//     Minimal code-behind — delegates to DocumentStructureViewModel.
//     Responsive toolbar mirrors DisassemblyViewer pattern.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Plugins.DocumentStructure.ViewModels;

namespace WpfHexEditor.Plugins.DocumentStructure.Views;

public partial class DocumentStructurePanel : UserControl
{
    // 5 left buttons (~30px each) + filter box (140px) + sort combo (110px) + margins ≈ 450px
    private const double CollapseThreshold = 320.0;

    private DocumentStructureViewModel? Vm => DataContext as DocumentStructureViewModel;

    /// <summary>Raised when the user requests to refresh the structure (e.g. via the Refresh button).</summary>
    public event EventHandler? RefreshRequested;

    public DocumentStructurePanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Vm is { } vm)
                vm.ScrollToNodeRequested += OnScrollToNodeRequested;
        };
        Unloaded += (_, _) =>
        {
            if (Vm is { } vm)
                vm.ScrollToNodeRequested -= OnScrollToNodeRequested;
        };
    }

    // ── Tree ────────────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Single click = highlight only (caret tracking handles visual feedback)
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // MouseDoubleClick bubbles through each TreeViewItem — guard against multi-fire.
        if (e.Handled) return;
        if (e.OriginalSource is not DependencyObject src) return;
        var tvi = FindAncestorOrSelf<TreeViewItem>(src);
        if (tvi is null) return;

        e.Handled = true;
        if (StructureTree.SelectedItem is StructureNodeVm node)
            Vm?.OnNodeActivated(node);
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject obj) where T : DependencyObject
    {
        while (obj is not null)
        {
            if (obj is T t) return t;
            // FrameworkContentElement (Run, Span, etc.) is not a Visual — use logical tree.
            obj = obj is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(obj)
                : LogicalTreeHelper.GetParent(obj);
        }
        return null;
    }

    // ── Flat list ───────────────────────────────────────────────────────────

    private void OnFlatSelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void OnFlatDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        if (FlatList.SelectedItem is StructureNodeVm node)
            Vm?.OnNodeActivated(node);
    }

    // ── Context menu ────────────────────────────────────────────────────────

    private void OnContextMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag } mi) return;

        // Prefer the right-clicked item's DataContext over SelectedItem
        StructureNodeVm? node = null;
        if (mi.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement pt)
            node = pt.DataContext as StructureNodeVm;
        node ??= (StructureNodeVm?)(StructureTree.SelectedItem ?? FlatList.SelectedItem);
        if (node is null) return;

        switch (tag)
        {
            case "navigate":       Vm?.OnNodeActivated(node); break;
            case "copy-name":      Clipboard.SetText(node.Name); break;
            case "copy-qualified": Clipboard.SetText(Vm?.BuildQualifiedName(node) ?? node.Name); break;
            case "expand-all":     Vm?.ExpandAll(); break;
            case "collapse-all":   Vm?.CollapseAll(); break;
        }
    }

    // ── Toolbar extra buttons ────────────────────────────────────────────────

    private void OnCollapseAllClicked(object sender, RoutedEventArgs e)
        => Vm?.CollapseAll();

    // ── Scroll sync ──────────────────────────────────────────────────────────

    private void OnScrollToNodeRequested(object? sender, StructureNodeVm node)
    {
        if (Vm?.IsTreeMode == true)
            ScrollTreeToNode(node);
        else
            ScrollFlatToNode(node);
    }

    private void ScrollTreeToNode(StructureNodeVm target)
    {
        // Ensure all ancestors are expanded so the item is in the visual tree.
        if (Vm is { } vm) ExpandPathTo(vm.RootNodes, target);
        StructureTree.UpdateLayout();
        BringTreeItemIntoView(StructureTree, StructureTree.ItemContainerGenerator, target);
    }

    private static bool ExpandPathTo(System.Collections.ObjectModel.ObservableCollection<StructureNodeVm> nodes, StructureNodeVm target)
    {
        foreach (var n in nodes)
        {
            if (ReferenceEquals(n, target)) return true;
            if (ExpandPathTo(n.Children, target))
            {
                n.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    private static void BringTreeItemIntoView(
        ItemsControl container,
        ItemContainerGenerator generator,
        StructureNodeVm target)
    {
        foreach (var item in container.Items)
        {
            if (generator.ContainerFromItem(item) is TreeViewItem tvi)
            {
                if (item is StructureNodeVm vm && ReferenceEquals(vm, target))
                {
                    tvi.BringIntoView();
                    return;
                }
                tvi.UpdateLayout();
                BringTreeItemIntoView(tvi, tvi.ItemContainerGenerator, target);
            }
        }
    }

    private void ScrollFlatToNode(StructureNodeVm target)
    {
        FlatList.ScrollIntoView(target);
    }

    // ── Refresh ─────────────────────────────────────────────────────────────

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
        => RefreshRequested?.Invoke(this, EventArgs.Empty);

    // ── Responsive toolbar ──────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var collapsed = e.NewSize.Width < CollapseThreshold;
        TbgSort.Visibility               = collapsed ? Visibility.Collapsed : Visibility.Visible;
        TbgDepth.Visibility              = collapsed ? Visibility.Collapsed : Visibility.Visible;
        ToolbarOverflowButton.Visibility = collapsed ? Visibility.Visible   : Visibility.Collapsed;
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
        => OverflowContextMenu.IsOpen = true;

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var sortIdx  = Vm.SortModeIndex;
        var depthIdx = Vm.MaxDepthIndex;
        foreach (var obj in OverflowContextMenu.Items)
        {
            if (obj is not MenuItem item || item.Tag is not string tag) continue;
            if (tag.StartsWith('s') && int.TryParse(tag[1..], out var sIdx))
                item.IsChecked = sIdx == sortIdx;
            else if (tag.StartsWith('d') && int.TryParse(tag[1..], out var dIdx))
                item.IsChecked = dIdx == depthIdx;
        }
    }

    private void OnOverflowSortClicked(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (sender is MenuItem { Tag: string tag } && tag.StartsWith('s') && int.TryParse(tag[1..], out var idx))
            Vm.SortModeIndex = idx;
    }

    private void OnOverflowDepthClicked(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (sender is MenuItem { Tag: string tag } && tag.StartsWith('d') && int.TryParse(tag[1..], out var idx))
            Vm.MaxDepthIndex = idx;
    }
}
