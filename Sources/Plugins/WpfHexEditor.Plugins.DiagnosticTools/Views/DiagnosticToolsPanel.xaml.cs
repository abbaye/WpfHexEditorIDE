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

using System.Windows.Controls;
using WpfHexEditor.Plugins.DiagnosticTools.ViewModels;

namespace WpfHexEditor.Plugins.DiagnosticTools.Views;

/// <summary>
/// VS-style dockable diagnostics panel.
/// </summary>
public sealed partial class DiagnosticToolsPanel : UserControl
{
    public event EventHandler? SnapshotRequested;
    public event EventHandler? PauseResumeRequested;
    public event EventHandler? ExportRequested;

    // -----------------------------------------------------------------------

    public DiagnosticToolsPanel(DiagnosticToolsPanelViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // -----------------------------------------------------------------------

    private void OnSnapshotClick(object sender, System.Windows.RoutedEventArgs e)
        => SnapshotRequested?.Invoke(this, EventArgs.Empty);

    private void OnPauseResumeClick(object sender, System.Windows.RoutedEventArgs e)
        => PauseResumeRequested?.Invoke(this, EventArgs.Empty);

    private void OnExportClick(object sender, System.Windows.RoutedEventArgs e)
        => ExportRequested?.Invoke(this, EventArgs.Empty);

    private void OnClearEventsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is DiagnosticToolsPanelViewModel vm)
            vm.Events.Clear();
    }
}
