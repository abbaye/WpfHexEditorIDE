// Project     : WpfHexEditor.App
// File        : StringDiffPanel.cs
// Description : Diff panel — pick two snapshots, compare, view Added/Removed/Modified/Unchanged.
// Architecture: Code-behind UserControl; delegates diff logic to StringDiffService.

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;

namespace WpfHexEditor.App.BinaryAnalysis.Panels;

internal sealed class StringDiffPanel : UserControl
{
    private static readonly SolidColorBrush AddedBrush    = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)));
    private static readonly SolidColorBrush RemovedBrush  = Freeze(new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)));
    private static readonly SolidColorBrush ModifiedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00)));
    private static readonly SolidColorBrush NeutralBrush  = Freeze(new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    private readonly StringExtractionViewModel _vm;
    private readonly ObservableCollection<StringDiffEntry> _results = [];
    private ComboBox _snapACombo = null!;
    private ComboBox _snapBCombo = null!;
    private TextBlock _summaryText = null!;

    public StringDiffPanel(StringExtractionViewModel vm)
    {
        _vm = vm;
        Content = BuildLayout();
    }

    private UIElement BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.SetResourceReference(BackgroundProperty, "TE_Background");

        // ── Toolbar ──────────────────────────────────────────────────────────
        var toolbar = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(4, 4, 4, 4),
        };

        var lblA = MakeLabel("Snapshot A:");
        _snapACombo = MakeSnapshotCombo();

        var lblB = MakeLabel("Snapshot B:");
        _snapBCombo = MakeSnapshotCombo();

        var compareBtn = new Button
        {
            Content = "Compare", Padding = new Thickness(10, 3, 10, 3),
            Margin  = new Thickness(8, 0, 0, 0), FontSize = 11,
        };
        compareBtn.SetResourceReference(ForegroundProperty, "TE_Foreground");
        compareBtn.Click += (_, _) => RunDiff();

        _summaryText = new TextBlock
        {
            FontSize = 10, VerticalAlignment = VerticalAlignment.Center,
            Margin   = new Thickness(12, 0, 0, 0),
        };
        _summaryText.SetResourceReference(ForegroundProperty, "Panel_ToolbarForegroundBrush");

        toolbar.Children.Add(lblA);
        toolbar.Children.Add(_snapACombo);
        toolbar.Children.Add(lblB);
        toolbar.Children.Add(_snapBCombo);
        toolbar.Children.Add(compareBtn);
        toolbar.Children.Add(_summaryText);

        // ── DataGrid ─────────────────────────────────────────────────────────
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
            ItemsSource             = _results,
        };
        VirtualizingPanel.SetIsVirtualizing(grid, true);
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
        grid.SetResourceReference(BackgroundProperty, "TE_Background");
        grid.SetResourceReference(ForegroundProperty, "TE_Foreground");

        var hdrStyle = new Style(typeof(DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(BackgroundProperty,      new DynamicResourceExtension("Panel_ToolbarBrush")));
        hdrStyle.Setters.Add(new Setter(ForegroundProperty,      new DynamicResourceExtension("Panel_ToolbarForegroundBrush")));
        hdrStyle.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        hdrStyle.Setters.Add(new Setter(PaddingProperty,         new Thickness(6, 3, 6, 3)));
        hdrStyle.Setters.Add(new Setter(FontSizeProperty,        11d));
        grid.ColumnHeaderStyle = hdrStyle;

        grid.Columns.Add(MakeStatusColumn());
        grid.Columns.Add(MakeTextCol("Offset",   e => $"0x{e.Run.Offset:X8}", 90));
        grid.Columns.Add(MakeTextCol("Encoding", e => e.Run.Encoding.ToString(), 80));
        grid.Columns.Add(MakeTextCol("Value",    e => e.Run.Value, 0));
        grid.Columns.Add(MakeTextCol("Old Value", e => e.OldValue ?? string.Empty, 0));

        // ── Legend ────────────────────────────────────────────────────────────
        var legend = new WrapPanel { Margin = new Thickness(4, 2, 4, 2) };
        legend.Children.Add(MakeLegendChip("Added",     AddedBrush));
        legend.Children.Add(MakeLegendChip("Removed",   RemovedBrush));
        legend.Children.Add(MakeLegendChip("Modified",  ModifiedBrush));
        legend.Children.Add(MakeLegendChip("Unchanged", NeutralBrush));

        Grid.SetRow(toolbar, 0);
        Grid.SetRow(grid,    1);
        Grid.SetRow(legend,  2);
        root.Children.Add(toolbar);
        root.Children.Add(grid);
        root.Children.Add(legend);
        return root;
    }

    private ComboBox MakeSnapshotCombo()
    {
        var cb = new ComboBox
        {
            Width  = 220, Height = 22, FontSize = 11,
            Margin = new Thickness(0, 0, 8, 0),
        };
        cb.SetResourceReference(ForegroundProperty, "TE_Foreground");
        cb.SetBinding(ItemsControl.ItemsSourceProperty,   new Binding(nameof(_vm.Snapshots)) { Source = _vm });
        cb.DisplayMemberPath = nameof(StringExtractionViewModel.ScanSnapshot.DisplayName);
        return cb;
    }

    private void RunDiff()
    {
        var a = _snapACombo.SelectedItem as StringExtractionViewModel.ScanSnapshot;
        var b = _snapBCombo.SelectedItem as StringExtractionViewModel.ScanSnapshot;
        if (a is null || b is null) { _summaryText.Text = "Select two snapshots."; return; }

        var entries = StringDiffService.Compare(a.Runs, b.Runs);
        _results.Clear();
        foreach (var e in entries) _results.Add(e);

        int added    = entries.Count(e => e.Status == StringDiffStatus.Added);
        int removed  = entries.Count(e => e.Status == StringDiffStatus.Removed);
        int modified = entries.Count(e => e.Status == StringDiffStatus.Modified);
        _summaryText.Text = $"+{added}  -{removed}  ~{modified}  ={entries.Count - added - removed - modified}";
    }

    private DataGridTemplateColumn MakeStatusColumn()
    {
        var col = new DataGridTemplateColumn { Header = "Status", Width = 80, CanUserSort = false };
        var tpl = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        factory.SetValue(Border.PaddingProperty,      new Thickness(4, 1, 4, 1));
        factory.SetValue(Border.MarginProperty,       new Thickness(2, 1, 2, 1));
        factory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        factory.SetValue(Border.VerticalAlignmentProperty,   VerticalAlignment.Center);
        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetValue(TextBlock.FontSizeProperty, 9d);
        tb.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        tb.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        factory.AppendChild(tb);
        factory.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is not Border b) return;
            var label = (TextBlock)b.Child;
            void Refresh(object? _, DependencyPropertyChangedEventArgs __)
            {
                if (b.DataContext is not StringDiffEntry e) return;
                (b.Background, label.Text) = e.Status switch
                {
                    StringDiffStatus.Added     => (AddedBrush,    "Added"),
                    StringDiffStatus.Removed   => (RemovedBrush,  "Removed"),
                    StringDiffStatus.Modified  => (ModifiedBrush, "Modified"),
                    _                          => (NeutralBrush,  "Unchanged"),
                };
            }
            b.DataContextChanged += Refresh;
            b.Unloaded += (_, _) => b.DataContextChanged -= Refresh;
            Refresh(null, default);
        }));
        tpl.VisualTree = factory;
        col.CellTemplate = tpl;
        return col;
    }

    private static DataGridTemplateColumn MakeTextCol(string header, Func<StringDiffEntry, string> selector, double width)
    {
        // Use a converter-free approach: bind via a template to avoid needing a dedicated property
        var col = new DataGridTemplateColumn
        {
            Header = new TextBlock { Text = header },
            Width  = width > 0 ? new DataGridLength(width) : DataGridLength.Auto,
        };
        var tpl = new DataTemplate();
        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetValue(TextBlock.FontSizeProperty, 11d);
        tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        tb.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is not TextBlock t) return;
            void Refresh(object? _, DependencyPropertyChangedEventArgs __)
                => t.Text = t.DataContext is StringDiffEntry e ? selector(e) : string.Empty;
            t.DataContextChanged += Refresh;
            t.Unloaded += (_, _) => t.DataContextChanged -= Refresh;
            Refresh(null, default);
        }));
        tpl.VisualTree = tb;
        col.CellTemplate = tpl;
        return col;
    }

    private static TextBlock MakeLabel(string text)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarForegroundBrush");
        return tb;
    }

    private static Border MakeLegendChip(string label, SolidColorBrush bg)
    {
        var b = new Border
        {
            Background   = bg,
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(6, 1, 6, 1),
            Margin       = new Thickness(0, 0, 4, 0),
            Child        = new TextBlock { Text = label, FontSize = 9, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold },
        };
        return b;
    }
}
