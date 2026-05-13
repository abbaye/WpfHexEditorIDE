// Project      : WpfHexEditor.App
// File         : HexDiff/Panels/HexDiffPanel.cs
// Description  : #B Hex Diff panel — code-behind-only UserControl.
// Architecture : Toolbar (file pickers + navigation) + DataGrid (diff records).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using WpfHexEditor.App.HexDiff.Models;
using WpfHexEditor.App.HexDiff.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.HexDiff.Panels;

public sealed class HexDiffPanel : UserControl
{
    private readonly HexDiffViewModel _vm = new();

    public HexDiffPanel()
    {
        var fileABox   = new TextBox { Width = 220, VerticalAlignment = VerticalAlignment.Center, IsReadOnly = true };
        var browseABtn = new Button  { Content = "…", Width = 24, Margin = new Thickness(2, 0, 4, 0) };
        var activeBtn  = new Button  { Content = "Active Editor", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 8, 0) };
        var fileBBox   = new TextBox { Width = 220, VerticalAlignment = VerticalAlignment.Center, IsReadOnly = true };
        var browseBBtn = new Button  { Content = "…", Width = 24, Margin = new Thickness(2, 0, 8, 0) };
        var compareBtn = new Button  { Content = "Compare", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0) };
        var prevBtn    = new Button  { Content = "◀ Prev",  Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) };
        var nextBtn    = new Button  { Content = "Next ▶",  Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 8, 0) };
        var exportBtn  = new Button  { Content = "Export Patch…", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 8, 0) };
        var statusTxt  = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

        var labelA = new TextBlock { Text = "A:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        var labelB = new TextBlock { Text = "B:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        toolbar.Children.Add(labelA);
        toolbar.Children.Add(fileABox);
        toolbar.Children.Add(browseABtn);
        toolbar.Children.Add(activeBtn);
        toolbar.Children.Add(labelB);
        toolbar.Children.Add(fileBBox);
        toolbar.Children.Add(browseBBtn);
        toolbar.Children.Add(compareBtn);
        toolbar.Children.Add(prevBtn);
        toolbar.Children.Add(nextBtn);
        toolbar.Children.Add(exportBtn);
        toolbar.Children.Add(statusTxt);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows      = false,
            CanUserDeleteRows   = false,
            IsReadOnly          = true,
            VirtualizingPanel.IsVirtualizing = true,
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Offset",   Binding = new Binding(nameof(DiffRecord.Offset))  { StringFormat = "{0:X8}" }, Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Kind",     Binding = new Binding(nameof(DiffRecord.Kind)),     Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "File A",   Binding = new Binding(nameof(DiffRecord.OldByte)) { StringFormat = "{0:X2}" }, Width = 60 });
        grid.Columns.Add(new DataGridTextColumn { Header = "File B",   Binding = new Binding(nameof(DiffRecord.NewByte)) { StringFormat = "{0:X2}" }, Width = 60 });
        grid.ItemsSource = _vm.Results;

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Triggers.Add(MakeKindTrigger(DiffKind.Substitution, Color.FromArgb(60, 255, 200, 0)));
        rowStyle.Triggers.Add(MakeKindTrigger(DiffKind.Insertion,    Color.FromArgb(60, 0,   200, 80)));
        rowStyle.Triggers.Add(MakeKindTrigger(DiffKind.Deletion,     Color.FromArgb(60, 255, 60,  60)));
        grid.RowStyle = rowStyle;

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(grid);
        Content = root;

        fileABox.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.FileAPath)) { Source = _vm });
        fileBBox.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.FileBPath)) { Source = _vm });
        statusTxt.SetBinding(TextBlock.TextProperty, new Binding(nameof(_vm.StatusText)) { Source = _vm });

        browseABtn.Click += (_, _) => { if (TryPickFile(out var p)) _vm.FileAPath = p; };
        browseBBtn.Click += (_, _) => { if (TryPickFile(out var p)) _vm.FileBPath = p; };
        activeBtn.Click  += (_, _) => _vm.UseActiveEditorAsFileA();
        compareBtn.Click += async (_, _) => await _vm.RunDiffAsync();
        prevBtn.Click    += (_, _) => { _vm.NavigatePrev(); grid.ScrollIntoView(_vm.CurrentDiff); };
        nextBtn.Click    += (_, _) => { _vm.NavigateNext(); grid.ScrollIntoView(_vm.CurrentDiff); };
        exportBtn.Click  += (_, _) => OnExport();
    }

    public void SetContext(IIDEHostContext context) => _vm.SetContext(context);
    public void OnFileOpened() => _vm.Results.Clear();

    private void OnExport()
    {
        var dlg = new SaveFileDialog
        {
            Filter     = "JSON patch (*.json)|*.json|Text patch (*.txt)|*.txt",
            DefaultExt = ".json",
            FileName   = "patch",
        };
        if (dlg.ShowDialog() == true) _vm.ExportPatch(dlg.FileName);
    }

    private static bool TryPickFile(out string path)
    {
        var dlg = new OpenFileDialog { Filter = "All files (*.*)|*.*" };
        if (dlg.ShowDialog() == true) { path = dlg.FileName; return true; }
        path = string.Empty;
        return false;
    }

    private static DataTrigger MakeKindTrigger(DiffKind kind, Color color)
    {
        var trigger = new DataTrigger
        {
            Binding = new Binding(nameof(DiffRecord.Kind)),
            Value   = kind,
        };
        trigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(color)));
        return trigger;
    }
}
