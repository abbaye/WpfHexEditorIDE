// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/BreakpointExplorerPanel.xaml.cs
// Description:
//     Code-behind for the VS-style Breakpoint Explorer panel.
//     Delegates all logic to BreakpointExplorerViewModel.
//     Uses ToolbarOverflowManager for dynamic toolbar overflow.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Plugins.Debugger.ViewModels;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class BreakpointExplorerPanel : UserControl
{
    private BreakpointExplorerViewModel? Vm => DataContext as BreakpointExplorerViewModel;
    private ToolbarOverflowManager? _overflowManager;

    public BreakpointExplorerPanel()
    {
        InitializeComponent();

        _overflowManager = new ToolbarOverflowManager(
            toolbarContainer:      ToolbarBorder,
            alwaysVisiblePanel:    ToolbarRightPanel,
            overflowButton:        OverflowButton,
            overflowMenu:          OverflowMenu,
            groupsInCollapseOrder: [TbgGroupBy, TbgImportExport, TbgActions],
            leftFixedElements:     [ToolbarLeftPanel]);

        Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
    }

    // ── Toolbar overflow ──────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowMenu.PlacementTarget = OverflowButton;
        OverflowMenu.Placement       = PlacementMode.Bottom;
        OverflowMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        _overflowManager?.SyncMenuVisibility();
    }

    // ── Overflow menu handlers ────────────────────────────────────────────

    private void OnOvfEnableAll(object sender, RoutedEventArgs e)  => Vm?.EnableAllCommand.Execute(null);
    private void OnOvfDisableAll(object sender, RoutedEventArgs e) => Vm?.DisableAllCommand.Execute(null);
    private void OnOvfDeleteAll(object sender, RoutedEventArgs e)  => Vm?.DeleteAllCommand.Execute(null);

    private void OnOvfGroupNone(object sender, RoutedEventArgs e)    { GroupByCombo.SelectedIndex = 0; }
    private void OnOvfGroupFile(object sender, RoutedEventArgs e)    { GroupByCombo.SelectedIndex = 1; }
    private void OnOvfGroupType(object sender, RoutedEventArgs e)    { GroupByCombo.SelectedIndex = 2; }
    private void OnOvfGroupEnabled(object sender, RoutedEventArgs e) { GroupByCombo.SelectedIndex = 3; }
    private void OnOvfGroupProject(object sender, RoutedEventArgs e) { GroupByCombo.SelectedIndex = 4; }

    // ── Group-by ComboBox ─────────────────────────────────────────────────

    private void OnGroupByChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || GroupByCombo.SelectedIndex < 0) return;
        Vm.GroupBy = (GroupByMode)GroupByCombo.SelectedIndex;
    }

    // ── Flat list double-click → navigate ─────────────────────────────────

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm?.SelectedBreakpoint is not null)
            Vm.GoToSourceCommand.Execute(Vm.SelectedBreakpoint);
    }

    // ── Tree item double-click → navigate ─────────────────────────────────

    private void OnTreeItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is BreakpointRowEx row)
        {
            Vm?.GoToSourceCommand.Execute(row);
            e.Handled = true;
        }
    }

    // ── CheckBox enable/disable ───────────────────────────────────────────

    private void OnCheckBoxClicked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is BreakpointRowEx row)
            Vm?.ToggleEnabledCommand.Execute(row);
    }

    // ── Context menu handlers ─────────────────────────────────────────────

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

    // ── VS XML Import / Export ────────────────────────────────────────────

    private void OnImportBreakpoints(object sender, RoutedEventArgs e) => Vm?.ImportCommand.Execute(null);
    private void OnExportBreakpoints(object sender, RoutedEventArgs e) => Vm?.ExportCommand.Execute(null);

    private BreakpointRowEx? GetSelectedRow() =>
        Vm?.SelectedBreakpoint ?? FlatList.SelectedItem as BreakpointRowEx;
}
