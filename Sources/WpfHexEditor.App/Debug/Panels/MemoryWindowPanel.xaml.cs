// ==========================================================
// Project: WpfHexEditor.App.Debug
// File: Panels/MemoryWindowPanel.xaml.cs
// ==========================================================

using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.App.Debug.ViewModels;

namespace WpfHexEditor.App.Debug.Panels;

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
}
