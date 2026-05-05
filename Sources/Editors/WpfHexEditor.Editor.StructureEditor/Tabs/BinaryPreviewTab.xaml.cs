// ==========================================================
// Project: WpfHexEditor.Editor.StructureEditor
// File: Tabs/BinaryPreviewTab.xaml.cs
// Description:
//     Code-behind for BinaryPreviewTab. Wires file picker to
//     BinaryPreviewViewModel.LoadBinaryAsync.
// ==========================================================

using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Tabs;

public partial class BinaryPreviewTab : UserControl
{
    public BinaryPreviewTab()
    {
        InitializeComponent();
    }

    private async void OnLoadBinaryClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not BinaryPreviewViewModel vm) return;

        var dlg = new OpenFileDialog
        {
            Title  = "Select binary file",
            Filter = "All files (*.*)|*.*",
        };

        if (dlg.ShowDialog() == true)
            await vm.LoadBinaryAsync(dlg.FileName);
    }
}
