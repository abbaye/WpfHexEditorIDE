// Project     : WpfHexEditor.App
// File        : StringDiffPanel.cs
// Description : Diff panel — pick two snapshots, compare, view Added/Removed/Modified/Unchanged.
// Architecture: Code-behind UserControl; delegates diff logic to StringDiffService.
//               Columns use FrameworkElementFactory + IValueConverter (stateless) instead of
//               LoadedEvent + DataContextChanged, which caused an infinite-loop with
//               VirtualizationMode.Recycling (each recycle fired Loaded again, accumulating
//               handlers that fired on every subsequent DataContext swap).
//               Status filter: CollectionViewSource + Predicate — O(n) filter, no re-diff.
//               Row coloring: DataGrid.RowStyle DataTrigger — zero allocations at render time.
//               Text search: applied as an extra predicate on the same CollectionView.
//               Sort: ICollectionView.SortDescriptions, multi-column (shift+click header).

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;

namespace WpfHexEditor.App.BinaryAnalysis.Panels;

// ── Converters ────────────────────────────────────────────────────────────────

/// <summary>Maps <see cref="StringDiffEntry"/> → display text for a given column.</summary>
internal sealed class DiffTextConverter(Func<StringDiffEntry, string> selector) : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is StringDiffEntry e ? selector(e) : string.Empty;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Maps <see cref="StringDiffEntry.Status"/> → badge background brush.</summary>
internal sealed class DiffStatusBrushConverter : IValueConverter
{
    internal static readonly DiffStatusBrushConverter Instance = new();

    private static readonly SolidColorBrush AddedBrush    = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)));
    private static readonly SolidColorBrush RemovedBrush  = Freeze(new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)));
    private static readonly SolidColorBrush ModifiedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00)));
    private static readonly SolidColorBrush NeutralBrush  = Freeze(new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is StringDiffStatus s ? StatusBrush(s) : NeutralBrush;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;

    internal static SolidColorBrush StatusBrush(StringDiffStatus s) => s switch
    {
        StringDiffStatus.Added    => AddedBrush,
        StringDiffStatus.Removed  => RemovedBrush,
        StringDiffStatus.Modified => ModifiedBrush,
        _                         => NeutralBrush,
    };

    // Tinted row backgrounds — same hue as badge but very dark, 15% opacity equivalent.
    internal static readonly SolidColorBrush AddedRowBrush    = Freeze(new SolidColorBrush(Color.FromArgb(0x26, 0x2E, 0x7D, 0x32)));
    internal static readonly SolidColorBrush RemovedRowBrush  = Freeze(new SolidColorBrush(Color.FromArgb(0x26, 0xC6, 0x28, 0x28)));
    internal static readonly SolidColorBrush ModifiedRowBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x26, 0xE6, 0x51, 0x00)));

    internal static string StatusLabel(StringDiffStatus s) => s switch
    {
        StringDiffStatus.Added    => "Added",
        StringDiffStatus.Removed  => "Removed",
        StringDiffStatus.Modified => "Modified",
        _                         => "Unchanged",
    };
}

/// <summary>Maps <see cref="StringDiffEntry.Status"/> → badge label string.</summary>
internal sealed class DiffStatusLabelConverter : IValueConverter
{
    internal static readonly DiffStatusLabelConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is StringDiffStatus s ? DiffStatusBrushConverter.StatusLabel(s) : string.Empty;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>
/// Maps <see cref="StringDiffEntry.OldValue"/> → display text.
/// Returns "—" (em dash) in italic style when null/empty (Added/Removed entries).
/// </summary>
internal sealed class DiffOldValueConverter : IValueConverter
{
    internal static readonly DiffOldValueConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is string s && s.Length > 0 ? s : "—";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Maps OldValue null/empty → italic FontStyle for the placeholder dash.</summary>
internal sealed class DiffOldValueStyleConverter : IValueConverter
{
    internal static readonly DiffOldValueStyleConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is string s && s.Length > 0 ? FontStyles.Normal : FontStyles.Italic;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

// ── Panel ─────────────────────────────────────────────────────────────────────

internal sealed class StringDiffPanel : UserControl
{
    private readonly StringExtractionViewModel _vm;

    // UI references
    private DataGrid  _grid        = null!;
    private ComboBox  _snapACombo  = null!;
    private ComboBox  _snapBCombo  = null!;
    private TextBlock _summaryText = null!;
    private TextBox   _searchBox   = null!;

    // Filter state — active status flags
    private HashSet<StringDiffStatus> _activeFilters = [
        StringDiffStatus.Added,
        StringDiffStatus.Removed,
        StringDiffStatus.Modified,
        StringDiffStatus.Unchanged,
    ];

    // Legend counter TextBlocks — updated after each diff/filter
    private TextBlock _addedCount    = null!;
    private TextBlock _removedCount  = null!;
    private TextBlock _modifiedCount = null!;
    private TextBlock _unchangedCount = null!;

    // Toggle buttons for status filter (kept to update IsChecked state)
    private ToggleButton _btnAdded     = null!;
    private ToggleButton _btnRemoved   = null!;
    private ToggleButton _btnModified  = null!;
    private ToggleButton _btnUnchanged = null!;

    // Full diff result (before status filtering)
    private IReadOnlyList<StringDiffEntry> _lastDiffResult = [];

    // CollectionView wrapping the DataGrid ItemsSource for in-place filter + sort
    private ListCollectionView? _collectionView;

    // Current text search term (lower-case, trimmed)
    private string _searchTerm = string.Empty;

    public StringDiffPanel(StringExtractionViewModel vm)
    {
        _vm     = vm;
        Content = BuildLayout();
    }

    private UIElement BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.SetResourceReference(BackgroundProperty, "TE_Background");

        var snapshotBar = BuildSnapshotBar();
        var filterBar   = BuildFilterBar();
        var searchBar   = BuildSearchBar();
        var grid        = BuildDataGrid();
        var legend      = BuildLegend();

        Grid.SetRow(snapshotBar, 0);
        Grid.SetRow(filterBar,   1);
        Grid.SetRow(searchBar,   2);
        Grid.SetRow(grid,        3);
        Grid.SetRow(legend,      4);
        root.Children.Add(snapshotBar);
        root.Children.Add(filterBar);
        root.Children.Add(searchBar);
        root.Children.Add(grid);
        root.Children.Add(legend);
        return root;
    }

    // ── Snapshot selection bar ─────────────────────────────────────────────────

    private UIElement BuildSnapshotBar()
    {
        var toolbar = new Grid { Margin = new Thickness(4, 4, 4, 2) };
        toolbar.SetResourceReference(BackgroundProperty, "Panel_ToolbarBrush");
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // 0: "Snapshot A:"
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1: combo A
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // 2: swap btn
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) }); // 3: sep
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // 4: "Snapshot B:"
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 5: combo B
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // 6: compare btn
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // 7: export btn
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // 8: summary

        _snapACombo = MakeSnapshotCombo();
        _snapBCombo = MakeSnapshotCombo();
        _snapACombo.SelectionChanged += OnSnapshotSelectionChanged;
        _snapBCombo.SelectionChanged += OnSnapshotSelectionChanged;

        // Swap A↔B button
        var swapBtn = new Button
        {
            Content          = "⇄",
            ToolTip          = "Swap Snapshot A ↔ B",
            Width            = 26,
            Height           = 22,
            FontSize         = 13,
            Padding          = new Thickness(0),
            Margin           = new Thickness(2, 0, 2, 0),
            FocusVisualStyle = null,
        };
        swapBtn.SetResourceReference(StyleProperty,      "PanelIconButtonStyle");
        swapBtn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        swapBtn.Click += OnSwapSnapshots;

        var sep = new Border
        {
            Width             = 1,
            Margin            = new Thickness(0, 3, 0, 3),
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        sep.SetResourceReference(Border.BackgroundProperty, "Panel_ToolbarForegroundBrush");

        var compareBtn = new Button
        {
            Content          = "⟳ Compare",
            Padding          = new Thickness(10, 3, 10, 3),
            Margin           = new Thickness(4, 0, 0, 0),
            FontSize         = 11,
            FocusVisualStyle = null,
        };
        compareBtn.SetResourceReference(StyleProperty,      "PanelIconButtonStyle");
        compareBtn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        compareBtn.Click += (_, _) => RunDiff();

        var exportBtn = new Button
        {
            Content          = "↓ CSV",
            ToolTip          = "Export visible rows to CSV",
            Padding          = new Thickness(8, 3, 8, 3),
            Margin           = new Thickness(4, 0, 0, 0),
            FontSize         = 11,
            FocusVisualStyle = null,
        };
        exportBtn.SetResourceReference(StyleProperty,      "PanelIconButtonStyle");
        exportBtn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        exportBtn.Click += (_, _) => ExportCsv();

        _summaryText = new TextBlock
        {
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 4, 0),
        };
        _summaryText.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        Grid.SetColumn(MakeLabel("Snapshot A:"), 0); toolbar.Children.Add(MakeLabel("Snapshot A:"));
        Grid.SetColumn(_snapACombo, 1);              toolbar.Children.Add(_snapACombo);
        Grid.SetColumn(swapBtn,     2);              toolbar.Children.Add(swapBtn);
        Grid.SetColumn(sep,         3);              toolbar.Children.Add(sep);
        var bLabel = MakeLabel("Snapshot B:");
        Grid.SetColumn(bLabel,      4);              toolbar.Children.Add(bLabel);
        Grid.SetColumn(_snapBCombo, 5);              toolbar.Children.Add(_snapBCombo);
        Grid.SetColumn(compareBtn,  6);              toolbar.Children.Add(compareBtn);
        Grid.SetColumn(exportBtn,   7);              toolbar.Children.Add(exportBtn);
        Grid.SetColumn(_summaryText,8);              toolbar.Children.Add(_summaryText);

        return toolbar;
    }

    private void OnSnapshotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_snapACombo.SelectedItem is not null && _snapBCombo.SelectedItem is not null)
            RunDiff();
    }

    private void OnSwapSnapshots(object sender, RoutedEventArgs e)
    {
        // Temporarily unsubscribe to prevent double diff during swap.
        _snapACombo.SelectionChanged -= OnSnapshotSelectionChanged;
        _snapBCombo.SelectionChanged -= OnSnapshotSelectionChanged;

        var tmp = _snapACombo.SelectedItem;
        _snapACombo.SelectedItem = _snapBCombo.SelectedItem;
        _snapBCombo.SelectedItem = tmp;

        _snapACombo.SelectionChanged += OnSnapshotSelectionChanged;
        _snapBCombo.SelectionChanged += OnSnapshotSelectionChanged;

        if (_snapACombo.SelectedItem is not null && _snapBCombo.SelectedItem is not null)
            RunDiff();
    }

    // ── Filter chips bar ──────────────────────────────────────────────────────

    private UIElement BuildFilterBar()
    {
        var bar = new WrapPanel { Margin = new Thickness(4, 0, 4, 2), Orientation = Orientation.Horizontal };
        bar.SetResourceReference(BackgroundProperty, "Panel_ToolbarBrush");

        var showLbl = new TextBlock
        {
            Text              = "Show:",
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 6, 0),
        };
        showLbl.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        bar.Children.Add(showLbl);

        _btnAdded     = MakeFilterChip("Added",     StringDiffStatus.Added,     DiffStatusBrushConverter.AddedRowBrush,    DiffStatusBrushConverter.StatusBrush(StringDiffStatus.Added));
        _btnRemoved   = MakeFilterChip("Removed",   StringDiffStatus.Removed,   DiffStatusBrushConverter.RemovedRowBrush,  DiffStatusBrushConverter.StatusBrush(StringDiffStatus.Removed));
        _btnModified  = MakeFilterChip("Modified",  StringDiffStatus.Modified,  DiffStatusBrushConverter.ModifiedRowBrush, DiffStatusBrushConverter.StatusBrush(StringDiffStatus.Modified));
        _btnUnchanged = MakeFilterChip("Unchanged", StringDiffStatus.Unchanged, Brushes.Transparent,                       DiffStatusBrushConverter.StatusBrush(StringDiffStatus.Unchanged));

        bar.Children.Add(_btnAdded);
        bar.Children.Add(_btnRemoved);
        bar.Children.Add(_btnModified);
        bar.Children.Add(_btnUnchanged);
        return bar;
    }

    private ToggleButton MakeFilterChip(string label, StringDiffStatus status,
                                        Brush bgUnchecked, SolidColorBrush badgeBrush)
    {
        var btn = new ToggleButton
        {
            IsChecked        = true,
            Padding          = new Thickness(8, 2, 8, 2),
            Margin           = new Thickness(0, 0, 4, 2),
            FontSize         = 10,
            FontWeight       = FontWeights.SemiBold,
            FocusVisualStyle = null,
            Content          = label,
            Background       = badgeBrush,
            Foreground       = Brushes.White,
            BorderThickness  = new Thickness(0),
        };
        btn.Checked   += (_, _) => { _activeFilters.Add(status);    ApplyFilter(); };
        btn.Unchecked += (_, _) => { _activeFilters.Remove(status); ApplyFilter(); };
        return btn;
    }

    // ── Search bar ────────────────────────────────────────────────────────────

    private UIElement BuildSearchBar()
    {
        var bar = new DockPanel { Margin = new Thickness(4, 0, 4, 2), LastChildFill = true };
        bar.SetResourceReference(BackgroundProperty, "Panel_ToolbarBrush");

        var lbl = new TextBlock
        {
            Text              = "🔍",
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 4, 0),
        };
        lbl.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        DockPanel.SetDock(lbl, Dock.Left);

        _searchBox = new TextBox
        {
            Height           = 20,
            FontSize         = 10,
            Padding          = new Thickness(4, 1, 4, 1),
            BorderThickness  = new Thickness(1),
        };
        _searchBox.SetResourceReference(BackgroundProperty,  "TE_Background");
        _searchBox.SetResourceReference(ForegroundProperty,  "TE_Foreground");
        _searchBox.TextChanged += OnSearchChanged;

        var clearBtn = new Button
        {
            Content          = "✕",
            Width            = 20,
            Height           = 20,
            FontSize         = 9,
            Padding          = new Thickness(0),
            Margin           = new Thickness(2, 0, 0, 0),
            FocusVisualStyle = null,
        };
        clearBtn.SetResourceReference(StyleProperty,      "PanelIconButtonStyle");
        clearBtn.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");
        clearBtn.Click += (_, _) => _searchBox.Clear();
        DockPanel.SetDock(clearBtn, Dock.Right);

        bar.Children.Add(lbl);
        bar.Children.Add(clearBtn);
        bar.Children.Add(_searchBox);
        return bar;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchTerm = _searchBox.Text.Trim().ToLowerInvariant();
        ApplyFilter();
    }

    // ── DataGrid ──────────────────────────────────────────────────────────────

    private DataGrid BuildDataGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns     = false,
            CanUserAddRows          = false,
            CanUserDeleteRows       = false,
            IsReadOnly              = true,
            SelectionMode           = DataGridSelectionMode.Single,
            EnableRowVirtualization = true,
            GridLinesVisibility     = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility       = DataGridHeadersVisibility.Column,
            BorderThickness         = new Thickness(0),
        };
        VirtualizingPanel.SetIsVirtualizing(grid, true);
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
        _grid = grid;
        grid.SetResourceReference(BackgroundProperty,                        "TE_Background");
        grid.SetResourceReference(ForegroundProperty,                        "TE_Foreground");
        grid.SetResourceReference(DataGrid.RowBackgroundProperty,            "TE_Background");
        grid.SetResourceReference(DataGrid.AlternatingRowBackgroundProperty, "Panel_ToolbarBrush");
        grid.ColumnHeaderStyle = BuildHeaderStyle();
        grid.CellStyle         = BuildCellStyle();
        grid.RowStyle          = BuildRowStyle();

        grid.Columns.Add(MakeStatusColumn());
        grid.Columns.Add(MakeTextCol("Offset",   e => $"0x{e.Run.Offset:X8}",   90, "Offset"));
        grid.Columns.Add(MakeTextCol("Encoding", e => e.Run.Encoding.ToString(), 80, "Encoding"));
        grid.Columns.Add(MakeTextCol("Value",    e => e.Run.Value,               0,  "Value"));
        grid.Columns.Add(MakeOldValueColumn());

        grid.MouseDoubleClick += OnGridDoubleClick;
        grid.KeyDown          += OnGridKeyDown;
        grid.Sorting          += OnGridSorting;
        grid.ContextMenu       = BuildContextMenu();
        return grid;
    }

    private static Style BuildHeaderStyle()
    {
        var s = new Style(typeof(DataGridColumnHeader));
        s.Setters.Add(new Setter(BackgroundProperty,      new DynamicResourceExtension("Panel_ToolbarBrush")));
        s.Setters.Add(new Setter(ForegroundProperty,      new DynamicResourceExtension("Panel_ToolbarForegroundBrush")));
        s.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        s.Setters.Add(new Setter(PaddingProperty,         new Thickness(6, 3, 6, 3)));
        s.Setters.Add(new Setter(FontSizeProperty,        11d));
        return s;
    }

    private static Style BuildCellStyle()
    {
        var s = new Style(typeof(DataGridCell));
        // Transparent so the row-level background (TE_Background / tint) shows through.
        s.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
        s.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0)));
        s.Setters.Add(new Setter(ForegroundProperty, new DynamicResourceExtension("TE_Foreground")));
        return s;
    }

    /// <summary>Row background tinted per diff status via DataTriggers.</summary>
    private static Style BuildRowStyle()
    {
        var s = new Style(typeof(DataGridRow));
        // Themed foreground for all rows — ensures text is readable in both Light/Dark themes.
        s.Setters.Add(new Setter(DataGridRow.ForegroundProperty, new DynamicResourceExtension("TE_Foreground")));
        s.Triggers.Add(MakeRowTrigger(StringDiffStatus.Added,    DiffStatusBrushConverter.AddedRowBrush));
        s.Triggers.Add(MakeRowTrigger(StringDiffStatus.Removed,  DiffStatusBrushConverter.RemovedRowBrush));
        s.Triggers.Add(MakeRowTrigger(StringDiffStatus.Modified, DiffStatusBrushConverter.ModifiedRowBrush));
        return s;
    }

    private static DataTrigger MakeRowTrigger(StringDiffStatus status, Brush brush)
    {
        var trigger = new DataTrigger
        {
            Binding = new Binding(nameof(StringDiffEntry.Status)),
            Value   = status,
        };
        trigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, brush));
        return trigger;
    }

    // ── Status column — badge with background from converter ──────────────────

    private static DataGridTemplateColumn MakeStatusColumn()
    {
        var col = new DataGridTemplateColumn
        {
            Header          = "Status",
            Width           = 80,
            SortMemberPath  = nameof(StringDiffEntry.Status),
            CanUserSort     = true,
        };

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty,        new CornerRadius(3));
        border.SetValue(Border.PaddingProperty,             new Thickness(4, 1, 4, 1));
        border.SetValue(Border.MarginProperty,              new Thickness(2, 1, 2, 1));
        border.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        border.SetValue(Border.VerticalAlignmentProperty,   VerticalAlignment.Center);
        border.SetBinding(Border.BackgroundProperty,
            new Binding(nameof(StringDiffEntry.Status)) { Converter = DiffStatusBrushConverter.Instance });

        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetValue(TextBlock.FontSizeProperty,   9d);
        label.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        label.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        label.SetBinding(TextBlock.TextProperty,
            new Binding(nameof(StringDiffEntry.Status)) { Converter = DiffStatusLabelConverter.Instance });

        border.AppendChild(label);
        col.CellTemplate = new DataTemplate { VisualTree = border };
        return col;
    }

    // ── Text column ───────────────────────────────────────────────────────────

    private DataGridTemplateColumn MakeTextCol(string header, Func<StringDiffEntry, string> selector,
                                               double width, string sortMemberPath)
    {
        var col = new DataGridTemplateColumn
        {
            Header         = header,
            Width          = width > 0 ? new DataGridLength(width) : DataGridLength.Auto,
            SortMemberPath = sortMemberPath,
            CanUserSort    = true,
        };

        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetValue(TextBlock.FontSizeProperty,          11d);
        tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        // Themed foreground — reads TE_Foreground from the active theme at render time.
        tb.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");
        tb.SetBinding(TextBlock.TextProperty,
            new Binding { Converter = new DiffTextConverter(selector) });

        col.CellTemplate = new DataTemplate { VisualTree = tb };
        return col;
    }

    // ── Old Value column — italic placeholder for Added/Removed ──────────────

    private DataGridTemplateColumn MakeOldValueColumn()
    {
        var col = new DataGridTemplateColumn
        {
            Header         = "Old Value",
            Width          = DataGridLength.Auto,
            SortMemberPath = nameof(StringDiffEntry.OldValue),
            CanUserSort    = true,
        };

        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetValue(TextBlock.FontSizeProperty,          11d);
        tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        tb.SetBinding(TextBlock.TextProperty,
            new Binding(nameof(StringDiffEntry.OldValue)) { Converter = DiffOldValueConverter.Instance });
        tb.SetBinding(TextBlock.FontStyleProperty,
            new Binding(nameof(StringDiffEntry.OldValue)) { Converter = DiffOldValueStyleConverter.Instance });
        tb.SetBinding(TextBlock.ForegroundProperty,
            new Binding(nameof(StringDiffEntry.OldValue)) { Converter = new DiffOldValueForegroundConverter() });

        col.CellTemplate = new DataTemplate { VisualTree = tb };
        return col;
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var goToItem = new MenuItem { Header = "Go to offset in Hex Editor" };
        goToItem.Click += (_, _) =>
        {
            if (_grid.SelectedItem is StringDiffEntry entry)
                _vm.NavigateToOffset(entry.Run);
        };

        var copyValueItem = new MenuItem { Header = "Copy Value" };
        copyValueItem.Click += (_, _) =>
        {
            if (_grid.SelectedItem is StringDiffEntry entry)
                Clipboard.SetText(entry.Run.Value);
        };

        var copyOldItem = new MenuItem { Header = "Copy Old Value" };
        copyOldItem.Click += (_, _) =>
        {
            if (_grid.SelectedItem is StringDiffEntry { OldValue: { Length: > 0 } oldVal })
                Clipboard.SetText(oldVal);
        };

        var copyOffsetItem = new MenuItem { Header = "Copy Offset (hex)" };
        copyOffsetItem.Click += (_, _) =>
        {
            if (_grid.SelectedItem is StringDiffEntry entry)
                Clipboard.SetText($"0x{entry.Run.Offset:X8}");
        };

        var exportItem = new MenuItem { Header = "Export visible rows to CSV…" };
        exportItem.Click += (_, _) => ExportCsv();

        menu.Items.Add(goToItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(copyValueItem);
        menu.Items.Add(copyOldItem);
        menu.Items.Add(copyOffsetItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exportItem);

        menu.Opened += (_, _) =>
        {
            var entry    = _grid.SelectedItem as StringDiffEntry;
            var hasEntry = entry is not null;
            goToItem.IsEnabled       = hasEntry;
            copyValueItem.IsEnabled  = hasEntry;
            copyOldItem.IsEnabled    = entry?.OldValue is { Length: > 0 };
            copyOffsetItem.IsEnabled = hasEntry;
            exportItem.IsEnabled     = _lastDiffResult.Count > 0;
        };

        return menu;
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_grid.SelectedItem is StringDiffEntry entry)
            _vm.NavigateToOffset(entry.Run);
    }

    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _grid.SelectedItem is StringDiffEntry entry)
        {
            _vm.NavigateToOffset(entry.Run);
            e.Handled = true;
        }
    }

    // DataGridTemplateColumn does not participate in AutoSort — handle it manually.
    // Uses ListCollectionView.CustomSort with a DiffEntryComparer keyed by column tag.
    private void OnGridSorting(object sender, DataGridSortingEventArgs e)
    {
        if (_collectionView is null || e.Column.SortMemberPath is not { Length: > 0 } path)
            return;

        e.Handled = true;   // prevent DataGrid's default (no-op for TemplateColumns)

        var nextDir = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        e.Column.SortDirection = nextDir;

        // Clear sort indicators on all other columns (single-column sort).
        foreach (var col in _grid.Columns)
            if (!ReferenceEquals(col, e.Column)) col.SortDirection = null;

        _collectionView.CustomSort = new DiffEntryComparer(path, nextDir);
    }

    // ── Legend with live counters ─────────────────────────────────────────────

    private UIElement BuildLegend()
    {
        var panel = new WrapPanel { Margin = new Thickness(4, 2, 4, 3) };
        panel.SetResourceReference(BackgroundProperty, "Panel_ToolbarBrush");

        _addedCount    = new TextBlock { FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
        _removedCount  = new TextBlock { FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
        _modifiedCount = new TextBlock { FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
        _unchangedCount = new TextBlock { FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };

        panel.Children.Add(MakeLegendChip(_addedCount,    StringDiffStatus.Added));
        panel.Children.Add(MakeLegendChip(_removedCount,  StringDiffStatus.Removed));
        panel.Children.Add(MakeLegendChip(_modifiedCount, StringDiffStatus.Modified));
        panel.Children.Add(MakeLegendChip(_unchangedCount,StringDiffStatus.Unchanged));
        return panel;
    }

    private static Border MakeLegendChip(TextBlock countLabel, StringDiffStatus status) =>
        new()
        {
            Background   = DiffStatusBrushConverter.StatusBrush(status),
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(6, 1, 6, 1),
            Margin       = new Thickness(0, 0, 4, 0),
            Child        = countLabel,
        };

    private void UpdateLegendCounters()
    {
        int added = 0, removed = 0, modified = 0, unchanged = 0;
        foreach (var e in _lastDiffResult)
        {
            switch (e.Status)
            {
                case StringDiffStatus.Added:    added++;    break;
                case StringDiffStatus.Removed:  removed++;  break;
                case StringDiffStatus.Modified: modified++; break;
                default:                        unchanged++; break;
            }
        }

        _addedCount.Text    = $"Added {added:N0}";
        _removedCount.Text  = $"Removed {removed:N0}";
        _modifiedCount.Text = $"Modified {modified:N0}";
        _unchangedCount.Text = $"Unchanged {unchanged:N0}";

        _summaryText.Text = $"|  +{added}  -{removed}  ~{modified}  ={unchanged}";
    }

    // ── Diff logic ────────────────────────────────────────────────────────────

    private void RunDiff()
    {
        var a = _snapACombo.SelectedItem as StringExtractionViewModel.ScanSnapshot;
        var b = _snapBCombo.SelectedItem as StringExtractionViewModel.ScanSnapshot;
        if (a is null || b is null) { _summaryText.Text = "Select two snapshots."; return; }

        _lastDiffResult = StringDiffService.Compare(a.Runs, b.Runs);
        UpdateLegendCounters();
        RebuildCollectionView();
    }

    private void RebuildCollectionView()
    {
        var obs = new ObservableCollection<StringDiffEntry>(_lastDiffResult);
        _collectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(obs);
        _collectionView.Filter = FilterEntry;
        _grid.ItemsSource = _collectionView;
    }

    private void ApplyFilter() => _collectionView?.Refresh();

    private bool FilterEntry(object item)
    {
        if (item is not StringDiffEntry e) return false;
        if (!_activeFilters.Contains(e.Status)) return false;

        // Text search: match against value, old value, offset, or encoding.
        if (_searchTerm.Length > 0)
        {
            bool matchValue    = e.Run.Value.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase);
            bool matchOld      = e.OldValue?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) == true;
            bool matchOffset   = $"0x{e.Run.Offset:X8}".Contains(_searchTerm, StringComparison.OrdinalIgnoreCase);
            bool matchEncoding = e.Run.Encoding.ToString().Contains(_searchTerm, StringComparison.OrdinalIgnoreCase);
            if (!matchValue && !matchOld && !matchOffset && !matchEncoding)
                return false;
        }

        return true;
    }

    // ── CSV export ────────────────────────────────────────────────────────────

    private void ExportCsv()
    {
        if (_collectionView is null || _collectionView.Count == 0) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Export Diff to CSV",
            Filter           = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt       = ".csv",
            FileName         = $"StringDiff_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Status,Offset,Encoding,Value,OldValue");

        foreach (var item in _collectionView)
        {
            if (item is not StringDiffEntry e) continue;
            sb.Append(DiffStatusBrushConverter.StatusLabel(e.Status)).Append(',');
            sb.Append($"0x{e.Run.Offset:X8}").Append(',');
            sb.Append(CsvEscape(e.Run.Encoding.ToString())).Append(',');
            sb.Append(CsvEscape(e.Run.Value)).Append(',');
            sb.AppendLine(CsvEscape(e.OldValue ?? string.Empty));
        }

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
    }

    private static string CsvEscape(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return '"' + s.Replace("\"", "\"\"") + '"';
        return s;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ComboBox MakeSnapshotCombo()
    {
        var cb = new ComboBox
        {
            Width             = 240,
            Height            = 22,
            FontSize          = 10,
            Margin            = new Thickness(0, 0, 4, 0),
            DisplayMemberPath = nameof(StringExtractionViewModel.ScanSnapshot.DisplayName),
        };
        cb.SetResourceReference(BackgroundProperty, "TE_Background");
        cb.SetResourceReference(ForegroundProperty, "TE_Foreground");
        cb.SetBinding(ItemsControl.ItemsSourceProperty,
            new Binding(nameof(_vm.Snapshots)) { Source = _vm });
        return cb;
    }

    private static TextBlock MakeLabel(string text)
    {
        var tb = new TextBlock
        {
            Text              = text,
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(4, 0, 4, 0),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarForegroundBrush");
        return tb;
    }
}

// ── Sort comparer ─────────────────────────────────────────────────────────────

/// <summary>
/// IComparer for <see cref="StringDiffEntry"/> rows. Supports sorting by flat column keys:
/// "Status", "Offset", "Encoding", "Value", "OldValue".
/// </summary>
internal sealed class DiffEntryComparer(string key, System.ComponentModel.ListSortDirection dir) : System.Collections.IComparer
{
    public int Compare(object? x, object? y)
    {
        if (x is not StringDiffEntry a || y is not StringDiffEntry b) return 0;
        int cmp = key switch
        {
            "Status"   => a.Status.CompareTo(b.Status),
            "Offset"   => a.Run.Offset.CompareTo(b.Run.Offset),
            "Encoding" => string.Compare(a.Run.Encoding.ToString(), b.Run.Encoding.ToString(), StringComparison.Ordinal),
            "Value"    => string.Compare(a.Run.Value, b.Run.Value, StringComparison.OrdinalIgnoreCase),
            "OldValue" => string.Compare(a.OldValue ?? string.Empty, b.OldValue ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            _          => 0,
        };
        return dir == System.ComponentModel.ListSortDirection.Descending ? -cmp : cmp;
    }
}

// ── Old Value foreground dim converter ────────────────────────────────────────

/// <summary>Dims the foreground for the "—" placeholder in Old Value column.</summary>
internal sealed class DiffOldValueForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush DimBrush = FreezeDim();
    private static SolidColorBrush FreezeDim()
    {
        var b = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        b.Freeze();
        return b;
    }

    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is string s && s.Length > 0 ? DependencyProperty.UnsetValue : DimBrush;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}
