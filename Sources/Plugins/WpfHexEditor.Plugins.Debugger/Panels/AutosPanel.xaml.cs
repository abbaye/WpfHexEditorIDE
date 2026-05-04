using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class AutosPanel : UserControl
{
    public AutosPanel() => InitializeComponent();

    private void OnValueClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2) return;
        if ((sender as FrameworkElement)?.DataContext is VariableNode node)
            node.IsEditing = true;
    }

    private void OnEditVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
        {
            tb.SelectAll();
            tb.Focus();
        }
    }

    private void OnEditKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not VariableNode node) return;

        if (e.Key == Key.Enter)
        {
            var vm = DataContext as AutosPanelViewModel;
            _ = vm?.SetValueAsync(node, tb.Text);
            node.IsEditing = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            node.IsEditing = false;
            e.Handled = true;
        }
    }

    private void OnEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is VariableNode node)
            node.IsEditing = false;
    }
}
