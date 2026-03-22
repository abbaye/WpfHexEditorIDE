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
    private string                  _filterText  = string.Empty;
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

        // P4: apply text filter — keep only groups that have at least one matching entry.
        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            groups = groups
                .Select(g => new PropertyGroup
                {
                    Name    = g.Name,
                    Entries = g.Entries
                        .Where(e => e.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                        .ToList()
                })
                .Where(g => g.Entries.Count > 0)
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

    // -- Reset-to-default handler (P7) ----------------------------------------

    private void OnResetPropertyClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PropertyEntry entry)
            entry.OnResetToDefault?.Invoke();
    }

    // -- Search bar handlers (P4) ---------------------------------------------

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _filterText = SearchBox.Text;
        SearchClearButton.Visibility = _filterText.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        Refresh();
    }

    private void OnSearchClearClick(object sender, RoutedEventArgs e)
        => SearchBox.Clear(); // triggers OnSearchTextChanged automatically

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) OnSearchClearClick(sender, e);
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
        if (sender is not TextBox tb || tb.DataContext is not PropertyEntry entry) return;

        // P6: run inline validator before committing.
        var error = entry.Validator?.Invoke(tb.Text);
        if (error is not null)
        {
            ShowValidationError(tb, error);
            return;
        }

        ClearValidationError(tb);
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

    // -- Thickness editor handlers (P5) ----------------------------------------

    private void OnThicknessPartCommit(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not PropertyEntry entry) return;

        // Find all 4 TextBox siblings inside the parent Grid of the thickness template.
        var grid = FindAncestorInTemplate<Grid>(tb);
        if (grid is null) return;

        var boxes  = grid.Children.OfType<TextBox>().ToList();
        string GetPart(string tag) =>
            boxes.FirstOrDefault(b => b.Tag as string == tag)?.Text ?? "0";

        var value = $"{GetPart("Left")},{GetPart("Top")},{GetPart("Right")},{GetPart("Bottom")}";

        // P6: validate before committing.
        var error = entry.Validator?.Invoke(value);
        if (error is not null)
        {
            ShowValidationError(tb, error);
            return;
        }

        ClearValidationError(tb);
        entry.OnValueChanged?.Invoke(value);
    }

    private void OnThicknessKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)       { OnThicknessPartCommit(sender, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { Refresh();                        e.Handled = true; }
    }

    // -- Brush swatch handler (P5) --------------------------------------------

    private void OnBrushSwatchClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border swatch || swatch.DataContext is not PropertyEntry entry) return;

        // Open a lightweight popup containing a color spectrum or hex edit.
        // Falls back to a simple color TextBox dialog until a full picker is integrated.
        ShowColorPickerPopup(swatch, entry);
    }

    private static void ShowColorPickerPopup(Border anchor, PropertyEntry entry)
    {
        var hexBox = new TextBox
        {
            Text             = entry.Value?.ToString() ?? "#FF000000",
            Width            = 120,
            Margin           = new Thickness(4),
            BorderThickness  = new Thickness(1),
        };
        hexBox.SetResourceReference(TextBox.BorderBrushProperty, "PP_BorderBrush");
        hexBox.SetResourceReference(TextBox.ForegroundProperty,  "PP_EditableForegroundBrush");
        hexBox.SetResourceReference(TextBox.BackgroundProperty,  "PP_BackgroundBrush");

        var popup = new Popup
        {
            StaysOpen          = false,
            AllowsTransparency = true,
            PlacementTarget    = anchor,
            Placement          = PlacementMode.Bottom,
            Child              = new Border
            {
                Padding         = new Thickness(2),
                BorderThickness = new Thickness(1),
                Child           = hexBox
            }
        };
        var popupBorder = (Border)popup.Child;
        popupBorder.SetResourceReference(Border.BorderBrushProperty,  "PP_BorderBrush");
        popupBorder.SetResourceReference(Border.BackgroundProperty,    "PP_BackgroundBrush");

        hexBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                entry.OnValueChanged?.Invoke(hexBox.Text);
                popup.IsOpen = false;
                ke.Handled   = true;
            }
            else if (ke.Key == Key.Escape)
            {
                popup.IsOpen = false;
                ke.Handled   = true;
            }
        };

        popup.IsOpen = true;
        hexBox.SelectAll();
        hexBox.Focus();
    }

    // -- Helpers --------------------------------------------------------------

    private static void TrySetClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch { /* silently ignored — clipboard may be locked by another process */ }
    }

    // -- Validation helpers (P6) ----------------------------------------------

    private static void ShowValidationError(TextBox tb, string message)
    {
        tb.SetResourceReference(TextBox.BorderBrushProperty, "PP_ValidationErrorBrush");
        tb.BorderThickness = new Thickness(0, 0, 0, 2);
        tb.ToolTip         = message;
    }

    private static void ClearValidationError(TextBox tb)
    {
        tb.SetResourceReference(TextBox.BorderBrushProperty, "PP_BorderBrush");
        tb.BorderThickness = new Thickness(0, 0, 0, 1);
        tb.ToolTip         = null;
    }

    /// <summary>
    /// Walks the visual tree starting at <paramref name="d"/> looking for
    /// the first ancestor of type <typeparamref name="T"/> that is within
    /// the same DataTemplate (stops at a UserControl boundary).
    /// </summary>
    private static T? FindAncestorInTemplate<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            d = VisualTreeHelper.GetParent(d);
            if (d is T t)   return t;
            if (d is UserControl) break; // don't escape the template boundary
        }
        return null;
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
            PropertyEntryType.Boolean   => uc.FindResource("BooleanTemplate")      as DataTemplate,
            PropertyEntryType.Enum      => uc.FindResource("EnumTemplate")          as DataTemplate,
            PropertyEntryType.Integer   => uc.FindResource("EditIntegerTemplate")   as DataTemplate,
            PropertyEntryType.Hex       => uc.FindResource("EditIntegerTemplate")   as DataTemplate,
            PropertyEntryType.FilePath  => uc.FindResource("FilePathTemplate")      as DataTemplate,
            PropertyEntryType.Color     => uc.FindResource("ColorTemplate")         as DataTemplate,
            PropertyEntryType.Thickness => uc.FindResource("ThicknessTemplate")     as DataTemplate, // P5
            PropertyEntryType.Brush     => uc.FindResource("BrushTemplate")         as DataTemplate, // P5
            _                           => uc.FindResource("EditTextTemplate")      as DataTemplate,
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

/// <summary>
/// Converts a nullable <see cref="Action"/> to <see cref="Visibility"/>:
/// <c>null</c> → <see cref="Visibility.Collapsed"/>, non-null → <see cref="Visibility.Visible"/>.
/// Used to hide the reset button when <see cref="PropertyEntry.OnResetToDefault"/> is null (P7).
/// </summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Converts <see cref="PropertyEntry.IsDefault"/> to a <see cref="FontWeight"/>:
/// <c>true</c> (default value) → <see cref="FontWeights.Normal"/>,
/// <c>false</c> (user-set value) → <see cref="FontWeights.SemiBold"/>.
/// Helps users spot modified properties at a glance (P7).
/// </summary>
public sealed class DefaultToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool isDefault && !isDefault
               ? FontWeights.SemiBold
               : FontWeights.Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Extracts a single component (Left / Top / Right / Bottom) from a
/// comma-separated Thickness string (e.g. "4,4,4,4" or "8,0,8,0").
/// Used by the ThicknessTemplate to populate each of the four TextBox fields.
/// </summary>
public sealed class ThicknessPartConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string raw || parameter is not string part) return "0";

        var parts = raw.Split(',');
        return part switch
        {
            "Left"   => parts.Length > 0 ? parts[0].Trim() : "0",
            "Top"    => parts.Length > 1 ? parts[1].Trim() : "0",
            "Right"  => parts.Length > 2 ? parts[2].Trim() : "0",
            "Bottom" => parts.Length > 3 ? parts[3].Trim() : "0",
            _        => "0"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
