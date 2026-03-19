// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlToolboxPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19
// Description:
//     Code-behind for the XAML Toolbox dockable panel.
//     Manages drag-and-drop initiation, search wiring, favorites,
//     recent items, category collapse, double-click insert, and context menus.
//
// Architecture Notes:
//     VS-Like Panel Pattern — 26px toolbar + grouped/filtered content.
//     Drag initiated on PreviewMouseMove with ToolboxDropService.DragDropFormat.
//     Follows OnLoaded/OnUnloaded lifecycle rule: never nulls the ViewModel.
//     InsertRequested: raised on double-click or "Insert at Selection" menu.
//     DropCompleted: raised after a successful drag-and-drop.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// XAML Toolbox dockable panel — lists all available WPF controls for drag-to-canvas.
/// </summary>
public partial class XamlToolboxPanel : UserControl
{
    // ── State ─────────────────────────────────────────────────────────────────

    private XamlToolboxPanelViewModel _vm = new();
    private Point _dragStartPoint;
    private bool  _isDragStarted;
    private bool  _showFavoritesOnly;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlToolboxPanel()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public XamlToolboxPanelViewModel ViewModel => _vm;

    /// <summary>Raised when the user double-clicks or uses "Insert at Selection".</summary>
    public event EventHandler<ToolboxItem>? InsertRequested;

    /// <summary>Raised after a successful drag-and-drop onto the canvas.</summary>
    public event EventHandler<ToolboxItem>? DropCompleted;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ToolboxList.PreviewMouseLeftButtonDown -= OnListMouseDown;
        ToolboxList.PreviewMouseLeftButtonDown += OnListMouseDown;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;
        RefreshFavoritesSection();
        RefreshRecentSection();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // IMPORTANT: do NOT null _vm — panel may reload after dock/float.
        ToolboxList.PreviewMouseLeftButtonDown -= OnListMouseDown;
        _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    // ── VM change listener ────────────────────────────────────────────────────

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(XamlToolboxPanelViewModel.FavoriteKeys))
            RefreshFavoritesSection();
        if (e.PropertyName is nameof(XamlToolboxPanelViewModel.RecentItems)
            || e.PropertyName is null)
            RefreshRecentSection();
    }

    // ── Search TextBox ────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
            _vm.SearchText = tb.Text;
    }

    // ── Favorites toggle ──────────────────────────────────────────────────────

    private void OnFavoritesToggleChanged(object sender, RoutedEventArgs e)
    {
        _showFavoritesOnly = BtnFavoritesToggle.IsChecked == true;
        FavoritesSection.Visibility = _showFavoritesOnly ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Category header collapse ──────────────────────────────────────────────

    private void OnCategoryHeaderClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string categoryName })
            _vm.ToggleCategoryCommand.Execute(categoryName);
    }

    // ── Context menu handlers ─────────────────────────────────────────────────

    private void OnToggleFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (GetMenuItemTag(sender) is ToolboxItem item)
        {
            _vm.ToggleFavorite(item);
            RefreshFavoritesSection();
        }
    }

    private void OnRemoveFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (GetMenuItemTag(sender) is ToolboxItem item && _vm.IsFavorite(item))
        {
            _vm.ToggleFavorite(item);
            RefreshFavoritesSection();
        }
    }

    private void OnInsertAtSelectionClick(object sender, RoutedEventArgs e)
    {
        if (GetMenuItemTag(sender) is ToolboxItem item)
            InsertRequested?.Invoke(this, item);
    }

    private void OnCopyTagClick(object sender, RoutedEventArgs e)
    {
        if (GetMenuItemTag(sender) is ToolboxItem item)
            System.Windows.Clipboard.SetText(item.DefaultXaml);
    }

    private static ToolboxItem? GetMenuItemTag(object sender)
        => sender is MenuItem { Tag: ToolboxItem item } ? item : null;

    // ── Double-click insert ───────────────────────────────────────────────────

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.SelectedItem is { } item)
            InsertRequested?.Invoke(this, item);
    }

    // ── Drag initiation ───────────────────────────────────────────────────────

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragStarted  = false;
    }

    private void OnListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragStarted) return;
        if (!ExceedsMinimumDragDistance(e)) return;
        if (_vm.SelectedItem is not ToolboxItem item) return;

        _isDragStarted = true;
        PerformDragDrop(item);
    }

    private bool ExceedsMinimumDragDistance(MouseEventArgs e)
    {
        var current = e.GetPosition(null);
        return Math.Abs(current.X - _dragStartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(current.Y - _dragStartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private void PerformDragDrop(ToolboxItem item)
    {
        var data   = new DataObject(ToolboxDropService.DragDropFormat, item);
        var result = DragDrop.DoDragDrop(ToolboxList, data, DragDropEffects.Copy);

        if (result == DragDropEffects.Copy)
        {
            _vm.TrackRecentUsage(item);
            RefreshRecentSection();
            DropCompleted?.Invoke(this, item);
        }
    }

    // ── Favorites / Recent section refresh ────────────────────────────────────

    private void RefreshFavoritesSection()
    {
        FavoritesWrap.Children.Clear();

        var favorites = GetAllToolboxItems()
            .Where(i => _vm.IsFavorite(i))
            .ToList();

        foreach (var item in favorites)
            FavoritesWrap.Children.Add(BuildSectionItemButton(item));

        FavoritesSection.Visibility =
            favorites.Count > 0 && _showFavoritesOnly
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshRecentSection()
    {
        RecentWrap.Children.Clear();

        foreach (var item in _vm.RecentItems)
            RecentWrap.Children.Add(BuildSectionItemButton(item));

        RecentSection.Visibility =
            _vm.RecentItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Button BuildSectionItemButton(ToolboxItem item)
    {
        var btn = new Button
        {
            Width   = 60,
            Height  = 56,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor  = Cursors.Hand,
            ToolTip = item.Name,
            Tag     = item
        };

        var panel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text       = item.IconGlyph,
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize   = 20,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text      = item.Name,
            FontSize  = 10,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        btn.Content = panel;
        btn.Click  += (_, _) => InsertRequested?.Invoke(this, item);
        return btn;
    }

    // ── Sort / Expand / Collapse / Clear Recent ───────────────────────────────

    private void OnToolboxSortClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn) btn.ContextMenu!.IsOpen = true;
    }

    private void OnToolboxSortSelected(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag })
            _vm.SortMode = tag;
    }

    private void OnExpandAllCategories(object sender, RoutedEventArgs e)
    {
        foreach (var key in _vm.CategoryExpanded.Keys.ToList())
            _vm.CategoryExpanded[key] = true;
        _vm.ApplyCategorySort();
    }

    private void OnCollapseAllCategories(object sender, RoutedEventArgs e)
    {
        foreach (var key in _vm.CategoryExpanded.Keys.ToList())
            _vm.CategoryExpanded[key] = false;
        _vm.ApplyCategorySort();
    }

    private void OnClearRecent(object sender, RoutedEventArgs e)
    {
        _vm.RecentItems.Clear();
        RefreshRecentSection();
    }

    // ── Private helper: enumerate all toolbox items ───────────────────────────

    private IEnumerable<ToolboxItem> GetAllToolboxItems()
    {
        foreach (var obj in _vm.ItemsView.SourceCollection)
            if (obj is ToolboxItem item) yield return item;
    }
}
