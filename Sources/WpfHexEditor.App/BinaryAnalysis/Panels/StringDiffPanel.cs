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

using System.Collections.ObjectModel;
using System.Globalization;
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

    // Filter state — bitmask mirrors StringDiffStatus enum values
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

    // CollectionView wrapping the DataGrid ItemsSource for in-place filter
    private ListCollectionView? _collectionView;

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
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.SetResourceReference(BackgroundProperty, "TE_Background");

        var snapshotBar = BuildSnapshotBar();
        var filterBar   = BuildFilterBar();
        var grid        = BuildDataGrid();
        var legend      = BuildLegend();

        Grid.SetRow(snapshotBar, 0);
        Grid.SetRow(filterBar,   1);
        Grid.SetRow(grid,        2);
        Grid.SetRow(legend,      3);
        root.Children.Add(snapshotBar);
        root.Children.Add(filterBar);
        root.Children.Add(grid);
        root.Children.Add(legend);
        return root;
    }

    // ── Snapshot selection bar ─────────────────────────────────────────────────

    private UIElement BuildSnapshotBar()
    {
        var toolbar = new Grid { Margin = new Thickness(4, 4, 4, 2) };
        toolbar.SetResourceReference(BackgroundProperty, "Panel_ToolbarBrush");
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) }); // separator
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _snapACombo = MakeSnapshotCombo();
        _snapBCombo = MakeSnapshotCombo();
        _snapACombo.SelectionChanged += OnSnapshotSelectionChanged;
        _snapBCombo.SelectionChanged += OnSnapshotSelectionChanged;

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

        _summaryText = new TextBlock
        {
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0),
        };
        _summaryText.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        Grid.SetColumn(MakeLabel("Snapshot A:"), 0); toolbar.Children.Add(MakeLabel("Snapshot A:"));
        Grid.SetColumn(_snapACombo,              1); toolbar.Children.Add(_snapACombo);
        Grid.SetColumn(sep,                      3); toolbar.Children.Add(sep);
        Grid.SetColumn(MakeLabel("Snapshot B:"), 4);
        var bLabel = MakeLabel("Snapshot B:");
        Grid.SetColumn(bLabel, 4); toolbar.Children.Add(bLabel);
        Grid.SetColumn(_snapBCombo,   5); toolbar.Children.Add(_snapBCombo);
        Grid.SetColumn(compareBtn,    6); toolbar.Children.Add(compareBtn);
        Grid.SetColumn(_summaryText,  7); toolbar.Children.Add(_summaryText);

        return toolbar;
    }

    private void OnSnapshotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Auto-compare as soon as both snapshots are selected.
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
        grid.SetResourceReference(BackgroundProperty, "TE_Background");
        grid.SetResourceReference(ForegroundProperty, "TE_Foreground");
        grid.ColumnHeaderStyle = BuildHeaderStyle();
        grid.RowStyle          = BuildRowStyle();

        grid.Columns.Add(MakeStatusColumn());
        grid.Columns.Add(MakeTextCol("Offset",    e => $"0x{e.Run.Offset:X8}", 90));
        grid.Columns.Add(MakeTextCol("Encoding",  e => e.Run.Encoding.ToString(), 80));
        grid.Columns.Add(MakeTextCol("Value",     e => e.Run.Value, 0));
        grid.Columns.Add(MakeOldValueColumn());

        grid.MouseDoubleClick += OnGridDoubleClick;
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

    /// <summary>Row background tinted per diff status via DataTriggers.</summary>
    private static Style BuildRowStyle()
    {
        var s = new Style(typeof(DataGridRow));
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

    private static DataGridTemplateColumn MakeTextCol(string header, Func<StringDiffEntry, string> selector, double width)
    {
        var col = new DataGridTemplateColumn
        {
            Header = new TextBlock { Text = header },
            Width  = width > 0 ? new DataGridLength(width) : DataGridLength.Auto,
        };

        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetValue(TextBlock.FontSizeProperty,          11d);
        tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        tb.SetBinding(TextBlock.TextProperty,
            new Binding { Converter = new DiffTextConverter(selector) });

        col.CellTemplate = new DataTemplate { VisualTree = tb };
        return col;
    }

    // ── Old Value column — italic placeholder for Added/Removed ──────────────

    private static DataGridTemplateColumn MakeOldValueColumn()
    {
        var col = new DataGridTemplateColumn
        {
            Header = new TextBlock { Text = "Old Value" },
            Width  = DataGridLength.Auto,
        };

        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetValue(TextBlock.FontSizeProperty,          11d);
        tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        // Text: "—" when null/empty, actual value otherwise
        tb.SetBinding(TextBlock.TextProperty,
            new Binding(nameof(StringDiffEntry.OldValue)) { Converter = DiffOldValueConverter.Instance });
        // FontStyle: Italic for placeholder "—", Normal for real values
        tb.SetBinding(TextBlock.FontStyleProperty,
            new Binding(nameof(StringDiffEntry.OldValue)) { Converter = DiffOldValueStyleConverter.Instance });
        // Foreground: dim for placeholder
        var fgConverter = new DiffOldValueForegroundConverter();
        tb.SetBinding(TextBlock.ForegroundProperty,
            new Binding(nameof(StringDiffEntry.OldValue)) { Converter = fgConverter });

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

        menu.Items.Add(goToItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(copyValueItem);
        menu.Items.Add(copyOldItem);
        menu.Items.Add(copyOffsetItem);

        // Enable/disable items based on selection before opening.
        menu.Opened += (_, _) =>
        {
            var entry    = _grid.SelectedItem as StringDiffEntry;
            var hasEntry = entry is not null;
            goToItem.IsEnabled      = hasEntry;
            copyValueItem.IsEnabled = hasEntry;
            copyOldItem.IsEnabled   = entry?.OldValue is { Length: > 0 };
            copyOffsetItem.IsEnabled = hasEntry;
        };

        return menu;
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_grid.SelectedItem is StringDiffEntry entry)
            _vm.NavigateToOffset(entry.Run);
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

        _summaryText.Text = $"+{added}  -{removed}  ~{modified}  ={unchanged}";
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

    private void ApplyFilter()
    {
        _collectionView?.Refresh();
    }

    private bool FilterEntry(object item) =>
        item is StringDiffEntry e && _activeFilters.Contains(e.Status);

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
