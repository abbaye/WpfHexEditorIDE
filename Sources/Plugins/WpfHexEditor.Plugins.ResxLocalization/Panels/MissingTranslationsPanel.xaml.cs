// ==========================================================
// Project: WpfHexEditor.Plugins.ResxLocalization
// File: Panels/MissingTranslationsPanel.xaml.cs
// Description:
//     Code-behind for the Missing Translations panel.
//     Dynamically generates DataGrid columns when the locale
//     set changes and provides "Copy base value" button logic.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.ResxEditor.Models;
using WpfHexEditor.Plugins.ResxLocalization.ViewModels;

namespace WpfHexEditor.Plugins.ResxLocalization.Panels;

/// <summary>
/// Dockable panel showing a key × locale matrix highlighting missing translations.
/// </summary>
public partial class MissingTranslationsPanel : UserControl
{
    private readonly MissingTranslationsViewModel _vm = new();

    public MissingTranslationsPanel()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    // -----------------------------------------------------------------------
    // Public API — called by plugin
    // -----------------------------------------------------------------------

    public void Refresh(IReadOnlyList<(string CultureCode, ResxDocument Doc)> locales)
    {
        _vm.Refresh(locales);
        RebuildColumns(locales.Select(l => l.CultureCode).ToList());
        MatrixGrid.ItemsSource = _vm.Rows;
    }

    // -----------------------------------------------------------------------
    // Column generation
    // -----------------------------------------------------------------------

    private void RebuildColumns(IReadOnlyList<string> cultures)
    {
        MatrixGrid.Columns.Clear();

        // Fixed Key column
        MatrixGrid.Columns.Add(new DataGridTextColumn
        {
            Header  = "Key",
            Binding = new System.Windows.Data.Binding("Key"),
            Width   = new DataGridLength(160),
            MinWidth = 80,
            IsReadOnly = true
        });

        // One column per culture — binds into the Cells list by index via code-behind template
        for (int i = 0; i < cultures.Count; i++)
        {
            var idx = i; // capture for lambda
            var col = new DataGridTemplateColumn
            {
                Header    = cultures[i],
                Width     = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth  = 70,
                IsReadOnly = true,
                CellTemplate = BuildCellTemplate(idx)
            };
            MatrixGrid.Columns.Add(col);
        }
    }

    private static DataTemplate BuildCellTemplate(int cellIndex)
    {
        // Build a DataTemplate programmatically that binds Cells[cellIndex]
        var factory = new FrameworkElementFactory(typeof(Border));

        factory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(
            $"Cells[{cellIndex}].IsMissing")
        {
            Converter = new MissingToBrushConverter()
        });
        factory.SetValue(Border.PaddingProperty, new Thickness(4, 1, 4, 1));

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding($"Cells[{cellIndex}].Value")
            {
                TargetNullValue = "(missing)"
            });
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        factory.AppendChild(text);

        return new DataTemplate { VisualTree = factory };
    }

    // -----------------------------------------------------------------------
    // Button handlers
    // -----------------------------------------------------------------------

    private void CopyBaseButton_Click(object sender, RoutedEventArgs e)
    {
        // Copy base column value (Cells[0].Value) into all missing cells in selected rows
        foreach (TranslationRowViewModel row in MatrixGrid.SelectedItems.OfType<TranslationRowViewModel>())
        {
            if (row.Cells.Count < 2) continue;
            var baseValue = row.Cells[0].Value;

            for (int i = 1; i < row.Cells.Count; i++)
            {
                if (row.Cells[i].IsMissing)
                {
                    row.Cells[i].Value    = baseValue;
                    row.Cells[i].IsMissing = false;
                }
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Converter: bool IsMissing → Brush
// ---------------------------------------------------------------------------

file sealed class MissingToBrushConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        if (value is true)
            return Application.Current.TryFindResource("RES_LocaleMissingBrush") as Brush
                   ?? Brushes.DarkRed;
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
