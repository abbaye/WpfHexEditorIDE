// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Views/DiagnosticToolsPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Code-behind for the DiagnosticToolsPanel UserControl.
//     Thin shell — all logic is in DiagnosticToolsPanelViewModel.
//     Exposes SnapshotRequested event so the plugin can delegate to
//     HeapSnapshotService without referencing DiagnosticsClient directly.
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.Plugins.DiagnosticTools.ViewModels;

namespace WpfHexEditor.Plugins.DiagnosticTools.Views;

/// <summary>
/// VS-style dockable diagnostics panel: CPU/memory graphs, event log, .NET counters.
/// </summary>
public sealed partial class DiagnosticToolsPanel : UserControl
{
    public event EventHandler? SnapshotRequested;

    // -----------------------------------------------------------------------

    public DiagnosticToolsPanel(DiagnosticToolsPanelViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // -----------------------------------------------------------------------

    private void OnSnapshotClick(object sender, System.Windows.RoutedEventArgs e)
        => SnapshotRequested?.Invoke(this, EventArgs.Empty);

    private void OnClearEventsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is DiagnosticToolsPanelViewModel vm)
            vm.Events.Clear();
    }
}
