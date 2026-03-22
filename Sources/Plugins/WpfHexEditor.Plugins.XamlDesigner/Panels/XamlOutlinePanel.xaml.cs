// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
//          2026-03-22 — Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.Panels).
// File: XamlOutlinePanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-19 — Search highlight, semantic icons, inline rename, context menu, breadcrumb
// Description:
//     Code-behind for the XAML Outline dockable panel.
//     Wires toolbar buttons, search TextBox, inline rename, and context menu commands.
//     Follows the OnLoaded/OnUnloaded lifecycle rule: never null _vm.
//     Exposes events for Delete, Move, Wrap, Navigate, and Sync so the plugin
//     host (XamlDesignerPlugin) can handle each action.
//
// Architecture Notes:
//     VS-Like Panel Pattern — 26px toolbar + tree content + 22px breadcrumb.
//     ToolbarOverflowManager manages TbgNavigation collapse on narrow widths.
//     Context menu commands are wired from code-behind using MenuItem.Tag to
//     differentiate actions, keeping XAML clean of command bindings.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Plugins.XamlDesigner.ViewModels;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.XamlDesigner.Panels;

// ── Event args ────────────────────────────────────────────────────────────────

/// <summary>Carries the node that the user requested to delete.</summary>
public sealed class DeleteRequestedEventArgs : EventArgs
{
    public XamlOutlineNode Node { get; }
    public DeleteRequestedEventArgs(XamlOutlineNode node) => Node = node;
}

/// <summary>Carries the node and direction for a move-up/move-down request.</summary>
public sealed class MoveRequestedEventArgs : EventArgs
{
    public XamlOutlineNode Node      { get; }
    /// <summary>+1 = move down, -1 = move up.</summary>
    public int             Direction { get; }

    public MoveRequestedEventArgs(XamlOutlineNode node, int direction)
    {
        Node      = node;
        Direction = direction;
    }
}

/// <summary>Carries the node and the container tag name (Grid, StackPanel, Border) for a wrap request.</summary>
public sealed class WrapRequestedEventArgs : EventArgs
{
    public XamlOutlineNode Node          { get; }
    public string           ContainerTag { get; }

    public WrapRequestedEventArgs(XamlOutlineNode node, string containerTag)
    {
        Node         = node;
        ContainerTag = containerTag;
    }
}

/// <summary>Carries the node whose corresponding XAML position should be revealed in the code editor.</summary>
public sealed class NavigateToXamlRequestedEventArgs : EventArgs
{
    public XamlOutlineNode Node { get; }
    public NavigateToXamlRequestedEventArgs(XamlOutlineNode node) => Node = node;
}

// ── Panel ─────────────────────────────────────────────────────────────────────

/// <summary>
/// XAML Outline dockable panel — shows the element hierarchy of the active XAML document.
/// Supports search filtering, inline rename, context menu actions, and a breadcrumb bar.
/// </summary>
public partial class XamlOutlinePanel : UserControl
{
    // ── State ─────────────────────────────────────────────────────────────────

    private XamlOutlinePanelViewModel _vm = new();
    private ToolbarOverflowManager?   _overflowManager;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlOutlinePanel()
    {
        InitializeComponent();
        DataContext = _vm;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Exposes the ViewModel for external wiring by the plugin
    /// (e.g. subscribing to SelectedNodeChanged).
    /// </summary>
    public XamlOutlinePanelViewModel ViewModel => _vm;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Re-subscribe toolbar buttons on every load (safe re-entry pattern).
        BtnExpandAll.Click   -= OnExpandAll;
        BtnCollapseAll.Click -= OnCollapseAll;
        BtnSyncToCode.Click  -= OnSyncToCode;

        BtnExpandAll.Click   += OnExpandAll;
        BtnCollapseAll.Click += OnCollapseAll;
        BtnSyncToCode.Click  += OnSyncToCode;

        OutlineTree.SelectedItemChanged  -= OnTreeSelectedItemChanged;
        OutlineTree.SelectedItemChanged  += OnTreeSelectedItemChanged;
        OutlineTree.MouseDoubleClick     -= OnTreeMouseDoubleClick;
        OutlineTree.MouseDoubleClick     += OnTreeMouseDoubleClick;

        TbxSearch.TextChanged -= OnSearchTextChanged;
        TbxSearch.TextChanged += OnSearchTextChanged;

        // Keep placeholder visibility in sync with content.
        TbxSearch.TextChanged -= OnSearchPlaceholderUpdate;
        TbxSearch.TextChanged += OnSearchPlaceholderUpdate;

        // Wire context menu items for all nodes via the TreeView's ContextMenuOpening.
        OutlineTree.ContextMenuOpening -= OnTreeContextMenuOpening;
        OutlineTree.ContextMenuOpening += OnTreeContextMenuOpening;

        // Wire inline rename commit via KeyDown on the tree.
        OutlineTree.KeyDown -= OnTreeKeyDown;
        OutlineTree.KeyDown += OnTreeKeyDown;

        // Initialize ToolbarOverflowManager after layout is complete.
        _overflowManager ??= new ToolbarOverflowManager(
            ToolbarBorder,
            ToolbarRightPanel,
            ToolbarOverflowButton,
            null,
            new FrameworkElement[] { TbgNavigation },
            leftFixedElements: null);

        Dispatcher.InvokeAsync(
            () => _overflowManager.CaptureNaturalWidths(),
            DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Per MEMORY.md rule: never null _vm on unload — OnLoaded re-subscribes.
        BtnExpandAll.Click   -= OnExpandAll;
        BtnCollapseAll.Click -= OnCollapseAll;
        BtnSyncToCode.Click  -= OnSyncToCode;

        OutlineTree.SelectedItemChanged  -= OnTreeSelectedItemChanged;
        OutlineTree.MouseDoubleClick     -= OnTreeMouseDoubleClick;
        OutlineTree.ContextMenuOpening   -= OnTreeContextMenuOpening;
        OutlineTree.KeyDown              -= OnTreeKeyDown;

        TbxSearch.TextChanged -= OnSearchTextChanged;
        TbxSearch.TextChanged -= OnSearchPlaceholderUpdate;
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    private void OnExpandAll(object sender, RoutedEventArgs e)
        => SetAllExpanded(_vm.RootNodes, true);

    private void OnCollapseAll(object sender, RoutedEventArgs e)
        => SetAllExpanded(_vm.RootNodes, false);

    private void OnSyncToCode(object sender, RoutedEventArgs e)
        => SyncRequested?.Invoke(this, _vm.SelectedNode);

    private void OnRefreshOutline(object sender, RoutedEventArgs e)
        => _vm.RefreshCommand.Execute(null);

    private void OnNavigateToParent(object sender, RoutedEventArgs e)
        => _vm.SelectParentCommand.Execute(null);

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        var path = _vm.SelectedNode?.ElementPath;
        if (!string.IsNullOrEmpty(path))
            Clipboard.SetText(path);
    }

    // ── Search filter ─────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        => _vm.SearchText = TbxSearch.Text;

    private void OnSearchPlaceholderUpdate(object sender, TextChangedEventArgs e)
        => TbxSearchPlaceholder.Visibility =
               string.IsNullOrEmpty(TbxSearch.Text)
                   ? Visibility.Visible
                   : Visibility.Collapsed;

    // ── Tree selection ────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is XamlOutlineNode node)
            _vm.SelectedNode = node;
    }

    // ── Double-click → inline rename ─────────────────────────────────────────

    private void OnTreeMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.SelectedNode is not { } node) return;
        node.BeginRename();
        e.Handled = true;

        // Focus the inline rename TextBox after layout.
        Dispatcher.InvokeAsync(() => FocusInlineRenameBox(node), DispatcherPriority.Input);
    }

    /// <summary>
    /// Finds the inline-rename TextBox for the given node in the visual tree
    /// and moves keyboard focus to it.
    /// </summary>
    private void FocusInlineRenameBox(XamlOutlineNode node)
    {
        // Walk the TreeView's visual tree to find the rendered item container,
        // then find the TextBox named InlineRenameBox inside it.
        if (OutlineTree.ItemContainerGenerator.ContainerFromItem(node) is TreeViewItem tvi)
        {
            var box = FindChild<TextBox>(tvi, "InlineRenameBox");
            if (box is not null)
            {
                box.Focus();
                box.SelectAll();

                // Commit on Enter or lose focus.
                box.KeyDown -= OnInlineRenameKeyDown;
                box.KeyDown += OnInlineRenameKeyDown;
                box.LostFocus -= OnInlineRenameLostFocus;
                box.LostFocus += OnInlineRenameLostFocus;
            }
        }
    }

    private void OnInlineRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;

        if (e.Key == Key.Return)
        {
            CommitRename(box);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (box.Tag is XamlOutlineNode node)
                node.IsEditing = false;
            e.Handled = true;
        }
    }

    private void OnInlineRenameLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            CommitRename(box);
    }

    private void CommitRename(TextBox box)
    {
        if (box.Tag is not XamlOutlineNode node) return;
        var newName = node.CommitRename();
        if (newName is not null)
            RenameCommitted?.Invoke(this, (node, newName));
    }

    // ── Tree key handling ─────────────────────────────────────────────────────

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 && _vm.SelectedNode is { } n)
        {
            n.BeginRename();
            Dispatcher.InvokeAsync(() => FocusInlineRenameBox(n), DispatcherPriority.Input);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _vm.SelectedNode is { } del)
        {
            DeleteRequested?.Invoke(this, new DeleteRequestedEventArgs(del));
            e.Handled = true;
        }
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    /// <summary>
    /// ContextMenuOpening fires on any TreeViewItem — we walk up the visual tree
    /// to find the bound XamlOutlineNode, then wire menu item Click handlers to it.
    /// </summary>
    private void OnTreeContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement fe) return;

        // Walk visual tree to find the node's DataContext.
        var node = ResolveNodeFromVisual(fe);
        if (node is null) return;

        // Select the node so context actions target the right element.
        node.IsSelected = true;

        // The ContextMenu is defined on the Grid inside the DataTemplate.
        // Reach it through the OriginalSource.
        var grid = FindParentWithContextMenu(fe);
        if (grid?.ContextMenu is not ContextMenu menu) return;

        // Wire each MenuItem using its Tag to identify the action.
        foreach (var item in menu.Items)
        {
            if (item is MenuItem mi)
                WireContextMenuItem(mi, node);
        }
    }

    private void WireContextMenuItem(MenuItem mi, XamlOutlineNode node)
    {
        // Remove previous handler before re-wiring (safe re-entry).
        mi.Click -= OnContextMenuItemClick;
        mi.Click += OnContextMenuItemClick;
        mi.DataContext = node;
    }

    private void OnContextMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.DataContext is not XamlOutlineNode node) return;

        var tag = mi.Tag as string;
        switch (tag)
        {
            case "Rename":
                node.BeginRename();
                Dispatcher.InvokeAsync(() => FocusInlineRenameBox(node), DispatcherPriority.Input);
                break;

            case "Delete":
                DeleteRequested?.Invoke(this, new DeleteRequestedEventArgs(node));
                break;

            case "MoveUp":
                MoveRequested?.Invoke(this, new MoveRequestedEventArgs(node, -1));
                break;

            case "MoveDown":
                MoveRequested?.Invoke(this, new MoveRequestedEventArgs(node, +1));
                break;

            case "Grid":
            case "StackPanel":
            case "Border":
                WrapRequested?.Invoke(this, new WrapRequestedEventArgs(node, tag));
                break;

            case "Navigate":
                NavigateToXamlRequested?.Invoke(this, new NavigateToXamlRequestedEventArgs(node));
                break;
        }
    }

    // ── Events exposed to the host ────────────────────────────────────────────

    /// <summary>Raised when the user clicks "Sync to code" — carries the selected node.</summary>
    public event EventHandler<XamlOutlineNode?>? SyncRequested;

    /// <summary>Raised when the user requests deletion of a node (Delete key or context menu).</summary>
    public event EventHandler<DeleteRequestedEventArgs>? DeleteRequested;

    /// <summary>Raised when the user requests move-up (-1) or move-down (+1) of a node.</summary>
    public event EventHandler<MoveRequestedEventArgs>? MoveRequested;

    /// <summary>Raised when the user requests wrapping a node in a container element.</summary>
    public event EventHandler<WrapRequestedEventArgs>? WrapRequested;

    /// <summary>Raised when the user requests navigation to the XAML source of a node.</summary>
    public event EventHandler<NavigateToXamlRequestedEventArgs>? NavigateToXamlRequested;

    /// <summary>
    /// Raised after a successful inline rename commit.
    /// Carries the node and the new name string.
    /// </summary>
    public event EventHandler<(XamlOutlineNode Node, string NewName)>? RenameCommitted;

    // ── Size changes ──────────────────────────────────────────────────────────

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            _overflowManager?.Update();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void SetAllExpanded(
        ObservableCollection<XamlOutlineNode> nodes,
        bool expanded)
    {
        foreach (var n in nodes)
        {
            n.IsExpanded = expanded;
            SetAllExpanded(n.Children, expanded);
        }
    }

    /// <summary>
    /// Walks up the visual tree from <paramref name="element"/> to find the
    /// nearest DataContext that is a <see cref="XamlOutlineNode"/>.
    /// </summary>
    private static XamlOutlineNode? ResolveNodeFromVisual(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is XamlOutlineNode node)
                return node;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// Walks up the visual tree to find the first <see cref="FrameworkElement"/>
    /// that has a non-null ContextMenu set.
    /// </summary>
    private static FrameworkElement? FindParentWithContextMenu(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.ContextMenu is not null)
                return fe;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// Searches the logical/visual subtree of <paramref name="parent"/> for a
    /// child element of type <typeparamref name="T"/> with the given name.
    /// </summary>
    private static T? FindChild<T>(DependencyObject parent, string name)
        where T : FrameworkElement
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name)
                return fe;

            var found = FindChild<T>(child, name);
            if (found is not null)
                return found;
        }
        return null;
    }
}
