// ==========================================================
// Project: WpfHexEditor.Plugins.Git
// File: Views/BranchPickerPopup.xaml.cs
// Description:
//     Code-behind for BranchPickerPopup.
//     Wires double-click on branch row to SwitchCommand.
// ==========================================================

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfHexEditor.Plugins.Git.ViewModels;

namespace WpfHexEditor.Plugins.Git.Views;

public partial class BranchPickerPopup : Popup
{
    public BranchPickerPopup() => InitializeComponent();

    private void OnBranchDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: BranchRow row } &&
            DataContext is BranchPickerViewModel vm)
        {
            vm.SwitchCommand.Execute(row);
        }
    }
}
