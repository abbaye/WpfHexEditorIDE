// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/BreakpointExplorerPanel.xaml.cs
// Description:
//     Code-behind for the VS-style Breakpoint Explorer panel.
//     Delegates all logic to BreakpointExplorerViewModel.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class BreakpointExplorerPanel : UserControl
{
    private BreakpointExplorerViewModel? Vm => DataContext as BreakpointExplorerViewModel;

    public BreakpointExplorerPanel()
    {
        InitializeComponent();
    }

    // ── Group-by ComboBox ───────────────────────────────────────────────────

    private void OnGroupByChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || GroupByCombo.SelectedIndex < 0) return;
        Vm.GroupBy = (GroupByMode)GroupByCombo.SelectedIndex;
    }

    // ── Flat list double-click → navigate ───────────────────────────────────

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm?.SelectedBreakpoint is not null)
            Vm.GoToSourceCommand.Execute(Vm.SelectedBreakpoint);
    }

    // ── Tree item double-click → navigate ───────────────────────────────────

    private void OnTreeItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is BreakpointRowEx row)
        {
            Vm?.GoToSourceCommand.Execute(row);
            e.Handled = true;
        }
    }

    // ── CheckBox enable/disable ─────────────────────────────────────────────

    private void OnCheckBoxClicked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is BreakpointRowEx row)
            Vm?.ToggleEnabledCommand.Execute(row);
    }

    // ── Context menu handlers ───────────────────────────────────────────────

    private void OnGoToSource(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row is not null) Vm?.GoToSourceCommand.Execute(row);
    }

    private void OnToggleEnabled(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row is not null) Vm?.ToggleEnabledCommand.Execute(row);
    }

    private void OnCopyLocation(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row is not null) Vm?.CopyLocationCommand.Execute(row);
    }

    private void OnDeleteSelected(object sender, RoutedEventArgs e)
    {
        var row = GetSelectedRow();
        if (row is not null) Vm?.DeleteCommand.Execute(row);
    }

    private BreakpointRowEx? GetSelectedRow() =>
        Vm?.SelectedBreakpoint ?? FlatList.SelectedItem as BreakpointRowEx;
}
