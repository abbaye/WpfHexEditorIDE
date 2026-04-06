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
using WpfHexEditor.Plugins.DocumentStructure.ViewModels;

namespace WpfHexEditor.Plugins.DocumentStructure.Views;

public partial class DocumentStructurePanel : UserControl
{
    private const double CollapseThreshold = 260.0;

    private DocumentStructureViewModel? Vm => DataContext as DocumentStructureViewModel;

    /// <summary>Raised when the user requests to refresh the structure (e.g. via the Refresh button).</summary>
    public event EventHandler? RefreshRequested;

    public DocumentStructurePanel()
    {
        InitializeComponent();
    }

    // ── Tree ────────────────────────────────────────────────────────────────

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Single click = highlight only (caret tracking handles visual feedback)
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (StructureTree.SelectedItem is StructureNodeVm node)
            Vm?.OnNodeActivated(node);
    }

    // ── Flat list ───────────────────────────────────────────────────────────

    private void OnFlatSelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void OnFlatDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FlatList.SelectedItem is StructureNodeVm node)
            Vm?.OnNodeActivated(node);
    }

    // ── Refresh ─────────────────────────────────────────────────────────────

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
        => RefreshRequested?.Invoke(this, EventArgs.Empty);

    // ── Responsive toolbar ──────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var collapsed = e.NewSize.Width < CollapseThreshold;
        TbgSort.Visibility               = collapsed ? Visibility.Collapsed : Visibility.Visible;
        ToolbarOverflowButton.Visibility = collapsed ? Visibility.Visible   : Visibility.Collapsed;
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
        => OverflowContextMenu.IsOpen = true;

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var idx = Vm.SortModeIndex;
        foreach (MenuItem item in OverflowContextMenu.Items)
        {
            if (item.Tag is string tag && int.TryParse(tag, out var tagIdx))
                item.IsChecked = tagIdx == idx;
        }
    }

    private void OnOverflowSortClicked(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (sender is MenuItem { Tag: string tag } && int.TryParse(tag, out var idx))
            Vm.SortModeIndex = idx;
    }
}
