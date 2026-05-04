// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/ParallelWatchPanel.xaml.cs
// Description: Code-behind for Parallel Watch — generates thread columns dynamically.
// ==========================================================

using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class ParallelWatchPanel : UserControl
{
    private ParallelWatchViewModel? Vm => DataContext as ParallelWatchViewModel;

    public ParallelWatchPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookViewModel();
    }

    private void HookViewModel()
    {
        if (Vm is null) return;
        Grid.ItemsSource = Vm.Rows;
        Vm.Columns.CollectionChanged += OnColumnsChanged;
        RebuildColumns();
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildColumns();

    private void RebuildColumns()
    {
        if (Vm is null) return;

        // Keep the first "Expression" column, remove thread columns
        while (DynamicGridView.Columns.Count > 1)
            DynamicGridView.Columns.RemoveAt(1);

        foreach (var col in Vm.Columns)
        {
            var threadId = col.Id;
            var gvc = new GridViewColumn
            {
                Header = $"[{col.Id}] {col.Name}",
                Width  = 160,
                CellTemplate = BuildValueTemplate(threadId),
            };
            DynamicGridView.Columns.Add(gvc);
        }
    }

    private static DataTemplate BuildValueTemplate(int threadId)
    {
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        var binding = new Binding($"[{threadId}]")
        {
            Source         = null,
            Mode           = BindingMode.OneWay,
            RelativeSource = new RelativeSource(RelativeSourceMode.Self),
        };
        // Use a converter that calls GetValue(threadId) on the row
        factory.SetBinding(TextBlock.TextProperty,
            new Binding { Converter = new ThreadValueConverter(threadId) });
        var template = new DataTemplate { VisualTree = factory };
        return template;
    }
}

/// <summary>Converts a ParallelWatchRow to its value string for a specific thread.</summary>
file sealed class ThreadValueConverter(int threadId) : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is ParallelWatchRow row ? row.GetValue(threadId) : "—";

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => System.Windows.DependencyProperty.UnsetValue;
}
