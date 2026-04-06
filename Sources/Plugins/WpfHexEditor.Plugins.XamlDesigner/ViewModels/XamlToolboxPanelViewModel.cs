// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: XamlToolboxPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19
//          2026-03-22 â€” Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.ViewModels).
// Description:
//     ViewModel for the XAML Toolbox dockable panel.
//     Exposes filtered and grouped toolbox items for display in a ListBox.
//     Supports live text search filtering, favorites, recent items (max 8),
//     and collapsible category state.
//
// Architecture: Plugin-owned panel ViewModel; uses ToolboxRegistry + ToolboxItem from editor core.
// ==========================================================

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the XAML Toolbox panel.
/// </summary>
public sealed class XamlToolboxPanelViewModel : ViewModelBase
{
    private const int MaxRecentItems = 8;

    private readonly ObservableCollection<ToolboxItem> _items;
    private string _filterText   = string.Empty;
    private ToolboxItem? _selectedItem;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlToolboxPanelViewModel()
    {
        _items = new ObservableCollection<ToolboxItem>(ToolboxRegistry.Instance.Items);

        ItemsView = CollectionViewSource.GetDefaultView(_items);
        ItemsView.Filter = FilterItem;

        if (ItemsView.GroupDescriptions != null)
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ToolboxItem.Category)));

        ToggleCategoryCommand = new RelayCommand(p => ExecuteToggleCategory(p as string));
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public ICollectionView ItemsView { get; }

    public string SearchText
    {
        get => _filterText;
        set
        {
            if (_filterText == value) return;
            _filterText = value;
            OnPropertyChanged();
            UpdateIsMatchOnAllItems(value);
            ItemsView.Refresh();
        }
    }

    public string FilterText
    {
        get => SearchText;
        set => SearchText = value;
    }

    public ToolboxItem? SelectedItem
    {
        get => _selectedItem;
        set { if (_selectedItem == value) return; _selectedItem = value; OnPropertyChanged(); }
    }

    public HashSet<string> FavoriteKeys { get; } = new(StringComparer.Ordinal);
    public ObservableCollection<ToolboxItem> RecentItems { get; } = new();
    public Dictionary<string, bool> CategoryExpanded { get; } = new(StringComparer.Ordinal);

    private string _sortMode = "ByCategory";

    public string SortMode
    {
        get => _sortMode;
        set
        {
            if (_sortMode == value) return;
            _sortMode = value;
            OnPropertyChanged();
            ApplyCategorySort();
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand ToggleCategoryCommand { get; }

    // ── Public API ────────────────────────────────────────────────────────────

    public bool IsFavorite(ToolboxItem item) => FavoriteKeys.Contains(item.Key);

    public void ToggleFavorite(ToolboxItem item)
    {
        if (!FavoriteKeys.Remove(item.Key))
            FavoriteKeys.Add(item.Key);

        OnPropertyChanged(nameof(FavoriteKeys));
        ItemsView.Refresh();
    }

    public void TrackRecentUsage(ToolboxItem item)
    {
        RemoveExistingRecentEntry(item);
        RecentItems.Insert(0, item);
        TrimRecentItemsToMax();
    }

    // ── Sort ──────────────────────────────────────────────────────────────────

    public void ApplyCategorySort()
    {
        ItemsView.SortDescriptions.Clear();
        switch (_sortMode)
        {
            case "ByNameAZ":
                ItemsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                    nameof(ToolboxItem.Name), System.ComponentModel.ListSortDirection.Ascending));
                break;
            case "ByRecent":
                ItemsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                    nameof(ToolboxItem.Category), System.ComponentModel.ListSortDirection.Ascending));
                ItemsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                    nameof(ToolboxItem.Name), System.ComponentModel.ListSortDirection.Ascending));
                break;
            default: // ByCategory
                ItemsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                    nameof(ToolboxItem.Category), System.ComponentModel.ListSortDirection.Ascending));
                ItemsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                    nameof(ToolboxItem.Name), System.ComponentModel.ListSortDirection.Ascending));
                break;
        }
        ItemsView.Refresh();
        OnPropertyChanged(nameof(CategoryExpanded));
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void UpdateIsMatchOnAllItems(string text)
    {
        _ = text; // filter handled by ItemsView.Refresh()
    }

    private void RemoveExistingRecentEntry(ToolboxItem item)
    {
        for (int i = RecentItems.Count - 1; i >= 0; i--)
        {
            if (RecentItems[i].Key == item.Key)
                RecentItems.RemoveAt(i);
        }
    }

    private void TrimRecentItemsToMax()
    {
        while (RecentItems.Count > MaxRecentItems)
            RecentItems.RemoveAt(RecentItems.Count - 1);
    }

    private void ExecuteToggleCategory(string? categoryName)
    {
        if (categoryName is null) return;

        bool current = CategoryExpanded.TryGetValue(categoryName, out bool val) ? val : true;
        CategoryExpanded[categoryName] = !current;
        OnPropertyChanged(nameof(CategoryExpanded));
    }

    private bool FilterItem(object obj)
    {
        if (obj is not ToolboxItem item) return false;
        if (string.IsNullOrWhiteSpace(_filterText)) return true;

        return item.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
            || item.Category.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
    }

    // ── INPC ──────────────────────────────────────────────────────────────────


}
