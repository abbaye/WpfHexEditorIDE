//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Panels.IDE;

/// <summary>
/// VS-style Properties panel (F4).
/// Binds to an <see cref="IPropertyProvider"/> and displays categorised,
/// optionally-editable property rows.
/// </summary>
public partial class PropertiesPanel : UserControl, IPropertiesPanel
{
    // -- IPropertiesPanel -----------------------------------------------------

    private IPropertyProvider? _provider;

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
    }

    // -- Refresh --------------------------------------------------------------

    private void OnProviderChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        ContextLabelText.Text = _provider?.ContextLabel ?? "Properties";
        DescNameText.Text   = "";
        DescDetailText.Text = "";

        var groups = _provider?.GetProperties() ?? [];

        var source = new ObservableCollection<PropertyGroup>(groups);
        GroupsControl.ItemsSource = source;
    }

    // -- Entry events ---------------------------------------------------------

    private void OnEntryRowClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PropertyEntry entry)
            ShowDescription(entry);
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
        else if (e.Key == Key.Escape && sender is TextBox tb2)
        {
            // Revert — refresh will restore the original value
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
}

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
            PropertyEntryType.Boolean => uc.FindResource("BooleanTemplate") as DataTemplate,
            PropertyEntryType.Enum    => uc.FindResource("EnumTemplate") as DataTemplate,
            PropertyEntryType.Integer => uc.FindResource("EditIntegerTemplate") as DataTemplate,
            PropertyEntryType.Hex     => uc.FindResource("EditIntegerTemplate") as DataTemplate,
            _                         => uc.FindResource("EditTextTemplate") as DataTemplate,
        };
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}
