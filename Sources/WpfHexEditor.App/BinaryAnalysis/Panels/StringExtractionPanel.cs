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
    private TextBlock? _tblNameLabel;
    private Button? _tblClearBtn;

    public StringExtractionPanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // toolbar + filter overlap
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // status bar
        root.SetResourceReference(BackgroundProperty, "TE_Background");

        var toolbarPanel = BuildToolbarWithFilter();
        var grid         = BuildGrid();
        var statusBar    = BuildStatusBar();

        Grid.SetRow(toolbarPanel, 0);
        Grid.SetRow(grid,         1);
        Grid.SetRow(statusBar,    2);

        root.Children.Add(toolbarPanel);
        root.Children.Add(grid);
        root.Children.Add(statusBar);

        Content = root;
        Focusable = true;
        KeyDown += OnKeyDown;
    }

    // ── Toolbar + filter overlap (single border, two rows) ───────────────────

    private UIElement BuildToolbarWithFilter()
    {
        var outer = new Border { BorderThickness = new Thickness(0, 0, 0, 1) };
        outer.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        outer.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");

        var stack = new StackPanel();

        // Row 1: action buttons (left, Dock=Left) + search box (right, Dock=Right, fixed width)
        var btnRow = new DockPanel { LastChildFill = false, Height = 26, Margin = new Thickness(4, 0, 4, 0) };

        // Search box anchored to the right with fixed width
        var searchBox = BuildSearchBox();
        DockPanel.SetDock(searchBox, Dock.Right);
        btnRow.Children.Add(searchBox);

        var runBtn    = MakeToolbarButton("", "Run (F5)");
        runBtn.Click += async (_, _) => await _vm.RunAsync();
        var cancelBtn = MakeToolbarButton("", "Cancel");
        cancelBtn.Click += (_, _) => _vm.Cancel();
        var tblBtn    = MakeToolbarButton("", "Load TBL file…");
        tblBtn.Click += OnLoadTbl;
        var exportBtn = MakeToolbarButton("", "Export…");
        exportBtn.Click += (_, _) => OnExport(exportAll: true);

        foreach (UIElement el in new UIElement[] { runBtn, cancelBtn, MakeToolbarSeparator(), tblBtn, MakeToolbarSeparator(), exportBtn })
        {
            DockPanel.SetDock(el, Dock.Left);
            btnRow.Children.Add(el);
        }

        // Row 2: file selector + encodings + min length
        var filterRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2, 4, 4) };

        var fileCombo = new ComboBox
        {
            Width = 160, Height = 20, FontSize = 11,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Select file to scan"
        };
        fileCombo.SetResourceReference(ForegroundProperty, "TE_Foreground");
        fileCombo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(_vm.OpenedFiles))  { Source = _vm });
        fileCombo.SetBinding(Selector.SelectedItemProperty,   new Binding(nameof(_vm.SelectedFile)) { Source = _vm, Mode = BindingMode.TwoWay });
        fileCombo.DisplayMemberPath = nameof(OpenedFileItem.DisplayName);

        var minBox = new TextBox
        {
            Width = 32, Height = 20, FontSize = 11, TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Text = _vm.MinLength.ToString(),
            ToolTip = "Minimum string length (1–64)"
        };
        minBox.SetResourceReference(BackgroundProperty, "TE_Background");
        minBox.SetResourceReference(ForegroundProperty, "TE_Foreground");
        minBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(minBox.Text, out int v)) _vm.MinLength = v;
            minBox.Text = _vm.MinLength.ToString();
        };
        minBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) Keyboard.ClearFocus(); };

        filterRow.Children.Add(MakeLabel("File:"));
        filterRow.Children.Add(fileCombo);
        filterRow.Children.Add(MakeLabel("Encodings:"));
        filterRow.Children.Add(BuildEncodingDropdown());
        filterRow.Children.Add(MakeLabel("Min:"));
        filterRow.Children.Add(minBox);
        filterRow.Children.Add(BuildTblIndicator());

        stack.Children.Add(btnRow);
        stack.Children.Add(filterRow);
        outer.Child = stack;
        return outer;
    }

    /// <summary>Search box with watermark, docked to the right of the toolbar.</summary>
    private UIElement BuildSearchBox()
    {
        var box = new TextBox
        {
            Width  = 140,
            Tag    = "Search strings…",
            ToolTip = "Live filter on extracted strings",
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        box.SetResourceReference(StyleProperty, "PanelSearchBoxStyle");
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.Filter))
        {
            Source = _vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        return box;
    }

    private static Button MakeToolbarButton(string glyph, string tooltip)
    {
        var btn = new Button { Content = glyph, ToolTip = tooltip };
        btn.SetResourceReference(StyleProperty, "PanelIconButtonStyle");
        return btn;
    }

    private static System.Windows.Shapes.Rectangle MakeToolbarSeparator()
    {
        var sep = new System.Windows.Shapes.Rectangle { Width = 1, Height = 16, Margin = new Thickness(4, 0, 4, 0) };
        sep.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Panel_ToolbarBorderBrush");
        return sep;
    }

    private TextBlock MakeLabel(string text)
    {
        var tb = new TextBlock
        {
            Text = text, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0), FontSize = 11
        };
        tb.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        return tb;
    }

    // ── Encoding multi-select dropdown ────────────────────────────────────────

    private UIElement BuildEncodingDropdown()
    {
        var summaryText = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        summaryText.SetResourceReference(ForegroundProperty, "TE_Foreground");

        void RefreshSummary() =>
            summaryText.Text = _vm.ActiveEncodings.Count == 0
                ? "(none)"
                : string.Join(", ", _vm.ActiveEncodings.Select(EncodingLabel));

        var popup = new Popup { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Bottom };
        var listBorder = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(2) };
        listBorder.SetResourceReference(BackgroundProperty,  "TE_Background");
        listBorder.SetResourceReference(BorderBrushProperty, "Panel_ToolbarBorderBrush");

        var listPanel = new StackPanel();

        (string label, StringEncoding? enc)[] items =
        [
            ("── Built-in ──",   null),
            ("ASCII",                                 StringEncoding.Ascii),
            ("EBCDIC + Special",                      StringEncoding.Ebcdic),
            ("EBCDIC (no spec)",                      StringEncoding.EbcdicNoSpec),
            ("── Encodings ──",  null),
            ("UTF-8",                                 StringEncoding.Utf8),
            ("UTF-16 LE",                             StringEncoding.Utf16Le),
            ("UTF-16 BE",                             StringEncoding.Utf16Be),
            ("Latin-1",                               StringEncoding.Latin1),
            ("── Project ──",    null),
            ("TBL (loaded)",                          StringEncoding.Tbl),
        ];

        var checkBoxes = new List<CheckBox>();
        foreach (var (label, enc) in items)
        {
            if (enc is null)
            {
                var hdr = new TextBlock
                {
                    Text = label, FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(4, 2, 4, 0), IsEnabled = false
                };
                hdr.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
                listPanel.Children.Add(hdr);
            }
            else
            {
                var chk = new CheckBox
                {
                    Content = label, Tag = enc,
                    IsChecked = _vm.IsEncodingActive(enc.Value),
                    Padding = new Thickness(4, 1, 8, 1),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chk.SetResourceReference(ForegroundProperty, "TE_Foreground");
                chk.Click += (_, _) =>
                {
                    _vm.ToggleEncoding(enc.Value);
                    chk.IsChecked = _vm.IsEncodingActive(enc.Value);
                    foreach (var c in checkBoxes)
                        if (c.Tag is StringEncoding e2) c.IsChecked = _vm.IsEncodingActive(e2);
                    RefreshSummary();
                };
                checkBoxes.Add(chk);
                listPanel.Children.Add(chk);
            }
        }

        listBorder.Child = listPanel;
        popup.Child      = listBorder;

        var btn = new Button
        {
            Height = 20, MinWidth = 130,
            Padding = new Thickness(6, 0, 6, 0),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment   = VerticalAlignment.Center,
            FontSize = 11,
            Margin = new Thickness(0, 0, 8, 0),
            FocusVisualStyle = null,
        };
        btn.SetResourceReference(BackgroundProperty,  "TE_Background");
        btn.SetResourceReference(ForegroundProperty,  "TE_Foreground");
        btn.SetResourceReference(BorderBrushProperty, "Panel_ToolbarBorderBrush");
        btn.Content = summaryText;
        btn.Click   += (_, _) => { popup.PlacementTarget = btn; popup.IsOpen = !popup.IsOpen; };

        RefreshSummary();
        return btn;
    }

    /// <summary>TBL indicator: "TBL: filename.tbl [×]" shown when a TBL is loaded.</summary>
    private UIElement BuildTblIndicator()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };

        _tblNameLabel = new TextBlock
        {
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            Visibility = Visibility.Collapsed,
        };
        _tblNameLabel.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        _tblClearBtn = new Button
        {
            Content = "",
            ToolTip = "Remove TBL",
            Width = 16, Height = 16,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 9,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            FocusVisualStyle = null,
        };
        _tblClearBtn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        _tblClearBtn.Click += (_, _) => ClearTbl();

        panel.Children.Add(MakeLabel("TBL:"));
        panel.Children.Add(_tblNameLabel);
        panel.Children.Add(_tblClearBtn);
        return panel;
    }

    private void UpdateTblIndicator(string? path)
    {
        if (_tblNameLabel is null || _tblClearBtn is null) return;
        if (path is null)
        {
            _tblNameLabel.Text       = string.Empty;
            _tblNameLabel.Visibility = Visibility.Collapsed;
            _tblClearBtn.Visibility  = Visibility.Collapsed;
        }
        else
        {
            _tblNameLabel.Text       = System.IO.Path.GetFileName(path);
            _tblNameLabel.ToolTip    = path;
            _tblNameLabel.Visibility = Visibility.Visible;
            _tblClearBtn.Visibility  = Visibility.Visible;
        }
    }

    private void ClearTbl()
    {
        _loadedTbl?.Dispose();
        _loadedTbl = null;
        _vm.SetTblTable(null);
        UpdateTblIndicator(null);
    }

    private static string EncodingLabel(StringEncoding enc) => enc switch
    {
        StringEncoding.Ascii        => "ASCII",
        StringEncoding.Utf16Le      => "UTF-16 LE",
        StringEncoding.Utf16Be      => "UTF-16 BE",
        StringEncoding.Utf8         => "UTF-8",
        StringEncoding.Latin1       => "Latin-1",
        StringEncoding.Ebcdic       => "EBCDIC+",
        StringEncoding.EbcdicNoSpec => "EBCDIC",
        StringEncoding.Tbl          => "TBL",
        _                           => enc.ToString(),
    };

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

        _grid.SetResourceReference(BackgroundProperty,                        "TE_Background");
        _grid.SetResourceReference(ForegroundProperty,                        "TE_Foreground");
        _grid.SetResourceReference(DataGrid.RowBackgroundProperty,            "TE_Background");
        _grid.SetResourceReference(DataGrid.AlternatingRowBackgroundProperty, "Panel_ToolbarBrush");

        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(BackgroundProperty,      new DynamicResourceExtension("Panel_ToolbarBrush")));
        headerStyle.Setters.Add(new Setter(ForegroundProperty,      new DynamicResourceExtension("Panel_ToolbarForegroundBrush")));
        headerStyle.Setters.Add(new Setter(BorderBrushProperty,     new DynamicResourceExtension("Panel_ToolbarBorderBrush")));
        headerStyle.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        headerStyle.Setters.Add(new Setter(PaddingProperty,         new Thickness(6, 3, 6, 3)));
        headerStyle.Setters.Add(new Setter(FontSizeProperty,        11d));
        headerStyle.Setters.Add(new Setter(SnapsToDevicePixelsProperty, true));
        _grid.ColumnHeaderStyle = headerStyle;

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

        cm.Items.Add(MakeMenuItem("Go to Offset",     "", () => NavigateSelected()));
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Copy Value",       "", () => CopyField(r => r.Value)));
        cm.Items.Add(MakeMenuItem("Copy Offset",      "", () => CopyField(r => $"0x{r.Offset:X8}")));
        cm.Items.Add(MakeMenuItem("Copy Row (TSV)",   "", () => CopyField(r => $"0x{r.Offset:X8}\t{r.Length}\t{r.Encoding}\t{r.Value}")));
        cm.Items.Add(new Separator());
        cm.Items.Add(MakeMenuItem("Export Selected…", "", () => OnExport(exportAll: false)));
        cm.Items.Add(MakeMenuItem("Export All…",      "", () => OnExport(exportAll: true)));
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
        if (e.Key == Key.F5)                        { _ = _vm.RunAsync(); e.Handled = true; }
        else if (e.Key == Key.Enter && !_vm.IsBusy) { NavigateSelected();  e.Handled = true; }
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
            Title      = "Load TBL file",
            Filter     = "TBL Files (*.tbl;*.tblx)|*.tbl;*.tblx|All Files (*.*)|*.*",
            DefaultExt = ".tbl",
        };
        if (dlg.ShowDialog() != true) return;

        _loadedTbl?.Dispose();
        _loadedTbl = new TblStream(dlg.FileName);
        _vm.SetTblTable(new TblDecodeTableAdapter(_loadedTbl));
        _vm.AddFileToCombo(dlg.FileName);
        UpdateTblIndicator(dlg.FileName);
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
