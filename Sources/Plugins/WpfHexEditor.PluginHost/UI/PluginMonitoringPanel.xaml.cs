// ==========================================================
// Project: WpfHexEditor.PluginHost.UI
// File: PluginMonitoringPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Code-behind for the Plugin Monitoring docking panel.
//     Redraws the CPU% and Memory MB Canvas+Polyline charts
//     whenever the ViewModel's chart history collections change.
//     Provides value converters for the Permissions tab (BoolToCheck,
//     BoolToGrantColor) and sparkline column visibility management.
//     Handles .whxplugin drag-drop install and uninstall confirmation dialog.
//
// Architecture Notes:
//     Pure WPF chart rendering: normalise values to canvas pixels,
//     update Polyline.Points directly. No external charting library.
//     SparklineControl (FrameworkElement) handles per-plugin redraws
//     autonomously via INotifyCollectionChanged subscription.
//     Drag-drop: DragOver shows DropHintOverlay, Drop delegates to VM.
//     Uninstall: code-behind shows MessageBox confirm before calling VM.
// ==========================================================

using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.PluginHost.UI;
using WpfHexEditor.SDK.UI;
using WpfHexEditor.Editor.Core.Dialogs;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// IValueConverter — not actively used for chart drawing (done in code-behind).
/// Kept for XAML resource dictionary completeness.
/// </summary>
public sealed class ChartPointsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue; // charts drawn in code-behind

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Converts a bool to a Segoe MDL2 checkmark glyph (\uE73E) or empty string.
/// Used in the Permissions tab "Declared" column.
/// </summary>
public sealed class BoolToCheckConverter : IValueConverter
{
    public static readonly BoolToCheckConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "\uE73E" : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Converts a bool (IsGranted) to a foreground color:
///   true  → green  (#22C55E — permission granted)
///   false → gray   (#6B7280 — permission not granted)
/// Used in the Permissions tab risk-badge column.
/// </summary>
public sealed class BoolToGrantColorConverter : IValueConverter
{
    public static readonly BoolToGrantColorConverter Instance = new();

    private static readonly SolidColorBrush GrantedBrush =
        new(Color.FromRgb(0x22, 0xC5, 0x5E)) { };  // #22C55E

    private static readonly SolidColorBrush RevokedBrush =
        new(Color.FromRgb(0x6B, 0x72, 0x80)) { };  // #6B7280

    static BoolToGrantColorConverter()
    {
        GrantedBrush.Freeze();
        RevokedBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? GrantedBrush : RevokedBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Multi-value converter: [CollectionViewGroup, PluginMonitoringViewModel] → PluginGroupSummaryViewModel.
/// Used in the DataGrid GroupStyle to bind the group header to the correct aggregate summary VM.
/// Values[0] = CollectionViewGroup (DataContext of the GroupItem)
/// Values[1] = PluginMonitoringViewModel (DataContext of the DataGrid, via RelativeSource)
/// </summary>
public sealed class GroupSummaryConverter : IMultiValueConverter
{
    public static readonly GroupSummaryConverter Instance = new();

    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return null;
        var groupName = (values[0] as System.Windows.Data.CollectionViewGroup)?.Name as string;
        var vm = values[1] as PluginMonitoringViewModel;
        return vm?.GetGroupSummary(groupName);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public partial class PluginMonitoringPanel : UserControl
{
    private PluginMonitoringViewModel? _vm;
    private bool _suppressLayoutChange;
    private ToolbarOverflowManager?   _overflowManager;

    public PluginMonitoringPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        CpuChartCanvas.SizeChanged += (_, _) => RedrawCharts();
        MemChartCanvas.SizeChanged += (_, _) => RedrawCharts();
        DragLeave += (_, _) => DropHintOverlay.Visibility = Visibility.Collapsed;
        Unloaded  += OnUnloaded;
        Loaded    += OnLoaded;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires on every Loaded event — including reloads after the panel was temporarily
    /// removed from the visual tree by the docking framework (e.g. hidden tab, re-dock).
    /// Re-subscribes VM event handlers that were removed in OnUnloaded, restoring
    /// chart redraws, layout-change notifications, and code-behind interactions.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // If _vm is non-null the panel was previously unloaded without a DataContext change
        // (OnUnloaded unsubscribed handlers but kept the reference alive so layout changes
        // remain functional after reload).
        if (_vm is not null)
        {
            ((INotifyCollectionChanged)_vm.CpuHistory).CollectionChanged        += OnCpuHistoryChanged;
            ((INotifyCollectionChanged)_vm.MemoryHistory).CollectionChanged     += OnMemHistoryChanged;
            ((INotifyCollectionChanged)_vm.EventLog).CollectionChanged          += OnEventLogChanged;
            ((INotifyCollectionChanged)_vm.GcCleanupEvents).CollectionChanged   += OnGcCleanupEventsChanged;
            _vm.PropertyChanged            += OnVmPropertyChanged;
            _vm.RequestUninstall           += OnRequestUninstall;
            _vm.RequestOpenInPluginManager += OnRequestOpenInPluginManager;
            ApplySparklineVisibility();
            SyncLayoutCombo(_vm.ChartsPosition);
            RebuildContentGrid(_vm.ChartsPosition);
            ApplyEventLogVisibility();
            RedrawCharts();
        }

        // Collapse order: TbgPlugin(0) TbgLog(1) TbgCharts(2) TbgLayout(3) TbgInterval(4) TbgMonitor(5)
        _overflowManager = new ToolbarOverflowManager(
            toolbarContainer:      ToolbarBorder,
            alwaysVisiblePanel:    ToolbarRightPanel,
            overflowButton:        ToolbarOverflowButton,
            overflowMenu:          OverflowContextMenu,
            groupsInCollapseOrder: [TbgPlugin, TbgLog, TbgCharts, TbgLayout, TbgInterval, TbgMonitor],
            leftFixedElements:     [ToolbarLeftPanel]);

        Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
        {
            ((INotifyCollectionChanged)_vm.CpuHistory).CollectionChanged        -= OnCpuHistoryChanged;
            ((INotifyCollectionChanged)_vm.MemoryHistory).CollectionChanged     -= OnMemHistoryChanged;
            ((INotifyCollectionChanged)_vm.EventLog).CollectionChanged          -= OnEventLogChanged;
            ((INotifyCollectionChanged)_vm.GcCleanupEvents).CollectionChanged   -= OnGcCleanupEventsChanged;
            _vm.PropertyChanged              -= OnVmPropertyChanged;
            _vm.RequestUninstall             -= OnRequestUninstall;
            _vm.RequestOpenInPluginManager   -= OnRequestOpenInPluginManager;
            // Intentionally NOT disposing or nulling _vm here.
            // The docking framework fires Unloaded when a panel is hidden (e.g. behind another
            // tab) and Loaded when it becomes visible again. Disposing the VM on every hide
            // would stop its timer and null _vm, causing layout changes and chart updates to
            // stop working after the first hide/show cycle.
            // VM disposal is handled in OnDataContextChanged when the DataContext is replaced.
        }
        // Do NOT unsubscribe this handler — it must fire on every Unloaded event
        // so that OnLoaded can safely re-subscribe without double-subscribing.
    }

    // ── DataContext wiring ───────────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            ((INotifyCollectionChanged)_vm.CpuHistory).CollectionChanged        -= OnCpuHistoryChanged;
            ((INotifyCollectionChanged)_vm.MemoryHistory).CollectionChanged     -= OnMemHistoryChanged;
            ((INotifyCollectionChanged)_vm.EventLog).CollectionChanged          -= OnEventLogChanged;
            ((INotifyCollectionChanged)_vm.GcCleanupEvents).CollectionChanged   -= OnGcCleanupEventsChanged;
            _vm.PropertyChanged              -= OnVmPropertyChanged;
            _vm.RequestUninstall             -= OnRequestUninstall;
            _vm.RequestOpenInPluginManager   -= OnRequestOpenInPluginManager;
            _vm.Dispose(); // Dispose the old VM now that DataContext is truly changing.
        }

        _vm = e.NewValue as PluginMonitoringViewModel;

        if (_vm is not null)
        {
            ((INotifyCollectionChanged)_vm.CpuHistory).CollectionChanged        += OnCpuHistoryChanged;
            ((INotifyCollectionChanged)_vm.MemoryHistory).CollectionChanged     += OnMemHistoryChanged;
            ((INotifyCollectionChanged)_vm.EventLog).CollectionChanged          += OnEventLogChanged;
            ((INotifyCollectionChanged)_vm.GcCleanupEvents).CollectionChanged   += OnGcCleanupEventsChanged;
            _vm.PropertyChanged              += OnVmPropertyChanged;
            _vm.RequestUninstall             += OnRequestUninstall;
            _vm.RequestOpenInPluginManager   += OnRequestOpenInPluginManager;
            ApplySparklineVisibility();
            SyncLayoutCombo(_vm.ChartsPosition);
            RebuildContentGrid(_vm.ChartsPosition);
            ApplyEventLogVisibility();
        }

        RedrawCharts();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PluginMonitoringViewModel.ChartsPosition):
                RebuildContentGrid(_vm!.ChartsPosition);
                break;
            case nameof(PluginMonitoringViewModel.ShowSparklines):
                ApplySparklineVisibility();
                break;
            case nameof(PluginMonitoringViewModel.ShowEventLog):
                ApplyEventLogVisibility();
                break;
        }
    }

    // ── Sparkline column visibility ──────────────────────────────────────────

    /// <summary>
    /// Shows or hides the two sparkline DataGrid columns based on ViewModel.ShowSparklines.
    /// </summary>
    private void ApplySparklineVisibility()
    {
        var vis = (_vm?.ShowSparklines ?? true) ? Visibility.Visible : Visibility.Collapsed;
        SparklineCpuColumn.Visibility = vis;
        SparklineMemColumn.Visibility = vis;
    }

    // ── EventLog visibility ──────────────────────────────────────────────────

    /// <summary>
    /// Shows or hides the EventLog splitter and area based on the user's ShowEventLog
    /// toggle and whether there are any log entries. Also adjusts the outer grid row
    /// heights so hidden rows consume no space.
    /// </summary>
    private void ApplyEventLogVisibility()
    {
        // Les événements sont maintenant intégrés dans le TabControl comme premier onglet.
        // Cette méthode n'est plus nécessaire mais conservée pour compatibilité.
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Syncs the layout combo box to <paramref name="pos"/> without re-triggering
    /// the <see cref="OnLayoutPositionChanged"/> handler.
    /// </summary>
    private void SyncLayoutCombo(MonitorChartsPosition pos)
    {
        _suppressLayoutChange = true;
        foreach (ComboBoxItem item in LayoutPositionCombo.Items)
        {
            if (item.Tag?.ToString() == pos.ToString())
            {
                LayoutPositionCombo.SelectedItem = item;
                break;
            }
        }
        _suppressLayoutChange = false;
    }

    /// <summary>
    /// Fires when the user selects a new layout from the toolbar combo.
    /// Updates the ViewModel and immediately rebuilds the layout.
    /// </summary>
    private void OnLayoutPositionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLayoutChange || _vm is null) return;
        if (LayoutPositionCombo.SelectedItem is not ComboBoxItem item) return;
        if (!Enum.TryParse<MonitorChartsPosition>(item.Tag?.ToString(), out var pos)) return;

        // Setting ChartsPosition fires OnVmPropertyChanged → RebuildContentGrid.
        // Do NOT call RebuildContentGrid here to avoid creating a second set of
        // RowDefinition/ColumnDefinition objects, which would corrupt the GridSplitter
        // adjacent-definition references and break dragging after repeated layout changes.
        _vm.ChartsPosition = pos;
    }

    /// <summary>
    /// Rebuilds ContentGrid row/column definitions and repositions the three
    /// child elements (ChartsArea, ContentSplitter, PluginsDataGrid) and the
    /// inner chart grid (ChartsInnerGrid) for the given <paramref name="position"/>.
    /// </summary>
    private void RebuildContentGrid(MonitorChartsPosition position)
    {
        ContentGrid.RowDefinitions.Clear();
        ContentGrid.ColumnDefinitions.Clear();

        switch (position)
        {
            case MonitorChartsPosition.Top:
                // 3 rows: charts (1*) | splitter | table (2*)
                ContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 50 });
                ContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
                ContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star), MinHeight = 50 });

                Grid.SetRow(ChartsArea,      0); Grid.SetColumn(ChartsArea,      0);
                Grid.SetRow(ContentSplitter, 1); Grid.SetColumn(ContentSplitter, 0);
                Grid.SetRow(PluginsDataGrid, 2); Grid.SetColumn(PluginsDataGrid, 0);

                ContentSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                ContentSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
                ContentSplitter.Height              = 4;
                ContentSplitter.Width               = double.NaN;
                ContentSplitter.ResizeDirection     = GridResizeDirection.Rows;

                RebuildChartsInnerGrid(stacked: false);
                break;

            case MonitorChartsPosition.Bottom:
                // 3 rows: table (2*) | splitter | charts (1*)
                ContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star), MinHeight = 50 });
                ContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
                ContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 50 });

                Grid.SetRow(PluginsDataGrid, 0); Grid.SetColumn(PluginsDataGrid, 0);
                Grid.SetRow(ContentSplitter, 1); Grid.SetColumn(ContentSplitter, 0);
                Grid.SetRow(ChartsArea,      2); Grid.SetColumn(ChartsArea,      0);

                ContentSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                ContentSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
                ContentSplitter.Height              = 4;
                ContentSplitter.Width               = double.NaN;
                ContentSplitter.ResizeDirection     = GridResizeDirection.Rows;

                RebuildChartsInnerGrid(stacked: false);
                break;

            case MonitorChartsPosition.Left:
                // 3 columns: charts (1*) | splitter | table (1*)
                ContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });
                ContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                ContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });

                Grid.SetRow(ChartsArea,      0); Grid.SetColumn(ChartsArea,      0);
                Grid.SetRow(ContentSplitter, 0); Grid.SetColumn(ContentSplitter, 1);
                Grid.SetRow(PluginsDataGrid, 0); Grid.SetColumn(PluginsDataGrid, 2);

                ContentSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                ContentSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
                ContentSplitter.Height              = double.NaN;
                ContentSplitter.Width               = 4;
                ContentSplitter.ResizeDirection     = GridResizeDirection.Columns;

                RebuildChartsInnerGrid(stacked: true);
                break;

            case MonitorChartsPosition.Right:
                // 3 columns: table (1*) | splitter | charts (1*)
                ContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });
                ContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                ContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });

                Grid.SetRow(PluginsDataGrid, 0); Grid.SetColumn(PluginsDataGrid, 0);
                Grid.SetRow(ContentSplitter, 0); Grid.SetColumn(ContentSplitter, 1);
                Grid.SetRow(ChartsArea,      0); Grid.SetColumn(ChartsArea,      2);

                ContentSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                ContentSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
                ContentSplitter.Height              = double.NaN;
                ContentSplitter.Width               = 4;
                ContentSplitter.ResizeDirection     = GridResizeDirection.Columns;

                RebuildChartsInnerGrid(stacked: true);
                break;
        }
    }

    /// <summary>
    /// Rebuilds the inner charts grid.
    /// <paramref name="stacked"/> = true → CPU/Memory in rows (Left/Right modes).
    /// <paramref name="stacked"/> = false → CPU/Memory in columns (Top/Bottom modes).
    /// </summary>
    private void RebuildChartsInnerGrid(bool stacked)
    {
        ChartsInnerGrid.RowDefinitions.Clear();
        ChartsInnerGrid.ColumnDefinitions.Clear();

        if (!stacked)
        {
            // Side-by-side columns: CPU | splitter | Memory
            ChartsInnerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ChartsInnerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            ChartsInnerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(CpuChartGrid,        0); Grid.SetColumn(CpuChartGrid,        0);
            Grid.SetRow(ChartsInnerSplitter, 0); Grid.SetColumn(ChartsInnerSplitter, 1);
            Grid.SetRow(MemChartGrid,        0); Grid.SetColumn(MemChartGrid,        2);

            ChartsInnerSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            ChartsInnerSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
            ChartsInnerSplitter.Width               = 4;
            ChartsInnerSplitter.Height              = double.NaN;
            ChartsInnerSplitter.ResizeDirection     = GridResizeDirection.Columns;
        }
        else
        {
            // Stacked rows: CPU / splitter / Memory
            ChartsInnerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            ChartsInnerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            ChartsInnerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(CpuChartGrid,        0); Grid.SetColumn(CpuChartGrid,        0);
            Grid.SetRow(ChartsInnerSplitter, 1); Grid.SetColumn(ChartsInnerSplitter, 0);
            Grid.SetRow(MemChartGrid,        2); Grid.SetColumn(MemChartGrid,        0);

            ChartsInnerSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            ChartsInnerSplitter.VerticalAlignment   = VerticalAlignment.Stretch;
            ChartsInnerSplitter.Width               = double.NaN;
            ChartsInnerSplitter.Height              = 4;
            ChartsInnerSplitter.ResizeDirection     = GridResizeDirection.Rows;
        }
    }

    // ── Toolbar overflow ──────────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowContextMenu.PlacementTarget = ToolbarOverflowButton;
        OverflowContextMenu.Placement       = PlacementMode.Bottom;
        OverflowContextMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;

        OvfStartStop.Header    = _vm.StartStopLabel;
        OvfTogglePlugin.Header = _vm.TogglePluginLabel;
        OvfShowSparklines.IsChecked = _vm.ShowSparklines;
        OvfShowEventLog.IsChecked   = _vm.ShowEventLog;

        var pos = _vm.ChartsPosition.ToString();
        OvfLayoutTop.IsChecked    = pos == "Top";
        OvfLayoutBottom.IsChecked = pos == "Bottom";
        OvfLayoutLeft.IsChecked   = pos == "Left";
        OvfLayoutRight.IsChecked  = pos == "Right";

        _overflowManager?.SyncMenuVisibility();
    }

    private void OnOverflowLayoutClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not MenuItem mi) return;
        if (Enum.TryParse<MonitorChartsPosition>(mi.Tag?.ToString(), out var pos))
        {
            _vm.ChartsPosition = pos;
            SyncLayoutCombo(pos);
        }
    }

    private void OnOvfStartStop(object sender, RoutedEventArgs e)       => _vm?.StartStopCommand.Execute(null);
    private void OnOvfReset(object sender, RoutedEventArgs e)            => _vm?.ResetCommand.Execute(null);
    private void OnOvfReloadPlugin(object sender, RoutedEventArgs e)     => _vm?.ReloadPluginCommand.Execute(null);
    private void OnOvfTogglePlugin(object sender, RoutedEventArgs e)     => _vm?.TogglePluginCommand.Execute(null);
    private void OnOvfInstallPlugin(object sender, RoutedEventArgs e)    => _vm?.InstallPluginCommand.Execute(null);
    private void OnOvfUninstallPlugin(object sender, RoutedEventArgs e)  => _vm?.UninstallPluginCommand.Execute(null);
    private void OnOvfCrashReport(object sender, RoutedEventArgs e)      => _vm?.ExportCrashReportCommand.Execute(null);
    private void OnOvfClearLog(object sender, RoutedEventArgs e)         => _vm?.ClearLogCommand.Execute(null);
    private void OnOvfToggleSparklines(object sender, RoutedEventArgs e) => _vm?.ToggleSparklinesCommand.Execute(null);
    private void OnOvfToggleEventLog(object sender, RoutedEventArgs e)   => _vm?.ToggleEventLogCommand.Execute(null);

    private void OnOvfInterval(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && int.TryParse(mi.Tag?.ToString(), out var sec))
            _vm?.SetIntervalCommand.Execute(sec);
    }

    // ── Export dropdown ──────────────────────────────────────────────────────

    private void OnExportButtonClick(object sender, RoutedEventArgs e)
    {
        if (Resources["ExportMenu"] is not ContextMenu menu) return;
        menu.PlacementTarget = ExportButton;
        menu.Placement = PlacementMode.Bottom;
        menu.DataContext = _vm;
        menu.IsOpen = true;
    }

    // ── Selection & event log ────────────────────────────────────────────────

    private void OnPluginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is not null && sender is DataGrid dg)
            _vm.SelectedPlugin = dg.SelectedItem as PluginMonitorRow;
    }

    private void OnEventLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Defer ScrollIntoView until after WPF's ItemContainerGenerator has finished
        // generating the container for the new item. Calling it synchronously inside
        // CollectionChanged causes "ItemsControl is inconsistent with its item source".
        if (_vm?.EventLog.Count > 0)
        {
            var lastItem = _vm.EventLog[^1];
            Dispatcher.InvokeAsync(
                () => EventLogListBox.ScrollIntoView(lastItem),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // Show/hide the log section based on current ShowEventLog flag and whether there are entries.
        ApplyEventLogVisibility();
    }

    // ── Drag-drop install ────────────────────────────────────────────────────

    private void OnPanelDragOver(object sender, DragEventArgs e)
    {
        if (IsValidPluginDrop(e))
        {
            e.Effects = DragDropEffects.Copy;
            DropHintOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            DropHintOverlay.Visibility = Visibility.Collapsed;
        }
        e.Handled = true;
    }

    private void OnPanelDrop(object sender, DragEventArgs e)
    {
        DropHintOverlay.Visibility = Visibility.Collapsed;

        if (_vm is null || !IsValidPluginDrop(e)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var packagePath = files?.FirstOrDefault(
            f => string.Equals(Path.GetExtension(f), ".whxplugin", StringComparison.OrdinalIgnoreCase));

        if (packagePath is not null)
            _ = _vm.InstallFromDropAsync(packagePath);

        e.Handled = true;
    }

    private static bool IsValidPluginDrop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        return files?.Any(f => string.Equals(
            Path.GetExtension(f), ".whxplugin", StringComparison.OrdinalIgnoreCase)) == true;
    }

    // ── Row right-click selection ────────────────────────────────────────────

    /// <summary>
    /// Ensures the right-clicked row is selected before the ContextMenu opens,
    /// so that _selectedPlugin is set in the ViewModel when the menu items evaluate
    /// their IsEnabled / Header bindings.
    /// </summary>
    internal void OnRowPreviewRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row)
            row.IsSelected = true;
    }

    // ── Open in Plugin Manager ───────────────────────────────────────────────

    /// <summary>
    /// Opens the Plugin Manager tab (or focuses it if already open) and pre-selects
    /// the plugin whose ID was received from the ViewModel event.
    /// Uses the same reflection-on-MainWindow pattern as OnContextOpenMonitor in
    /// PluginManagerControl, keeping the two panels symmetrical.
    /// </summary>
    private void OnRequestOpenInPluginManager(string pluginId)
    {
        var win = Window.GetWindow(this);
        if (win is null) return;

        win.GetType()
           .GetMethod("OnOpenPluginManagerWithSelection",
               System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
           ?.Invoke(win, [pluginId]);
    }

    // ── Uninstall confirmation ───────────────────────────────────────────────

    private void OnRequestUninstall(PluginMonitorRow row)
    {
        var result = IdeMessageBox.Show(
            $"Uninstall \"{row.Name}\"?\n\nThis will remove the plugin files from disk and cannot be undone.",
            "Uninstall Plugin",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes && _vm is not null)
            _ = _vm.UninstallConfirmedAsync(row);
    }

    // ── Chart drawing ────────────────────────────────────────────────────────

    private void OnCpuHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RedrawChart(CpuChartCanvas, CpuPolyline, _vm?.CpuHistory, maxValue: 100.0);

    private void OnMemHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RedrawChart(MemChartCanvas, MemPolyline, _vm?.MemoryHistory, maxValue: null);
        RedrawGcMarkers(); // keep marker X positions in sync as the chart scrolls
    }

    private void OnIntervalSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item
            && int.TryParse(item.Tag?.ToString(), out var seconds))
        {
            _vm.SetIntervalCommand.Execute(seconds);
        }
    }

    private void RedrawCharts()
    {
        RedrawChart(CpuChartCanvas, CpuPolyline, _vm?.CpuHistory, maxValue: 100.0);
        RedrawChart(MemChartCanvas, MemPolyline, _vm?.MemoryHistory, maxValue: null);
        RedrawGcMarkers();
    }

    private void OnGcCleanupEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RedrawGcMarkers();

    /// <summary>
    /// Draws a dashed amber vertical line on GcMarkerCanvas for each recorded GC cleanup event
    /// that falls within the current visible time window of the memory chart.
    /// </summary>
    private void RedrawGcMarkers()
    {
        GcMarkerCanvas.Children.Clear();

        var events  = _vm?.GcCleanupEvents;
        var history = _vm?.MemoryHistory;
        if (events is null || history is null || history.Count < 2) return;

        var w = GcMarkerCanvas.ActualWidth;
        var h = GcMarkerCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var minTime = history[0].Time;
        var maxTime = history[history.Count - 1].Time;
        var rangeMs = (maxTime - minTime).TotalMilliseconds;
        if (rangeMs <= 0) return;

        var amber = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        amber.Freeze();

        foreach (var gcTime in events)
        {
            if (gcTime < minTime) continue; // before the visible window

            // gcTime is captured a few ms after the last chart point, so it can be
            // slightly greater than maxTime. Clamp to the right edge in that case.
            var px = gcTime >= maxTime
                ? w
                : w * (gcTime - minTime).TotalMilliseconds / rangeMs;

            GcMarkerCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1              = px,
                Y1              = 0,
                X2              = px,
                Y2              = h,
                Stroke          = amber,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Opacity         = 0.75
            });
        }
    }

    /// <summary>
    /// Maps a ChartPoint collection onto a Polyline inside a Canvas.
    /// X axis = linear time (left→right), Y axis = value normalised to canvas height
    /// (top = max). maxValue clamps the Y scale; null = auto-scale to observed peak.
    /// </summary>
    private static void RedrawChart(
        Canvas canvas,
        System.Windows.Shapes.Polyline polyline,
        IReadOnlyList<ChartPoint>? points,
        double? maxValue)
    {
        polyline.Points.Clear();

        if (points is null || points.Count < 2) return;

        var w = canvas.ActualWidth;
        var h = canvas.ActualHeight;

        if (w <= 0 || h <= 0) return;

        var peak = maxValue ?? points.Max(p => p.Value);
        if (peak <= 0) peak = 1; // avoid divide-by-zero when all values are 0

        var count = points.Count;
        for (int i = 0; i < count; i++)
        {
            var px = w * i / (count - 1);
            var py = h - (h * points[i].Value / peak); // Y is inverted (top = max)
            polyline.Points.Add(new Point(px, py));
        }
    }
}
