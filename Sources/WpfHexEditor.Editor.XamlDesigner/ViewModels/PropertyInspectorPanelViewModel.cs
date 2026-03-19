// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: PropertyInspectorPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-19 — Added IsGroupedView toggle + ToggleGroupCommand.
//                        When grouped: CategoryName GroupDescription active (existing).
//                        When flat:    GroupDescriptions cleared, sorted alphabetically.
// Description:
//     ViewModel for the XAML Property Inspector dockable panel.
//     Reflects DependencyProperties from the selected element and
//     supports filtering by name, hiding default values, and toggling
//     between grouped (by category) and flat alphabetical views.
//
// Architecture Notes:
//     INPC. Uses ICollectionView for filtering + grouping.
//     PropertyInspectorService handles reflection.
//     Phase D — XamlPatchCallback: external code (XamlDesignerSplitHost) sets
//     this property so every PropertyInspectorEntry edit is routed to the XAML
//     source text via DesignToXamlSyncService.
//     ToggleGroupCommand: Strategy pattern — switches view mode without
//     rebuilding the underlying collection.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the Property Inspector panel.
/// </summary>
public sealed class PropertyInspectorPanelViewModel : INotifyPropertyChanged
{
    private readonly PropertyInspectorService                    _service    = new();
    private readonly ObservableCollection<PropertyInspectorEntry> _allEntries = new();

    private DependencyObject? _selectedObject;
    private string            _filterText        = string.Empty;
    private bool              _showDefaultValues = false;
    private bool              _isGroupedView     = true;

    // ── Constructor ───────────────────────────────────────────────────────────

    public PropertyInspectorPanelViewModel()
    {
        PropertiesView        = CollectionViewSource.GetDefaultView(_allEntries);
        PropertiesView.Filter = FilterProperty;

        // Apply initial grouped layout.
        ApplyGroupedLayout();

        ToggleGroupCommand = new RelayCommand(_ => IsGroupedView = !IsGroupedView);
    }

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Filtered and optionally grouped view of all property entries.</summary>
    public ICollectionView PropertiesView { get; }

    /// <summary>
    /// Called when the user edits a property value in the inspector.
    /// Propagates the change to the XAML source text.
    /// Set by XamlDesignerSplitHost after panel wiring.
    /// Signature: (propertyName, newStringValue).
    /// </summary>
    public Action<string, string?>? XamlPatchCallback { get; set; }

    /// <summary>
    /// The DependencyObject whose properties are displayed.
    /// Set by the plugin when the design canvas selection changes.
    /// </summary>
    public DependencyObject? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (ReferenceEquals(_selectedObject, value)) return;
            _selectedObject = value;
            OnPropertyChanged();
            RefreshProperties();
        }
    }

    /// <summary>Property name filter text (case-insensitive substring match).</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value) return;
            _filterText = value;
            OnPropertyChanged();
            PropertiesView.Refresh();
        }
    }

    /// <summary>When false (default), properties with their default values are hidden.</summary>
    public bool ShowDefaultValues
    {
        get => _showDefaultValues;
        set
        {
            if (_showDefaultValues == value) return;
            _showDefaultValues = value;
            OnPropertyChanged();
            PropertiesView.Refresh();
        }
    }

    /// <summary>
    /// When true: entries are grouped by CategoryName (VS-Like inspector grouping).
    /// When false: GroupDescriptions are cleared and entries are sorted alphabetically.
    /// Toggled via <see cref="ToggleGroupCommand"/>.
    /// </summary>
    public bool IsGroupedView
    {
        get => _isGroupedView;
        set
        {
            if (_isGroupedView == value) return;
            _isGroupedView = value;
            OnPropertyChanged();
            ApplyViewLayout();
        }
    }

    /// <summary>Toggles between grouped-by-category and flat-alphabetical views.</summary>
    public ICommand ToggleGroupCommand { get; }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private ───────────────────────────────────────────────────────────────

    private void RefreshProperties()
    {
        _allEntries.Clear();

        if (_selectedObject is null) return;

        var entries = _service.GetProperties(_selectedObject);
        foreach (var entry in entries)
        {
            // Phase D: attach the XAML patch callback so every Value edit
            // propagates to the XAML source text via XamlDesignerSplitHost.
            entry.SetXamlPatchCallback(XamlPatchCallback);
            _allEntries.Add(entry);
        }

        PropertiesView.Refresh();
    }

    private bool FilterProperty(object item)
    {
        if (item is not PropertyInspectorEntry entry) return false;

        // Hide defaults when ShowDefaultValues is false.
        if (!_showDefaultValues && entry.IsDefault) return false;

        // Apply text filter.
        if (!string.IsNullOrEmpty(_filterText))
            return entry.PropertyName.Contains(_filterText, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    /// <summary>Switches to grouped-by-category layout.</summary>
    private void ApplyGroupedLayout()
    {
        PropertiesView.GroupDescriptions?.Clear();
        PropertiesView.SortDescriptions.Clear();

        if (PropertiesView is ListCollectionView lcv)
            lcv.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PropertyInspectorEntry.CategoryName)));
        else
            PropertiesView.GroupDescriptions?.Add(
                new PropertyGroupDescription(nameof(PropertyInspectorEntry.CategoryName)));

        // Within each group: sort by PropertyName.
        PropertiesView.SortDescriptions.Add(
            new SortDescription(nameof(PropertyInspectorEntry.CategoryName), ListSortDirection.Ascending));
        PropertiesView.SortDescriptions.Add(
            new SortDescription(nameof(PropertyInspectorEntry.PropertyName), ListSortDirection.Ascending));

        PropertiesView.Refresh();
    }

    /// <summary>Switches to flat alphabetical layout (no group headers).</summary>
    private void ApplyFlatLayout()
    {
        PropertiesView.GroupDescriptions?.Clear();
        PropertiesView.SortDescriptions.Clear();

        PropertiesView.SortDescriptions.Add(
            new SortDescription(nameof(PropertyInspectorEntry.PropertyName), ListSortDirection.Ascending));

        PropertiesView.Refresh();
    }

    private void ApplyViewLayout()
    {
        if (_isGroupedView)
            ApplyGroupedLayout();
        else
            ApplyFlatLayout();
    }
}

