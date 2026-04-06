// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: PropertyInspectorPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-19 â€” IsGroupedView toggle + ToggleGroupCommand.
//          2026-03-22 â€” Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.ViewModels).
// Description:
//     ViewModel for the XAML Property Inspector dockable panel.
//     Reflects DependencyProperties from the selected element and
//     supports filtering by name, hiding default values, and toggling
//     between grouped (by category) and flat alphabetical views.
//
// Architecture: Plugin-owned panel ViewModel; uses PropertyInspectorService from editor core.
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
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for the Property Inspector panel.
/// </summary>
public sealed class PropertyInspectorPanelViewModel : ViewModelBase
{
    private readonly PropertyInspectorService                    _service    = new();
    private readonly ObservableCollection<PropertyInspectorEntry> _allEntries = new();

    private DependencyObject? _selectedObject;
    private string            _filterText             = string.Empty;
    private bool              _showDefaultValues      = false;
    private bool              _isGroupedView          = true;
    private bool              _showAttachedProperties = true;

    // ── Constructor ───────────────────────────────────────────────────────────

    public PropertyInspectorPanelViewModel()
    {
        PropertiesView        = CollectionViewSource.GetDefaultView(_allEntries);
        PropertiesView.Filter = FilterProperty;

        ApplyGroupedLayout();

        ToggleGroupCommand = new RelayCommand(_ => IsGroupedView = !IsGroupedView);

        ResetAllCommand = new RelayCommand(
            _ =>
            {
                if (PropertiesView is null || XamlPatchCallback is null) return;
                foreach (var entry in PropertiesView.OfType<PropertyInspectorEntry>()
                             .Where(e => e.IsLocalValue).ToList())
                    XamlPatchCallback(entry.PropertyName, null);
            },
            _ => SelectedObject is not null);

        CopyPropertiesCommand = new RelayCommand(
            _ =>
            {
                if (PropertiesView is null) return;
                var sb = new StringBuilder();
                foreach (var entry in PropertiesView.OfType<PropertyInspectorEntry>())
                    sb.AppendLine($"{entry.PropertyName}: {entry.Value?.ToString() ?? string.Empty}");
                if (sb.Length > 0) System.Windows.Clipboard.SetText(sb.ToString());
            },
            _ => SelectedObject is not null);
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public ICollectionView PropertiesView { get; }

    public Action<string, string?>? XamlPatchCallback { get; set; }

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

    public ICommand ToggleGroupCommand    { get; }
    public ICommand ResetAllCommand       { get; }
    public ICommand CopyPropertiesCommand { get; }

    public bool ShowAttachedProperties
    {
        get => _showAttachedProperties;
        set
        {
            if (_showAttachedProperties == value) return;
            _showAttachedProperties = value;
            OnPropertyChanged();
            PropertiesView.Refresh();
        }
    }

    // ── INPC ──────────────────────────────────────────────────────────────────



    // ── Private ───────────────────────────────────────────────────────────────

    private void RefreshProperties()
    {
        _allEntries.Clear();

        if (_selectedObject is null) return;

        var entries = _service.GetProperties(_selectedObject);
        foreach (var entry in entries)
        {
            entry.SetXamlPatchCallback(XamlPatchCallback);
            _allEntries.Add(entry);
        }

        PropertiesView.Refresh();
    }

    private bool FilterProperty(object item)
    {
        if (item is not PropertyInspectorEntry entry) return false;

        if (!_showDefaultValues && entry.IsDefault) return false;

        if (!_showAttachedProperties && entry.PropertyName.Contains('.')) return false;

        if (!string.IsNullOrEmpty(_filterText))
            return entry.PropertyName.Contains(_filterText, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private void ApplyGroupedLayout()
    {
        PropertiesView.GroupDescriptions?.Clear();
        PropertiesView.SortDescriptions.Clear();

        if (PropertiesView is ListCollectionView lcv)
            lcv.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PropertyInspectorEntry.CategoryName)));
        else
            PropertiesView.GroupDescriptions?.Add(
                new PropertyGroupDescription(nameof(PropertyInspectorEntry.CategoryName)));

        PropertiesView.SortDescriptions.Add(
            new SortDescription(nameof(PropertyInspectorEntry.CategoryName), ListSortDirection.Ascending));
        PropertiesView.SortDescriptions.Add(
            new SortDescription(nameof(PropertyInspectorEntry.PropertyName), ListSortDirection.Ascending));

        PropertiesView.Refresh();
    }

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
