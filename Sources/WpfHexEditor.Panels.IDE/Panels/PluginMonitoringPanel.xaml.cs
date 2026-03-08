// ==========================================================
// Project: WpfHexEditor.Panels.IDE
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
using WpfHexEditor.Panels.IDE.Panels.ViewModels;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Panels.IDE.Panels;

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

        Loaded += (_, _) =>
        {
            // Collapse order: TbgPlugin(0) TbgLog(1) TbgCharts(2) TbgLayout(3) TbgInterval(4) TbgMonitor(5)
            _overflowManager = new ToolbarOverflowManager(
                toolbarContainer:      ToolbarBorder,
                alwaysVisiblePanel:    ToolbarRightPanel,
                overflowButton:        ToolbarOverflowButton,
                overflowMenu:          OverflowContextMenu,
                groupsInCollapseOrder: [TbgPlugin, TbgLog, TbgCharts, TbgLayout, TbgInterval, TbgMonitor],
                leftFixedElements:     [ToolbarLeftPanel]);

            Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
        };
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
        {
            ((INotifyCollectionChanged)_vm.CpuHistory).CollectionChanged    -= OnCpuHistoryChanged;
            ((INotifyCollectionChanged)_vm.MemoryHistory).CollectionChanged -= OnMemHistoryChanged;
            ((INotifyCollectionChanged)_vm.EventLog).CollectionChanged      -= OnEventLogChanged;
            _vm.PropertyChanged              -= OnVmPropertyChanged;
            _vm.RequestUninstall             -= OnRequestUninstall;
            _vm.RequestOpenInPluginManager   -= OnRequestOpenInPluginManager;
            _vm.Dispose();
            _vm = null;
        }
        Unloaded -= OnUnloaded;
    }

    // ── DataContext wiring ───────────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            ((INotifyCollectionChanged)_vm.CpuHistory).CollectionChanged    -= OnCpuHistoryChanged;
            ((INotifyCollectionChanged)_vm.MemoryHistory).CollectionChanged -= OnMemHistoryChanged;
            ((INotifyCollectionChanged)_vm.EventLog).CollectionChanged      -= OnEventLogChanged;
            _vm.PropertyChanged              -= OnVmPropertyChanged;
            _vm.RequestUninstall             -= OnRequestUninstall;
            _vm.RequestOpenInPluginManager   -= OnRequestOpenInPluginManager;
        }

        _vm = e.NewValue as PluginMonitoringViewModel;

        if (_vm is not null)
        {
            ((INotifyCollectionChanged)_vm.CpuHistory).CollectionChanged    += OnCpuHistoryChanged;
            ((INotifyCollectionChanged)_vm.MemoryHistory).CollectionChanged += OnMemHistoryChanged;
            ((INotifyCollectionChanged)_vm.EventLog).CollectionChanged      += OnEventLogChanged;
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
        var show = (_vm?.ShowEventLog ?? true) && (_vm?.EventLog.Count ?? 0) > 0;
        var vis  = show ? Visibility.Visible : Visibility.Collapsed;

        EventLogSplitter.Visibility = vis;
        EventLogArea.Visibility     = vis;

        // When hidden, collapse the rows entirely so they don't reserve space.
        RootGrid.RowDefinitions[4].Height = show
            ? new GridLength(4)
            : new GridLength(0);
        RootGrid.RowDefinitions[5].Height = show
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
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
        // Auto-scroll to the latest log entry.
        if (_vm?.EventLog.Count > 0)
            EventLogListBox.ScrollIntoView(_vm.EventLog[^1]);

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
        var result = MessageBox.Show(
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
        => RedrawChart(MemChartCanvas, MemPolyline, _vm?.MemoryHistory, maxValue: null);

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
