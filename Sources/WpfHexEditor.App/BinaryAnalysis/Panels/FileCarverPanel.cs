//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis.Panels;

/// <summary>#112 Embedded File Carver — code-behind-only panel.</summary>
public sealed class FileCarverPanel : UserControl
{
    private readonly FileCarverViewModel _vm;
    private IIDEHostContext? _context;

    public FileCarverPanel(FileCarverViewModel vm)
    {
        _vm = vm;

        var scanBtn      = new Button { Content = "Scan",       Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(10, 2, 10, 2) };
        var cancelBtn    = new Button { Content = "Cancel",     Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(10, 2, 10, 2) };
        var exportAllBtn = new Button { Content = "Export All", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 2, 10, 2) };
        var statusTxt    = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        statusTxt.SetBinding(TextBlock.TextProperty, new Binding(nameof(_vm.StatusText)) { Source = _vm });

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 4, 4, 4) };
        toolbar.Children.Add(scanBtn);
        toolbar.Children.Add(cancelBtn);
        toolbar.Children.Add(exportAllBtn);
        toolbar.Children.Add(statusTxt);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows      = false,
            CanUserDeleteRows   = false,
            IsReadOnly          = true,
        };
        VirtualizingPanel.SetIsVirtualizing(grid, true);
        grid.Columns.Add(new DataGridTextColumn { Header = "Offset",     Binding = new Binding(nameof(CarvedEntry.Offset))     { StringFormat = "{0:X8}" }, Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Format",     Binding = new Binding(nameof(CarvedEntry.FormatName)),  Width = 160 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Confidence", Binding = new Binding(nameof(CarvedEntry.Confidence))  { StringFormat = "{0:P0}" }, Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Source",     Binding = new Binding(nameof(CarvedEntry.Source)),      Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Est. Size",  Binding = new Binding(nameof(CarvedEntry.EstSize))     { StringFormat = "{0:N0}" }, Width = 90 });
        grid.Columns.Add(new DataGridTemplateColumn { Header = "Jump",    Width = 55, CellTemplate = BuildJumpTemplate() });
        grid.Columns.Add(new DataGridTemplateColumn { Header = "Extract", Width = 65, CellTemplate = BuildExtractTemplate() });
        grid.ItemsSource = _vm.Results;

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(grid);
        Content = root;

        scanBtn.Click      += async (_, _) => await _vm.ScanAsync();
        cancelBtn.Click    += (_, _) => _vm.Cancel();
        exportAllBtn.Click += async (_, _) =>
        {
            if (_context is null) return;
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select output folder" };
            if (dlg.ShowDialog() == true)
                await _vm.ExtractAllAsync(dlg.FolderName, _context);
        };
    }

    public void SetContext(IIDEHostContext context)
    {
        _context = context;
        _vm.SetContext(context);
    }

    public void OnFileOpened() { _vm.Results.Clear(); }

    private DataTemplate BuildJumpTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(Button.ContentProperty, "Jump");
        factory.SetValue(Button.PaddingProperty, new Thickness(4, 1, 4, 1));
        factory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is Button b && b.DataContext is CarvedEntry e)
                _vm.JumpTo(e);
        }));
        return new DataTemplate { VisualTree = factory };
    }

    private DataTemplate BuildExtractTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(Button.ContentProperty, "Extract");
        factory.SetValue(Button.PaddingProperty, new Thickness(4, 1, 4, 1));
        factory.AddHandler(Button.ClickEvent, new RoutedEventHandler(async (s, _) =>
        {
            if (s is Button b && b.DataContext is CarvedEntry e && _context is not null)
                await _vm.ExtractAsync(e, _context);
        }));
        return new DataTemplate { VisualTree = factory };
    }
}
