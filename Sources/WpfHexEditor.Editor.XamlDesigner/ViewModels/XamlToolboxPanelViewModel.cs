// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlToolboxPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ViewModel for the XAML Toolbox dockable panel.
//     Exposes filtered and grouped toolbox items for display in a ListBox.
//     Supports live text search filtering across category and item name.
//
// Architecture Notes:
//     INPC. ICollectionView for grouping by Category.
//     ToolboxRegistry.Instance provides the master item list.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the XAML Toolbox panel.
/// </summary>
public sealed class XamlToolboxPanelViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<ToolboxItem> _items;
    private string _filterText = string.Empty;
    private ToolboxItem? _selectedItem;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlToolboxPanelViewModel()
    {
        _items = new ObservableCollection<ToolboxItem>(ToolboxRegistry.Instance.Items);

        ItemsView = CollectionViewSource.GetDefaultView(_items);
        ItemsView.Filter = FilterItem;

        if (ItemsView.GroupDescriptions != null)
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ToolboxItem.Category)));
    }

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Filtered and grouped view of all toolbox items.</summary>
    public ICollectionView ItemsView { get; }

    /// <summary>Text filter applied to category and item name.</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value) return;
            _filterText = value;
            OnPropertyChanged();
            ItemsView.Refresh();
        }
    }

    /// <summary>Currently selected toolbox item (used to initiate drag).</summary>
    public ToolboxItem? SelectedItem
    {
        get => _selectedItem;
        set { if (_selectedItem == value) return; _selectedItem = value; OnPropertyChanged(); }
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private ───────────────────────────────────────────────────────────────

    private bool FilterItem(object obj)
    {
        if (obj is not ToolboxItem item) return false;
        if (string.IsNullOrWhiteSpace(_filterText)) return true;

        return item.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
            || item.Category.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
    }
}
