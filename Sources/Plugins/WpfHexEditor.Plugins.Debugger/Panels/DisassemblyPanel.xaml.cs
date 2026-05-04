// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/DisassemblyPanel.xaml.cs
// ==========================================================

using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class DisassemblyPanel : UserControl
{
    private DisassemblyPanelViewModel? Vm => DataContext as DisassemblyPanelViewModel;

    public DisassemblyPanel()
    {
        InitializeComponent();
    }

    private void OnAddressKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Vm?.GoCommand.Execute(null);
    }
}
