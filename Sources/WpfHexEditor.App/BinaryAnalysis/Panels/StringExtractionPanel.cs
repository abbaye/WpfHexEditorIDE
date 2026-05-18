//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis.Panels;

/// <summary>#110 String Extraction panel — full IDE theme, file selector, encoding picker, context menu, export.</summary>
public sealed class StringExtractionPanel : UserControl, IDisposable
{
    private readonly StringExtractionViewModel _vm = new();
    private DataGrid _grid = null!;
    private TblStream? _loadedTbl;
    private bool _disposed;

    public StringExtractionPanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // toolbar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filter row
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // status bar
        root.SetResourceReference(BackgroundProperty, "TE_Background");

        var toolbar   = BuildToolbar();
        var filterRow = BuildFilterRow();
        var grid      = BuildGrid();
        var statusBar = BuildStatusBar();

        Grid.SetRow(toolbar,   0);
        Grid.SetRow(filterRow, 1);
        Grid.SetRow(grid,      2);
        Grid.SetRow(statusBar, 3);

        root.Children.Add(toolbar);
        root.Children.Add(filterRow);
        root.Children.Add(grid);
        root.Children.Add(statusBar);

        Content = root;
        Focusable = true;
        KeyDown += OnKeyDown;
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private UIElement BuildToolbar()
    {
        var toolbar = new Border
        {
            Padding         = new Thickness(4, 0, 4, 0),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Height          = 26,
        };
        toolbar.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        toolbar.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
        Grid.SetRow(toolbar, 0);

        var row = new DockPanel { LastChildFill = false };

        // Run button
        var runBtn = MakeToolbarButton("", "Run (F5)");
        runBtn.Click += async (_, _) => await _vm.RunAsync();

        // Cancel button
        var cancelBtn = MakeToolbarButton("", "Cancel");
        cancelBtn.Click += (_, _) => _vm.Cancel();

        // Separator
        var sep1 = MakeToolbarSeparator();

        // Load TBL button
        var tblBtn = MakeToolbarButton("", "Load TBL file…");
        tblBtn.Click += OnLoadTbl;

        // Separator
        var sep2 = MakeToolbarSeparator();

        // Export All button
        var exportBtn = MakeToolbarButton("", "Export…");
        exportBtn.Click += (_, _) => OnExport(exportAll: true);

        row.Children.Add(runBtn);
        row.Children.Add(cancelBtn);
        row.Children.Add(sep1);
        row.Children.Add(tblBtn);
        row.Children.Add(sep2);
        row.Children.Add(exportBtn);

        toolbar.Child = row;
        return toolbar;
    }

    private static Button MakeToolbarButton(string glyph, string tooltip)
    {
        var btn = new Button
        {
            Content  = glyph,
            ToolTip  = tooltip,
            Padding  = new Thickness(0),
            Margin   = new Thickness(1, 0, 1, 0),
            Width    = 22,
            Height   = 22,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize   = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
        };
        btn.SetResourceReference(ForegroundProperty,   "Panel_ToolbarForegroundBrush");
        return btn;
    }

    private static Separator MakeToolbarSeparator()
    {
        var sep = new Separator { Width = 1, Margin = new Thickness(4, 3, 4, 3) };
        sep.SetResourceReference(BackgroundProperty, "Panel_ToolbarBorderBrush");
        return sep;
    }

    // ── Filter row (file + encoding + checkboxes + min-length + filter text) ──

    private UIElement BuildFilterRow()
    {
        var bar = new Border
        {
            Padding         = new Thickness(4, 2, 4, 2),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        bar.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        bar.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
        Grid.SetRow(bar, 1);

        var row = new WrapPanel { Orientation = Orientation.Horizontal };

        // File selector
        var fileLabel = new TextBlock { Text = "File:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), FontSize = 11 };
        fileLabel.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        var fileCombo = new ComboBox { Width = 160, Height = 20, FontSize = 11, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        fileCombo.SetResourceReference(ForegroundProperty, "TE_Foreground");
        fileCombo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(_vm.OpenedFiles))  { Source = _vm });
        fileCombo.SetBinding(Selector.SelectedItemProperty,   new Binding(nameof(_vm.SelectedFile)) { Source = _vm, Mode = BindingMode.TwoWay });
        fileCombo.DisplayMemberPath = nameof(OpenedFileItem.DisplayName);
        fileCombo.ToolTip = "Select file to scan";

        // Encoding selector
        var encLabel = new TextBlock { Text = "Encoding:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), FontSize = 11 };
        encLabel.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        var encCombo = BuildEncodingCombo();

        // ASCII / UTF-16 checkboxes
        var asciiChk = MakeCheckBox("ASCII");
        asciiChk.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(_vm.ShowAscii)) { Source = _vm, Mode = BindingMode.TwoWay });

        var utf16Chk = MakeCheckBox("UTF-16");
        utf16Chk.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(_vm.ShowUtf16)) { Source = _vm, Mode = BindingMode.TwoWay });

        // Min length slider
        var minLabel = new TextBlock { Text = "Min:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0), FontSize = 11 };
        minLabel.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        var minBox = new Slider { Minimum = 1, Maximum = 64, Value = 4, Width = 70, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        minBox.SetBinding(RangeBase.ValueProperty, new Binding(nameof(_vm.MinLength)) { Source = _vm, Mode = BindingMode.TwoWay, Converter = new DoubleToIntConverter() });

        // Filter textbox
        var filterLabel = new TextBlock { Text = "Filter:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), FontSize = 11 };
        filterLabel.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        var filterBox = new TextBox { Width = 120, Height = 20, FontSize = 11, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 0) };
        filterBox.SetResourceReference(BackgroundProperty, "TE_Background");
        filterBox.SetResourceReference(ForegroundProperty, "TE_Foreground");
        filterBox.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.Filter)) { Source = _vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        row.Children.Add(fileLabel);
        row.Children.Add(fileCombo);
        row.Children.Add(encLabel);
        row.Children.Add(encCombo);
        row.Children.Add(asciiChk);
        row.Children.Add(utf16Chk);
        row.Children.Add(minLabel);
        row.Children.Add(minBox);
        row.Children.Add(filterLabel);
        row.Children.Add(filterBox);

        bar.Child = row;
        return bar;
    }

    private ComboBox BuildEncodingCombo()
    {
        var combo = new ComboBox { Width = 130, Height = 20, FontSize = 11, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        combo.SetResourceReference(ForegroundProperty, "TE_Foreground");

        void AddGroup(string header)
        {
            combo.Items.Add(new ComboBoxItem { Content = header, IsEnabled = false, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 10 });
        }

        void AddItem(string label, StringEncoding enc)
        {
            var item = new ComboBoxItem { Content = label, Tag = enc, Padding = new Thickness(12, 1, 4, 1) };
            combo.Items.Add(item);
        }

        AddGroup("Built-in Tables");
        AddItem("Default (ASCII)",      StringEncoding.Ascii);
        AddItem("ASCII",                StringEncoding.Ascii);
        AddItem("EBCDIC + Special",     StringEncoding.Ebcdic);
        AddItem("EBCDIC (no spec)",     StringEncoding.EbcdicNoSpec);
        AddGroup("Encodings");
        AddItem("UTF-8",                StringEncoding.Utf8);
        AddItem("UTF-16 LE",            StringEncoding.Utf16Le);
        AddItem("UTF-16 BE",            StringEncoding.Utf16Be);
        AddItem("Latin-1",              StringEncoding.Latin1);
        AddGroup("Project TBL");
        AddItem("(loaded .tbl)",        StringEncoding.Tbl);

        combo.SelectedIndex = 1; // Default (ASCII)
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem { Tag: StringEncoding enc })
                _vm.SelectedEncoding = enc;
        };

        return combo;
    }

    private CheckBox MakeCheckBox(string label)
    {
        var chk = new CheckBox
        {
            Content = label,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
        };
        chk.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        return chk;
    }

    // ── DataGrid ──────────────────────────────────────────────────────────────

    private UIElement BuildGrid()
    {
        _grid = new DataGrid
        {
            AutoGenerateColumns      = false,
            CanUserAddRows           = false,
            CanUserDeleteRows        = false,
            IsReadOnly               = true,
            SelectionMode            = DataGridSelectionMode.Extended,
            EnableRowVirtualization  = true,
            GridLinesVisibility      = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility        = DataGridHeadersVisibility.Column,
            BorderThickness          = new Thickness(0),
        };
        VirtualizingPanel.SetIsVirtualizing(_grid, true);
        VirtualizingPanel.SetVirtualizationMode(_grid, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(_grid, ScrollUnit.Item);

        _grid.SetResourceReference(BackgroundProperty,                           "TE_Background");
        _grid.SetResourceReference(ForegroundProperty,                           "TE_Foreground");
        _grid.SetResourceReference(DataGrid.RowBackgroundProperty,               "TE_Background");
        _grid.SetResourceReference(DataGrid.AlternatingRowBackgroundProperty,    "Panel_ToolbarBrush");

        // Column header style — themed
        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(BackgroundProperty,    new DynamicResourceExtension("Panel_ToolbarBrush")));
        headerStyle.Setters.Add(new Setter(ForegroundProperty,    new DynamicResourceExtension("Panel_ToolbarForegroundBrush")));
        headerStyle.Setters.Add(new Setter(BorderBrushProperty,   new DynamicResourceExtension("Panel_ToolbarBorderBrush")));
        headerStyle.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        headerStyle.Setters.Add(new Setter(PaddingProperty,       new Thickness(6, 3, 6, 3)));
        headerStyle.Setters.Add(new Setter(FontSizeProperty,      11d));
        headerStyle.Setters.Add(new Setter(SnapsToDevicePixelsProperty, true));
        _grid.ColumnHeaderStyle = headerStyle;

        // Row style — themed foreground
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(ForegroundProperty, new DynamicResourceExtension("TE_Foreground")));
        _grid.RowStyle = rowStyle;

        _grid.Columns.Add(MakeCol("Offset",   nameof(StringRun.Offset),   80,  "X8"));
        _grid.Columns.Add(MakeCol("Length",   nameof(StringRun.Length),   55));
        _grid.Columns.Add(MakeCol("Encoding", nameof(StringRun.Encoding), 85));
        _grid.Columns.Add(MakeCol("Value",    nameof(StringRun.Value),    0));

        _grid.ItemsSource       = _vm.ResultsView;
        _grid.MouseDoubleClick += (_, _) => NavigateSelected();
        _grid.ContextMenu       = BuildContextMenu();

        Grid.SetRow(_grid, 2);
        return _grid;
    }

    private static DataGridTextColumn MakeCol(string header, string path, double width, string? format = null)
    {
        var binding = new Binding(path);
        if (format is not null) binding.StringFormat = $"{{0:{format}}}";
        return new DataGridTextColumn
        {
            Header  = header,
            Binding = binding,
            Width   = width > 0 ? new DataGridLength(width) : DataGridLength.Auto,
        };
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private ContextMenu BuildContextMenu()
    {
        var cm = new ContextMenu();
        cm.SetResourceReference(BackgroundProperty, "TE_Background");
        cm.SetResourceReference(ForegroundProperty, "TE_Foreground");

        cm.Items.Add(MakeMenuItem("Go to Offset",        "", () => NavigateSelected()));
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Copy Value",          "", () => CopyField(r => r.Value)));
        cm.Items.Add(MakeMenuItem("Copy Offset",         "", () => CopyField(r => $"0x{r.Offset:X8}")));
        cm.Items.Add(MakeMenuItem("Copy Row (TSV)",      "", () => CopyField(r => $"0x{r.Offset:X8}\t{r.Length}\t{r.Encoding}\t{r.Value}")));
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Export Selected…",    "", () => OnExport(exportAll: false)));
        cm.Items.Add(MakeMenuItem("Export All…",         "", () => OnExport(exportAll: true)));
        return cm;
    }

    private MenuItem MakeMenuItem(string header, string glyph, Action action)
    {
        var icon = new TextBlock
        {
            Text       = glyph,
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize   = 12,
            Width      = 16,
        };
        icon.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        var item = new MenuItem { Header = header, Icon = icon };
        item.SetResourceReference(ForegroundProperty, "TE_Foreground");
        item.Click += (_, _) => action();
        return item;
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    private UIElement BuildStatusBar()
    {
        var bar = new Border
        {
            Padding         = new Thickness(6, 2, 6, 2),
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
        bar.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        bar.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
        Grid.SetRow(bar, 3);

        var statusTxt = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        statusTxt.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        statusTxt.SetBinding(TextBlock.TextProperty, new Binding(nameof(_vm.StatusText)) { Source = _vm });
        bar.Child = statusTxt;
        return bar;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetContext(IIDEHostContext context) => _vm.SetContext(context);
    public void OnFileOpened() => _vm.ResultsView.Refresh();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadedTbl?.Dispose();
        _loadedTbl = null;
        _vm.Dispose();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void NavigateSelected()
    {
        if (_grid.SelectedItem is StringRun run)
            _vm.NavigateToOffset(run);
    }

    private void CopyField(Func<StringRun, string> selector)
    {
        var lines = _grid.SelectedItems.OfType<StringRun>().Select(selector);
        var text  = string.Join(Environment.NewLine, lines);
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)                            { _ = _vm.RunAsync(); e.Handled = true; }
        else if (e.Key == Key.Enter && !_vm.IsBusy)     { NavigateSelected();  e.Handled = true; }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopyField(r => r.Value);
            e.Handled = true;
        }
    }

    private void OnLoadTbl(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title       = "Load TBL file",
            Filter      = "TBL Files (*.tbl;*.tblx)|*.tbl;*.tblx|All Files (*.*)|*.*",
            DefaultExt  = ".tbl",
        };
        if (dlg.ShowDialog() != true) return;

        _loadedTbl?.Dispose();
        _loadedTbl = new TblStream(dlg.FileName);
        _vm.SetTblTable(new TblDecodeTableAdapter(_loadedTbl));
        _vm.SelectedEncoding = StringEncoding.Tbl;
    }

    private void OnExport(bool exportAll)
    {
        var runs = (exportAll ? _vm.GetAllRuns() : _grid.SelectedItems.OfType<StringRun>()).ToList();
        if (runs.Count == 0) return;

        var dlg = new SaveFileDialog
        {
            Title      = exportAll ? "Export All Strings" : "Export Selected Strings",
            Filter     = string.Join("|", StringExtractionExporters.All.Select(x => x.FileFilter)),
            DefaultExt = ".txt",
            FileName   = "strings_export",
        };
        if (dlg.ShowDialog() != true) return;

        var ext      = Path.GetExtension(dlg.FileName);
        var exporter = StringExtractionExporters.All.FirstOrDefault(x => x.DefaultExt.Equals(ext, StringComparison.OrdinalIgnoreCase))
                    ?? StringExtractionExporters.All[0];

        _ = exporter.ExportAsync(runs, dlg.FileName);
    }
}

// ── Local converters ──────────────────────────────────────────────────────────

file sealed class DoubleToIntConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
        => value is int i ? (double)i : value;
    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
        => value is double d ? (int)d : value;
}
