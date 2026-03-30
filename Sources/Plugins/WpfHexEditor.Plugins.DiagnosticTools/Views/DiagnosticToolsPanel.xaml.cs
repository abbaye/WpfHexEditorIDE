// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Views/DiagnosticToolsPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Code-behind for DiagnosticToolsPanel.
//     Thin shell — all logic is in DiagnosticToolsPanelViewModel.
//     Exposes three events so the plugin can delegate to services
//     without the view referencing Models directly.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using WpfHexEditor.Plugins.DiagnosticTools.ViewModels;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.DiagnosticTools.Views;

/// <summary>
/// VS-style dockable diagnostics panel.
/// </summary>
public sealed partial class DiagnosticToolsPanel : UserControl
{
    public event EventHandler? SnapshotRequested;
    public event EventHandler? PauseResumeRequested;
    public event EventHandler? ExportRequested;

    private ToolbarOverflowManager? _overflowManager;

    // -----------------------------------------------------------------------

    public DiagnosticToolsPanel(DiagnosticToolsPanelViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        _overflowManager = new ToolbarOverflowManager(
            toolbarContainer:      ToolbarBorder,
            alwaysVisiblePanel:    ToolbarRightPanel,
            overflowButton:        OverflowButton,
            overflowMenu:          OverflowMenu,
            groupsInCollapseOrder: [TbgMetrics, TbgActions],
            leftFixedElements:     [ToolbarLeftPanel]);
        Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
    }

    // -----------------------------------------------------------------------

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
        => _overflowManager?.SyncMenuVisibility();

    private void OnSnapshotClick(object sender, RoutedEventArgs e)
        => SnapshotRequested?.Invoke(this, EventArgs.Empty);

    private void OnPauseResumeClick(object sender, RoutedEventArgs e)
        => PauseResumeRequested?.Invoke(this, EventArgs.Empty);

    private void OnExportClick(object sender, RoutedEventArgs e)
        => ExportRequested?.Invoke(this, EventArgs.Empty);

    private void OnClearEventsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is DiagnosticToolsPanelViewModel vm)
            vm.Events.Clear();
    }
}
