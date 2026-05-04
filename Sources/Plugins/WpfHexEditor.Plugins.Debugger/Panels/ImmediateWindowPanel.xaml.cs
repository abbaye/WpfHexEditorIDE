using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class ImmediateWindowPanel : UserControl
{
    public ImmediateWindowPanel() => InitializeComponent();

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ImmediateWindowViewModel vm) return;
        if (e.Key == Key.Enter)
        {
            _ = vm.ExecuteAsync();
            // Auto-scroll transcript to bottom
            if (TranscriptBox.Items.Count > 0)
                TranscriptBox.ScrollIntoView(TranscriptBox.Items[^1]);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            vm.HistoryUp();
            if (sender is TextBox tb) tb.CaretIndex = tb.Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            vm.HistoryDown();
            if (sender is TextBox tb) tb.CaretIndex = tb.Text.Length;
            e.Handled = true;
        }
    }
}
