// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentStructure
// File: Views/DocumentStructurePanel.xaml.cs
// Created: 2026-04-05
// Description:
//     Code-behind for the Document Structure panel XAML.
//     Handles tree selection, double-click navigation, sort changes, and refresh.
//
// Architecture Notes:
//     Minimal code-behind — delegates to DocumentStructureViewModel.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.DocumentStructure.ViewModels;

namespace WpfHexEditor.Plugins.DocumentStructure.Views;

public partial class DocumentStructurePanel : UserControl
{
    private DocumentStructureViewModel? Vm => DataContext as DocumentStructureViewModel;

    /// <summary>Raised when the user requests to refresh the structure (e.g. via the Refresh button).</summary>
    public event EventHandler? RefreshRequested;

    public DocumentStructurePanel()
    {
        InitializeComponent();
    }

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Single click = highlight only (caret tracking takes care of visual feedback)
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (StructureTree.SelectedItem is StructureNodeVm node)
            Vm?.OnNodeActivated(node);
    }

    private void OnFlatSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Single click = no action in flat mode
    }

    private void OnFlatDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FlatList.SelectedItem is StructureNodeVm node)
            Vm?.OnNodeActivated(node);
    }

    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm is null) return;
        if (sender is ComboBox combo)
        {
            Vm.CurrentSort = combo.SelectedIndex switch
            {
                0 => SortMode.SourceOrder,
                1 => SortMode.Alphabetical,
                2 => SortMode.ByKind,
                _ => SortMode.SourceOrder,
            };
        }
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
        => RefreshRequested?.Invoke(this, EventArgs.Empty);
}
