// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: Panels/ModulesPanel.xaml.cs
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.App.Debug.ViewModels;

namespace WpfHexEditor.App.Debug.Panels;

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
