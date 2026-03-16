// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: PropertyInspectorPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     ViewModel for the XAML Property Inspector dockable panel.
//     Reflects DependencyProperties from the selected element and
//     supports filtering by name and hiding default values.
//
// Architecture Notes:
//     INPC. Uses ICollectionView for filtering + grouping.
//     PropertyInspectorService handles reflection.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the Property Inspector panel.
/// </summary>
public sealed class PropertyInspectorPanelViewModel : INotifyPropertyChanged
{
    private readonly PropertyInspectorService _service = new();
    private readonly ObservableCollection<PropertyInspectorEntry> _allEntries = new();

    private DependencyObject? _selectedObject;
    private string            _filterText        = string.Empty;
    private bool              _showDefaultValues = false;

    // ── Constructor ───────────────────────────────────────────────────────────

    public PropertyInspectorPanelViewModel()
    {
        PropertiesView = CollectionViewSource.GetDefaultView(_allEntries);
        PropertiesView.Filter = FilterProperty;

        if (PropertiesView is ListCollectionView lcv)
            lcv.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PropertyInspectorEntry.CategoryName)));
        else
            PropertiesView.GroupDescriptions?.Add(
                new PropertyGroupDescription(nameof(PropertyInspectorEntry.CategoryName)));
    }

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Filtered and grouped view of all property entries.</summary>
    public ICollectionView PropertiesView { get; }

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
            _allEntries.Add(entry);

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
}
