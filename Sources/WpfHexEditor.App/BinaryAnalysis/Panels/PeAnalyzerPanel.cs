//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis.Panels;

/// <summary>#114 PE Import/Export Table Analyzer — code-behind-only panel.</summary>
public sealed class PeAnalyzerPanel : UserControl
{
    private readonly PeAnalyzerViewModel _vm = new();

    public PeAnalyzerPanel()
    {
        _vm.RequestJumpToOffset = JumpToOffset;

        // --- Toolbar -------------------------------------------------------
        var scanBtn    = new Button { Content = "Analyse PE", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 4, 0) };
        var cancelBtn  = new Button { Content = "Cancel",     Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 8, 0) };
        var filterBox  = new TextBox { Width = 160, Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        filterBox.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.FilterText)) { Source = _vm, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        var archText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 0) };
        archText.SetBinding(TextBlock.TextProperty, new Binding(nameof(_vm.Architecture)) { Source = _vm });

        var statusText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        statusText.SetBinding(TextBlock.TextProperty, new Binding(nameof(_vm.StatusText)) { Source = _vm });

        scanBtn.SetBinding(Button.CommandProperty, new Binding(nameof(_vm.ScanCommand)) { Source = _vm });
        cancelBtn.SetBinding(Button.CommandProperty, new Binding(nameof(_vm.CancelCommand)) { Source = _vm });

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        toolbar.Children.Add(scanBtn);
        toolbar.Children.Add(cancelBtn);
        toolbar.Children.Add(new TextBlock { Text = "Filter:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        toolbar.Children.Add(filterBox);
        toolbar.Children.Add(archText);
        toolbar.Children.Add(statusText);

        // --- Tabs ----------------------------------------------------------
        var tabs = new TabControl();
        tabs.Items.Add(BuildImportsTab());
        tabs.Items.Add(BuildExportsTab());
        tabs.Items.Add(BuildSectionsTab());
        tabs.Items.Add(BuildHeadersTab());

        // --- Not-PE banner -------------------------------------------------
        var noPeBanner = new TextBlock
        {
            Text               = "Not a PE file — open a Windows executable or DLL first.",
            HorizontalAlignment= HorizontalAlignment.Center,
            VerticalAlignment  = VerticalAlignment.Center,
            Foreground         = Brushes.Gray,
            FontSize           = 13
        };
        noPeBanner.SetBinding(VisibilityProperty, new Binding(nameof(_vm.IsPeFile))
        {
            Source    = _vm,
            Converter = new BoolToVisConverter(invert: true)
        });
        tabs.SetBinding(VisibilityProperty, new Binding(nameof(_vm.IsPeFile))
        {
            Source    = _vm,
            Converter = new BoolToVisConverter(invert: false)
        });

        var body = new Grid();
        body.Children.Add(tabs);
        body.Children.Add(noPeBanner);

        // --- Root ----------------------------------------------------------
        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(body);
        Content = root;
    }

    // -- Context wiring -----------------------------------------------------

    public void SetContext(IIDEHostContext context) => _vm.SetContext(context);

    public void OnFileOpened()
    {
        _vm.OnFileOpened();
        _ = _vm.ScanAsync();
    }

    // -- Jump to offset in HexEditor ----------------------------------------

    private void JumpToOffset(long offset)
    {
        if (_vm.Context?.HexEditor is not { IsActive: true } hex) return;
        hex.NavigateTo(offset);
    }

    // -- Tab builders -------------------------------------------------------

    private TabItem BuildImportsTab()
    {
        // TreeView: DLL nodes → function leaves
        var tree = new TreeView();
        tree.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(_vm.FilteredImports)) { Source = _vm });

        var dllTemplate = new HierarchicalDataTemplate(typeof(ImportModule))
        {
            ItemsSource = new Binding(nameof(ImportModule.Functions))
        };

        var dllStack = new FrameworkElementFactory(typeof(StackPanel));
        dllStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var dllFlag = new FrameworkElementFactory(typeof(TextBlock));
        dllFlag.SetBinding(TextBlock.TextProperty, new Binding(".") { Converter = new SuspectFlagConverter() });
        dllFlag.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 4, 0));
        dllStack.AppendChild(dllFlag);

        var dllName = new FrameworkElementFactory(typeof(TextBlock));
        dllName.SetBinding(TextBlock.TextProperty, new Binding(nameof(ImportModule.Dll)));
        dllName.SetBinding(TextBlock.ForegroundProperty, new Binding(".") { Converter = new SuspectColorConverter() });
        dllStack.AppendChild(dllName);

        dllTemplate.VisualTree = dllStack;

        // Leaf template: function entry
        var funcTemplate = new DataTemplate(typeof(ImportEntry));
        var funcGrid     = new FrameworkElementFactory(typeof(Grid));
        AddGridColumn(funcGrid, 180); // Name
        AddGridColumn(funcGrid, 60);  // Ordinal
        AddGridColumn(funcGrid, 90);  // RVA
        AddGridColumn(funcGrid, 90);  // FileOffset

        AppendGridCell(funcGrid, nameof(ImportEntry.Name),       0);
        AppendGridCell(funcGrid, nameof(ImportEntry.Ordinal),    1, fmt: "{0}");
        AppendGridCell(funcGrid, nameof(ImportEntry.Rva),        2, fmt: "0x{0:X8}");
        AppendGridCell(funcGrid, nameof(ImportEntry.FileOffset), 3, fmt: "0x{0:X8}");
        funcTemplate.VisualTree = funcGrid;

        dllTemplate.ItemTemplate = funcTemplate;
        tree.ItemTemplate        = dllTemplate;

        // Double-click → jump
        tree.MouseDoubleClick += (_, _) =>
        {
            if (tree.SelectedItem is ImportEntry ie) JumpToOffset(ie.FileOffset);
        };

        // Context menu
        tree.ContextMenu = BuildImportContextMenu(tree);

        return new TabItem { Header = "Imports", Content = tree };
    }

    private TabItem BuildExportsTab()
    {
        var grid = BuildVirtualDataGrid();
        grid.Columns.Add(new DataGridTextColumn { Header = "Name",       Binding = new Binding(nameof(ExportEntry.Name)),       Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Ordinal",    Binding = new Binding(nameof(ExportEntry.Ordinal)),    Width = 70 });
        grid.Columns.Add(new DataGridTextColumn { Header = "RVA",        Binding = new Binding(nameof(ExportEntry.Rva))         { StringFormat = "0x{0:X8}" }, Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "FileOffset", Binding = new Binding(nameof(ExportEntry.FileOffset))  { StringFormat = "0x{0:X8}" }, Width = 100 });
        grid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(_vm.FilteredExports)) { Source = _vm });
        grid.MouseDoubleClick += (_, _) =>
        {
            if (grid.SelectedItem is ExportEntry ee) JumpToOffset(ee.FileOffset);
        };
        grid.ContextMenu = BuildCopyContextMenu(grid,
            ("Copy Name",       r => ((ExportEntry)r).Name),
            ("Copy RVA",        r => $"0x{((ExportEntry)r).Rva:X8}"),
            ("Copy FileOffset", r => $"0x{((ExportEntry)r).FileOffset:X8}"),
            ("Jump to Offset",  r => { JumpToOffset(((ExportEntry)r).FileOffset); return null; }));
        return new TabItem { Header = "Exports", Content = grid };
    }

    private TabItem BuildSectionsTab()
    {
        var grid = BuildVirtualDataGrid();
        grid.Columns.Add(new DataGridTextColumn { Header = "Name",    Binding = new Binding(nameof(PeSection.Name)),           Width = 70 });
        grid.Columns.Add(new DataGridTextColumn { Header = "VirtAddr",Binding = new Binding(nameof(PeSection.VirtualAddress))  { StringFormat = "0x{0:X8}" }, Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "RawOffset",Binding = new Binding(nameof(PeSection.RawOffset))      { StringFormat = "0x{0:X8}" }, Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Size",    Binding = new Binding(nameof(PeSection.Size))            { StringFormat = "{0:N0}" },   Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Flags",   Binding = new Binding(nameof(PeSection.Characteristics)) { Converter = new SectionFlagsConverter() }, Width = 80 });
        grid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(_vm.Sections)) { Source = _vm });
        grid.MouseDoubleClick += (_, _) =>
        {
            if (grid.SelectedItem is PeSection sec) JumpToOffset(sec.RawOffset);
        };
        return new TabItem { Header = "Sections", Content = grid };
    }

    private TabItem BuildHeadersTab()
    {
        var grid = BuildVirtualDataGrid();
        grid.Columns.Add(new DataGridTextColumn { Header = "Field", Binding = new Binding(nameof(HeaderRow.Key)),   Width = 180 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(nameof(HeaderRow.Value)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(_vm.Headers)) { Source = _vm });
        return new TabItem { Header = "Headers", Content = grid };
    }

    // -- Helpers ------------------------------------------------------------

    private static DataGrid BuildVirtualDataGrid()
    {
        var g = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows      = false,
            CanUserDeleteRows   = false,
            IsReadOnly          = true,
        };
        VirtualizingPanel.SetIsVirtualizing(g, true);
        return g;
    }

    private static void AddGridColumn(FrameworkElementFactory grid, double width)
    {
        var col = new FrameworkElementFactory(typeof(ColumnDefinition));
        col.SetValue(ColumnDefinition.WidthProperty, new GridLength(width));
        grid.AppendChild(col);
    }

    private static void AppendGridCell(FrameworkElementFactory grid, string bindingPath, int col,
        string? fmt = null)
    {
        var tb = new FrameworkElementFactory(typeof(TextBlock));
        var b  = new Binding(bindingPath);
        if (fmt != null) b.StringFormat = fmt;
        tb.SetBinding(TextBlock.TextProperty, b);
        tb.SetValue(Grid.ColumnProperty, col);
        tb.SetValue(TextBlock.MarginProperty, new Thickness(2, 0, 8, 0));
        grid.AppendChild(tb);
    }

    private ContextMenu BuildImportContextMenu(TreeView tree) =>
        BuildCopyContextMenu(tree,
            ("Copy Function Name", r => (r as ImportEntry)?.Name ?? (r as ImportModule)?.Dll ?? ""),
            ("Copy RVA",           r => r is ImportEntry ie ? $"0x{ie.Rva:X8}" : ""),
            ("Copy FileOffset",    r => r is ImportEntry ie2 ? $"0x{ie2.FileOffset:X8}" : ""),
            ("Jump to Offset",     r => { if (r is ImportEntry ie3) JumpToOffset(ie3.FileOffset); return null; }));

    private static ContextMenu BuildCopyContextMenu(Control ctrl, params (string Header, Func<object, string?> Action)[] items)
    {
        var menu = new ContextMenu();
        foreach (var (header, action) in items)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) =>
            {
                var selected = ctrl switch
                {
                    DataGrid dg   => dg.SelectedItem,
                    TreeView tv   => tv.SelectedItem,
                    _             => null
                };
                if (selected is null) return;
                var result = action(selected);
                if (result != null) Clipboard.SetText(result);
            };
            menu.Items.Add(item);
        }
        return menu;
    }
}

// -- Value converters (file-scoped, no public API) --------------------------

file sealed class BoolToVisConverter(bool invert) : System.Windows.Data.IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        (v is true) ^ invert ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        throw new NotSupportedException();
}

file sealed class SuspectFlagConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        v is ImportModule m && PeFileAnalyzer.IsSuspectModule(m) ? "⚠" : "";
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        throw new NotSupportedException();
}

file sealed class SuspectColorConverter : System.Windows.Data.IValueConverter
{
    private static readonly Brush Normal  = SystemColors.ControlTextBrush;
    private static readonly Brush Suspect = new SolidColorBrush(Color.FromRgb(0xFF, 0x59, 0x5E));
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        v is ImportModule m && PeFileAnalyzer.IsSuspectModule(m) ? Suspect : Normal;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        throw new NotSupportedException();
}

file sealed class SectionFlagsConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c)
    {
        if (v is not uint chars) return "";
        var sb = new System.Text.StringBuilder(4);
        if ((chars & 0x4000_0000) != 0) sb.Append('R');
        if ((chars & 0x8000_0000) != 0) sb.Append('W');
        if ((chars & 0x2000_0000) != 0) sb.Append('X');
        return sb.ToString();
    }
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        throw new NotSupportedException();
}
