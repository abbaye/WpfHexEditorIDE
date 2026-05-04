// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/TasksPanel.xaml.cs
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class TasksPanel : UserControl
{
    private TasksPanelViewModel? Vm => DataContext as TasksPanelViewModel;

    public TasksPanel()
    {
        InitializeComponent();
    }

    private void OnRefreshClick(object sender, System.Windows.RoutedEventArgs e)
        => _ = Vm?.RefreshAsync();
}
