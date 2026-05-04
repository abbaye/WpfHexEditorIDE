// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/MemoryWindowPanel.xaml.cs
// ==========================================================

using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class MemoryWindowPanel : UserControl
{
    private MemoryWindowViewModel? Vm => DataContext as MemoryWindowViewModel;

    public MemoryWindowPanel()
    {
        InitializeComponent();
    }

    private void OnAddressKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Vm?.GoCommand.Execute(null);
    }

    private void OnRefreshClick(object sender, System.Windows.RoutedEventArgs e)
        => _ = Vm?.RefreshAsync();
}
