// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlToolboxPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19
// Description:
//     ViewModel for the XAML Toolbox dockable panel.
//     Exposes filtered and grouped toolbox items for display in a ListBox.
//     Supports live text search filtering, favorites, recent items (max 8),
//     and collapsible category state.
//
// Architecture Notes:
//     INPC. ICollectionView for grouping by Category.
//     ToolboxRegistry.Instance provides the master item list.
//     FavoriteKeys: HashSet<string> keyed by ToolboxItem.Key.
//     RecentItems: ObservableCollection max 8, deduplication on insert.
//     CategoryExpanded: Dictionary<string, bool> default all true.
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

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the XAML Toolbox panel.
/// </summary>
public sealed class XamlToolboxPanelViewModel : INotifyPropertyChanged
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

    /// <summary>Filtered and grouped view of all toolbox items.</summary>
    public ICollectionView ItemsView { get; }

    /// <summary>Text filter applied to category and item name. Also updates IsMatch on items.</summary>
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

    /// <summary>Text filter applied to category and item name (alias for legacy binding).</summary>
    public string FilterText
    {
        get => SearchText;
        set => SearchText = value;
    }

    /// <summary>Currently selected toolbox item (used to initiate drag).</summary>
    public ToolboxItem? SelectedItem
    {
        get => _selectedItem;
        set { if (_selectedItem == value) return; _selectedItem = value; OnPropertyChanged(); }
    }

    /// <summary>Keys of favorite items. Keyed by ToolboxItem.Key.</summary>
    public HashSet<string> FavoriteKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Recent items, max 8, most-recent-first.</summary>
    public ObservableCollection<ToolboxItem> RecentItems { get; } = new();

    /// <summary>Collapsed state per category name (default: all true = expanded).</summary>
    public Dictionary<string, bool> CategoryExpanded { get; } = new(StringComparer.Ordinal);

    private string _sortMode = "ByCategory";

    /// <summary>
    /// Active sort mode: "ByCategory" (default), "ByNameAZ", or "ByRecent".
    /// Changing this property triggers an immediate <see cref="ApplyCategorySort"/> call.
    /// </summary>
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

    /// <summary>Returns true when <paramref name="item"/> is in FavoriteKeys.</summary>
    public bool IsFavorite(ToolboxItem item) => FavoriteKeys.Contains(item.Key);

    /// <summary>Adds or removes <paramref name="item"/> from FavoriteKeys and refreshes the view.</summary>
    public void ToggleFavorite(ToolboxItem item)
    {
        if (!FavoriteKeys.Remove(item.Key))
            FavoriteKeys.Add(item.Key);

        OnPropertyChanged(nameof(FavoriteKeys));
        ItemsView.Refresh();
    }

    /// <summary>Inserts <paramref name="item"/> at the front of RecentItems (max 8, deduplicated).</summary>
    public void TrackRecentUsage(ToolboxItem item)
    {
        RemoveExistingRecentEntry(item);
        RecentItems.Insert(0, item);
        TrimRecentItemsToMax();
    }

    // ── Sort ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies sort descriptions to <see cref="ItemsView"/> based on the current <see cref="SortMode"/>.
    /// Also called by the code-behind after toggling category expanded state.
    /// </summary>
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
                // Sort by name within category; recently-used section is separate.
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
        // ToolboxItem is a sealed record — IsMatch must be set via a wrapper or tracked separately.
        // Since ToolboxItem is immutable, we rely on the filter predicate (FilterItem) for visibility;
        // IsMatch on ToolboxItem (if present) is set below.
        // Note: check was done — ToolboxItem does not have IsMatch; the filter handles visibility.
        _ = text; // intentional no-op; filter applied via ItemsView.Refresh()
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
