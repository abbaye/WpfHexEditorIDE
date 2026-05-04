// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/ModulesPanel.xaml.cs
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class ModulesPanel : UserControl
{
    private ModulesPanelViewModel? Vm => DataContext as ModulesPanelViewModel;

    public ModulesPanel()
    {
        InitializeComponent();
    }

    private void OnRefreshClick(object sender, System.Windows.RoutedEventArgs e)
        => _ = Vm?.RefreshAsync();
}
