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

/// <summary>#118 Custom Signature DB — code-behind-only panel.</summary>
public sealed class SignatureDbPanel : UserControl
{
    private readonly SignatureDbViewModel _vm;

    public SignatureDbPanel(SignatureDbViewModel vm)
    {
        _vm = vm;

        var addBtn  = new Button { Content = "Add",    Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(10, 2, 10, 2) };
        var remBtn  = new Button { Content = "Remove", Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(10, 2, 10, 2) };
        var testBtn = new Button { Content = "Test on Current File", Margin = new Thickness(0, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2) };

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 4, 4, 4) };
        toolbar.Children.Add(addBtn);
        toolbar.Children.Add(remBtn);
        toolbar.Children.Add(testBtn);

        var grid = new DataGrid
        {
            AutoGenerateColumns  = false,
            CanUserAddRows       = false,
            CanUserDeleteRows    = false,
            HeadersVisibility    = DataGridHeadersVisibility.Column,
            SelectionMode        = DataGridSelectionMode.Single,
        };
        grid.Columns.Add(MakeEditCol("Name",        nameof(UserSignature.Name),        120));
        grid.Columns.Add(MakeEditCol("Hex Pattern", nameof(UserSignature.HexPattern),  140));
        grid.Columns.Add(MakeEditCol("Offset",      nameof(UserSignature.Offset),       60));
        grid.Columns.Add(MakeEditCol("Description", nameof(UserSignature.Description),   0));
        grid.ItemsSource      = _vm.Signatures;
        grid.SelectionChanged += (_, _) => _vm.Selected = grid.SelectedItem as UserSignature;
        grid.CellEditEnding   += (_, _) => _vm.Persist();

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(grid);
        Content = root;

        addBtn.Click  += (_, _) => _vm.Add();
        remBtn.Click  += (_, _) => _vm.Remove();
        testBtn.Click += async (_, _) => await _vm.TestOnCurrentFileAsync();
    }

    public void SetContext(IIDEHostContext context) => _vm.SetContext(context);

    private static DataGridTextColumn MakeEditCol(string header, string path, double width)
    {
        return new DataGridTextColumn
        {
            Header  = header,
            Binding = new Binding(path) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width   = width > 0 ? new DataGridLength(width) : DataGridLength.Auto,
        };
    }
}
