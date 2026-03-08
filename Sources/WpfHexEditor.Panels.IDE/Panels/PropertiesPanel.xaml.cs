// ==========================================================
// Project: WpfHexEditor.Panels.IDE
// File: PropertiesPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Code-behind for the VS-style Properties panel (F4).
//     Binds to an IPropertyProvider and displays categorised,
//     optionally-editable property rows.
//
// Architecture Notes:
//     Uses PropertyEntryDataTemplateSelector to pick the right XAML
//     DataTemplate per entry type. Supports toolbar Sort/Copy/Refresh,
//     right-click context menu copy, FilePath open-in-Explorer, and
//     Color swatch rendering via InverseBoolConverter / StringToColorConverter.
//
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Panels.IDE;

/// <summary>
/// VS-style Properties panel (F4).
/// Binds to an <see cref="IPropertyProvider"/> and displays categorised,
/// optionally-editable property rows.
/// </summary>
public partial class PropertiesPanel : UserControl, IPropertiesPanel
{
    // -- IPropertiesPanel -----------------------------------------------------

    private IPropertyProvider?      _provider;
    private bool                    _isSorted;
    private PropertyEntry?          _selectedEntry;
    private ToolbarOverflowManager  _overflowManager = null!;

    public PropertyEntryDataTemplateSelector EntryTemplateSelector { get; } = new();

    public void SetProvider(IPropertyProvider? provider)
    {
        if (_provider != null)
            _provider.PropertiesChanged -= OnProviderChanged;

        _provider = provider;

        if (_provider != null)
            _provider.PropertiesChanged += OnProviderChanged;

        Refresh();
    }

    // -- Constructor ----------------------------------------------------------

    public PropertiesPanel()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            _overflowManager = new ToolbarOverflowManager(
                toolbarContainer:      ToolbarBorder,
                alwaysVisiblePanel:    ToolbarRightPanel,
                overflowButton:        ToolbarOverflowButton,
                overflowMenu:          OverflowContextMenu,
                groupsInCollapseOrder: new FrameworkElement[] { TbgPropActions });
            Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
        };
    }

    // -- Refresh --------------------------------------------------------------

    private void OnProviderChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        ContextLabelText.Text = _provider?.ContextLabel ?? "Properties";
        DescNameText.Text   = "";
        DescDetailText.Text = "";

        var groups = _provider?.GetProperties() ?? [];

        if (_isSorted)
        {
            groups = groups
                .Select(g => new PropertyGroup { Name = g.Name, Entries = [.. g.Entries.OrderBy(e => e.Name)] })
                .ToList();
        }

        GroupsControl.ItemsSource = new ObservableCollection<PropertyGroup>(groups);
    }

    // -- Toolbar handlers -----------------------------------------------------

    private void OnRefreshButtonClick(object sender, RoutedEventArgs e) => Refresh();

    private void OnCopyButtonClick(object sender, RoutedEventArgs e)
        => TrySetClipboard(_selectedEntry?.Value?.ToString() ?? "");

    private void OnSortChanged(object sender, RoutedEventArgs e)
    {
        _isSorted = SortToggle.IsChecked ?? false;
        Refresh();
    }

    // -- Entry events ---------------------------------------------------------

    private void OnEntryRowClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PropertyEntry entry)
        {
            _selectedEntry = entry;
            ShowDescription(entry);
        }
    }

    private void ShowDescription(PropertyEntry entry)
    {
        DescNameText.Text   = entry.Name;
        DescDetailText.Text = entry.Description ?? "";
    }

    private void OnTextBoxCommit(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is PropertyEntry entry)
            entry.OnValueChanged?.Invoke(tb.Text);
    }

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb && tb.DataContext is PropertyEntry entry)
        {
            entry.OnValueChanged?.Invoke(tb.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && sender is TextBox)
        {
            Refresh();
            e.Handled = true;
        }
    }

    private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is PropertyEntry entry)
            entry.OnValueChanged?.Invoke(cb.IsChecked ?? false);
    }

    private void OnComboBoxChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.DataContext is PropertyEntry entry &&
            cb.SelectedItem != null)
            entry.OnValueChanged?.Invoke(cb.SelectedItem);
    }

    // -- Context menu handlers ------------------------------------------------

    private void OnCopyEntryValue(object sender, RoutedEventArgs e)
    {
        var entry = GetEntryFromMenuItem(sender);
        TrySetClipboard(entry?.Value?.ToString() ?? "");
    }

    private void OnCopyEntryName(object sender, RoutedEventArgs e)
    {
        var entry = GetEntryFromMenuItem(sender);
        TrySetClipboard(entry?.Name ?? "");
    }

    private static PropertyEntry? GetEntryFromMenuItem(object sender)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PropertyEntry e) return e;
        // MenuItem placed inside a ContextMenu — walk up PlacementTarget
        if (sender is MenuItem mi && mi.CommandParameter is PropertyEntry pe) return pe;
        return null;
    }

    // -- FilePath handler -----------------------------------------------------

    private void OnOpenFilePathClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PropertyEntry entry &&
            entry.Value is string path && File.Exists(path))
        {
            try { Process.Start("explorer.exe", $"/select,\"{path}\""); }
            catch { /* silently ignore launch errors */ }
        }
    }

    // -- Helpers --------------------------------------------------------------

    private static void TrySetClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch { /* silently ignored — clipboard may be locked by another process */ }
    }

    // ── Toolbar overflow ─────────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowContextMenu.PlacementTarget = ToolbarOverflowButton;
        OverflowContextMenu.Placement       = PlacementMode.Bottom;
        OverflowContextMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        OvfSort.IsChecked = SortToggle.IsChecked == true;
        _overflowManager?.SyncMenuVisibility();
    }

    private void OvfSort_Click(object sender, RoutedEventArgs e)
    {
        SortToggle.IsChecked = !(SortToggle.IsChecked == true);
        OnSortChanged(SortToggle, new RoutedEventArgs());
    }
}

// -- DataTemplateSelector -----------------------------------------------------

/// <summary>
/// Selects the DataTemplate for a <see cref="PropertyEntry"/> based on its
/// <see cref="PropertyEntry.IsReadOnly"/> flag and <see cref="PropertyEntry.Type"/>.
/// </summary>
public sealed class PropertyEntryDataTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not PropertyEntry entry) return null;

        var uc = FindAncestor<UserControl>(container);
        if (uc?.Resources == null) return null;

        if (entry.IsReadOnly)
            return uc.FindResource("ReadOnlyTextTemplate") as DataTemplate;

        return entry.Type switch
        {
            PropertyEntryType.Boolean  => uc.FindResource("BooleanTemplate")      as DataTemplate,
            PropertyEntryType.Enum     => uc.FindResource("EnumTemplate")          as DataTemplate,
            PropertyEntryType.Integer  => uc.FindResource("EditIntegerTemplate")   as DataTemplate,
            PropertyEntryType.Hex      => uc.FindResource("EditIntegerTemplate")   as DataTemplate,
            PropertyEntryType.FilePath => uc.FindResource("FilePathTemplate")      as DataTemplate,
            PropertyEntryType.Color    => uc.FindResource("ColorTemplate")         as DataTemplate,
            _                          => uc.FindResource("EditTextTemplate")      as DataTemplate,
        };
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}

// -- Value converters ---------------------------------------------------------

/// <summary>Inverts a bool — used so read-only entries disable their editing control.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : DependencyProperty.UnsetValue;
}

/// <summary>
/// Converts a "#RRGGBB" or "#AARRGGBB" string to a WPF <see cref="Color"/>.
/// Returns <see cref="Colors.Transparent"/> for invalid input.
/// </summary>
public sealed class StringToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string s && s.Length > 0)
                return (Color)ColorConverter.ConvertFromString(s);
        }
        catch { /* fall through */ }
        return Colors.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Color c ? c.ToString() : "";
}
