//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/ValidationBar.xaml.cs
// Description: Inline validation summary strip bound to ValidationSummaryItem collection.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.StructureEditor.Properties;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

public sealed partial class ValidationBar : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource),
            typeof(ObservableCollection<ValidationSummaryItem>),
            typeof(ValidationBar),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty ErrorCountProperty =
        DependencyProperty.Register(nameof(ErrorCount), typeof(int), typeof(ValidationBar),
            new PropertyMetadata(0, OnCountChanged));

    public static readonly DependencyProperty WarningCountProperty =
        DependencyProperty.Register(nameof(WarningCount), typeof(int), typeof(ValidationBar),
            new PropertyMetadata(0, OnCountChanged));

    public ObservableCollection<ValidationSummaryItem>? ItemsSource
    {
        get => (ObservableCollection<ValidationSummaryItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int ErrorCount
    {
        get => (int)GetValue(ErrorCountProperty);
        set => SetValue(ErrorCountProperty, value);
    }

    public int WarningCount
    {
        get => (int)GetValue(WarningCountProperty);
        set => SetValue(WarningCountProperty, value);
    }

    public bool HasItems => (ItemsSource?.Count ?? 0) > 0;

    public ValidationBar() => InitializeComponent();

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var bar = (ValidationBar)d;
        bar.ItemsPanel.ItemsSource = (ObservableCollection<ValidationSummaryItem>?)e.NewValue;
        bar.Visibility = bar.HasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var bar = (ValidationBar)d;
        bar.ErrorCountText.Text   = bar.ErrorCount   > 0 ? string.Format(StructureEditorResources.StructureEditor_ValidationErrorCount,   bar.ErrorCount)   : "";
        bar.WarningCountText.Text = bar.WarningCount > 0 ? string.Format(StructureEditorResources.StructureEditor_ValidationWarningCount, bar.WarningCount) : "";
    }
}
