// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlOutlinePanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Code-behind for the XAML Outline dockable panel.
//     Wires toolbar buttons and ToolbarOverflowManager.
//     Follows the OnLoaded/OnUnloaded lifecycle rule: never null _vm.
//
// Architecture Notes:
//     VS-Like Panel Pattern — 26px toolbar + tree content area.
//     ToolbarOverflowManager manages TbgNavigation collapse on narrow widths.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// XAML Outline dockable panel — shows the element hierarchy of the active XAML document.
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
        // Re-subscribe toolbar buttons on every load (safe re-entry).
        BtnExpandAll.Click   -= OnExpandAll;
        BtnCollapseAll.Click -= OnCollapseAll;
        BtnSyncToCode.Click  -= OnSyncToCode;

        BtnExpandAll.Click   += OnExpandAll;
        BtnCollapseAll.Click += OnCollapseAll;
        BtnSyncToCode.Click  += OnSyncToCode;

        OutlineTree.SelectedItemChanged -= OnTreeSelectedItemChanged;
        OutlineTree.SelectedItemChanged += OnTreeSelectedItemChanged;

        // Initialize ToolbarOverflowManager after layout is complete.
        _overflowManager ??= new ToolbarOverflowManager(
            ToolbarBorder,
            ToolbarRightPanel,
            ToolbarOverflowButton,
            null,                      // no overflow ContextMenu for this panel
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
        OutlineTree.SelectedItemChanged -= OnTreeSelectedItemChanged;
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    private void OnExpandAll(object sender, RoutedEventArgs e)
        => SetAllExpanded(_vm.RootNodes, true);

    private void OnCollapseAll(object sender, RoutedEventArgs e)
        => SetAllExpanded(_vm.RootNodes, false);

    private void OnSyncToCode(object sender, RoutedEventArgs e)
    {
        // Sync request: the plugin handles the actual scroll-to-line logic.
        SyncRequested?.Invoke(this, _vm.SelectedNode);
    }

    // ── Tree selection ────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is XamlOutlineNode node)
            _vm.SelectedNode = node;
    }

    // ── Events exposed to the host ────────────────────────────────────────────

    /// <summary>Raised when the user clicks "Sync to code" — carries the selected node.</summary>
    public event EventHandler<XamlOutlineNode?>? SyncRequested;

    // ── Size changes ──────────────────────────────────────────────────────────

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            _overflowManager?.Update();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void SetAllExpanded(
        System.Collections.ObjectModel.ObservableCollection<XamlOutlineNode> nodes,
        bool expanded)
    {
        foreach (var n in nodes)
        {
            n.IsExpanded = expanded;
            SetAllExpanded(n.Children, expanded);
        }
    }
}
