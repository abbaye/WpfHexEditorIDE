// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Views/PropertiesPanel.xaml.cs

using System.Windows;
using WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

namespace WpfHexEditor.Plugins.ScreenRecorder.Views;

public partial class PropertiesPanel : System.Windows.Controls.UserControl
{
    public PropertiesPanel() => InitializeComponent();

    private PropertiesViewModel? Vm => DataContext as PropertiesViewModel;

    private void OnLoopDecrement(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.LoopCount = Math.Max(0, vm.LoopCount - 1);
    }
    private void OnLoopIncrement(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.LoopCount++;
    }

    private void OnRepeatDecrement(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.RepeatLastFrameDelay = Math.Max(0, vm.RepeatLastFrameDelay - 100);
    }
    private void OnRepeatIncrement(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.RepeatLastFrameDelay += 100;
    }

    private void OnIntervalDecrement(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.TimerInterval = Math.Max(80, vm.TimerInterval - 50);
    }
    private void OnIntervalIncrement(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.TimerInterval += 50;
    }
}
