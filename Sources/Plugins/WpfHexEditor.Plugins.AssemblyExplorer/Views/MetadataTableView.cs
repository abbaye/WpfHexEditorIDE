// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/MetadataTableView.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Code-behind-only UserControl that renders a metadata table's decoded rows
//     in an auto-generated-column DataGrid. Row click navigates the hex editor
//     to the member's PE byte offset. A toolbar hosts the Export CSV button.
//
// Architecture Notes:
//     Pattern: MVVM — binds to MetadataTableNodeViewModel.
//     No XAML file: all UI is constructed in code (codebase convention for plugin views).
//     DataGrid columns are auto-generated from MetadataTableRow.Columns dynamically.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.AssemblyAnalysis;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

/// <summary>
/// Full-featured metadata table browser rendered as a code-behind-only UserControl.
/// Binds to <see cref="MetadataTableNodeViewModel"/>.
/// </summary>
public sealed class MetadataTableView : UserControl
{
    private readonly MetadataTableNodeViewModel _vm;
    private readonly DataGrid                   _grid;

    // Callback wired by the hosting panel to navigate the hex editor on row click.
    private Action<long>? _navigateToOffset;

    public MetadataTableView(MetadataTableNodeViewModel vm)
    {
        _vm          = vm ?? throw new ArgumentNullException(nameof(vm));
        DataContext  = vm;

        // ── Root layout ────────────────────────────────────────────────────
        var root = new DockPanel { LastChildFill = true };

        // ── Toolbar ────────────────────────────────────────────────────────
        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        // ── DataGrid ───────────────────────────────────────────────────────
        _grid = new DataGrid
        {
            AutoGenerateColumns  = false,
            IsReadOnly           = true,
            SelectionMode        = DataGridSelectionMode.Single,
            SelectionUnit        = DataGridSelectionUnit.FullRow,
            CanUserAddRows       = false,
            CanUserDeleteRows    = false,
            CanUserReorderColumns = true,
            CanUserResizeColumns  = true,
            CanUserSortColumns    = true,
            GridLinesVisibility  = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility    = DataGridHeadersVisibility.Column,
            Background           = Brushes.Transparent,
            RowBackground        = Brushes.Transparent,
            AlternatingRowBackground = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            FontSize             = 12,
            Margin               = new Thickness(0)
        };

        // Static columns that are always present.
        _grid.Columns.Add(MakeColumn("Row",   nameof(MetadataTableRow.RowNumber),   50));
        _grid.Columns.Add(MakeTextColumn("Token", nameof(MetadataTableRow.Token),   90,
            cell => $"0x{((MetadataTableRow)cell).Token:X8}"));
        _grid.Columns.Add(MakeColumn("Offset", nameof(MetadataTableRow.FileOffset), 90));

        // Dynamic columns — built from the first row's column list.
        // Columns are regenerated when the row collection changes.
        _vm.Rows.CollectionChanged += (_, _) => RebuildDynamicColumns();
        RebuildDynamicColumns();

        _grid.ItemsSource = _vm.Rows;

        // Row click → navigate hex editor.
        _grid.MouseDoubleClick += OnRowDoubleClick;
        _grid.KeyDown          += OnGridKeyDown;

        root.Children.Add(_grid);
        Content = root;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Wires a callback that is invoked when the user double-clicks a row,
    /// allowing the hosting panel to navigate the hex editor to the member's offset.
    /// </summary>
    public void SetNavigateCallback(Action<long> navigateToOffset)
        => _navigateToOffset = navigateToOffset;

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private ToolBar BuildToolbar()
    {
        var tb = new ToolBar
        {
            Background  = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Margin      = new Thickness(0, 0, 0, 2)
        };

        var exportBtn = new Button
        {
            Content    = "Export CSV",
            ToolTip    = "Export all rows as a CSV file",
            Padding    = new Thickness(8, 3, 8, 3),
            Margin     = new Thickness(2, 1, 2, 1),
            Command    = _vm.ExportCsvCommand
        };

        var countLabel = new TextBlock
        {
            Text       = $"{_vm.RowCount} rows",
            Margin     = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray,
            FontSize   = 11
        };

        tb.Items.Add(exportBtn);
        tb.Items.Add(new Separator());
        tb.Items.Add(countLabel);

        return tb;
    }

    // ── Dynamic column generation ─────────────────────────────────────────────

    private void RebuildDynamicColumns()
    {
        // Remove existing dynamic columns (keep the 3 static ones).
        while (_grid.Columns.Count > 3)
            _grid.Columns.RemoveAt(_grid.Columns.Count - 1);

        if (_vm.Rows.Count == 0) return;

        // Build one column per decoded column in the first row.
        var firstRow = _vm.Rows[0];
        for (var i = 0; i < firstRow.Columns.Count; i++)
        {
            var colIndex  = i;
            var colName   = firstRow.Columns[i].ColumnName;

            var col = new DataGridTextColumn
            {
                Header  = colName,
                Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = new System.Windows.Data.Binding($"Columns[{colIndex}].Value")
            };
            _grid.Columns.Add(col);
        }
    }

    // ── Row interaction ───────────────────────────────────────────────────────

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_grid.SelectedItem is MetadataTableRow row && row.FileOffset > 0)
            _navigateToOffset?.Invoke(row.FileOffset);
    }

    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && _grid.SelectedItem is MetadataTableRow row
            && row.FileOffset > 0)
        {
            _navigateToOffset?.Invoke(row.FileOffset);
            e.Handled = true;
        }
    }

    // ── Column factories ──────────────────────────────────────────────────────

    private static DataGridTextColumn MakeColumn(string header, string bindingPath, double width)
        => new()
        {
            Header  = header,
            Binding = new System.Windows.Data.Binding(bindingPath),
            Width   = new DataGridLength(width)
        };

    private static DataGridTextColumn MakeTextColumn(
        string header, string bindingPath, double width,
        Func<object, string>? formatter = null)
    {
        var col = new DataGridTextColumn
        {
            Header  = header,
            Binding = formatter is null
                ? new System.Windows.Data.Binding(bindingPath)
                : new System.Windows.Data.Binding(bindingPath)
                  {
                      Converter = new FuncConverter(formatter)
                  },
            Width = new DataGridLength(width)
        };
        return col;
    }

    // ── Inline converter helper ───────────────────────────────────────────────

    private sealed class FuncConverter : System.Windows.Data.IValueConverter
    {
        private readonly Func<object, string> _fn;
        public FuncConverter(Func<object, string> fn) => _fn = fn;

        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
            => value is null ? string.Empty : _fn(value);

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
