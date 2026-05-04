using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WpfHexEditor.App.Debug.ViewModels;

namespace WpfHexEditor.App.Debug.Panels;

public partial class AutosPanel : UserControl
{
    public AutosPanel()
    {
        InitializeComponent();
        InputBindings.Add(new KeyBinding(ApplicationCommands.Find,
            new KeyGesture(Key.F, ModifierKeys.Control)));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Find, (_, _) => SearchBox.Focus()));
    }

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

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is not AutosPanelViewModel vm) return;
        var view = CollectionViewSource.GetDefaultView(vm.Variables);
        var query = SearchBox.Text;
        view.Filter = string.IsNullOrEmpty(query) ? null
            : o => o is VariableNode n && (n.Name?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
