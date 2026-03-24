// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Views/UnitTestingPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Updated: 2026-03-24 (ADR-UT-03 — overkill upgrade)
// Description:
//     Code-behind for the Unit Testing Panel.
//     Binds the UnitTestingViewModel; delegates run/stop/clear/run-failed
//     to events; wires selection, filter, search, context menu, clipboard.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.UnitTesting.ViewModels;

namespace WpfHexEditor.Plugins.UnitTesting.Views;

/// <summary>
/// Dockable Unit Testing Panel.
/// </summary>
public partial class UnitTestingPanel : UserControl
{
    public event EventHandler?                  RunAllRequested;
    public event EventHandler?                  StopRequested;
    public event EventHandler<string?>?         RunFailedRequested;
    public event EventHandler<TestResultRow?>?  RunThisTestRequested;

    private UnitTestingViewModel Vm => (UnitTestingViewModel)DataContext;

    public UnitTestingPanel(UnitTestingViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UnitTestingViewModel.IsRunning))
                UpdateToolbarState(vm.IsRunning);
        };
    }

    // ── Toolbar handlers ─────────────────────────────────────────────────────

    private void OnRunAllClicked(object sender, RoutedEventArgs e)
        => RunAllRequested?.Invoke(this, EventArgs.Empty);

    private void OnStopClicked(object sender, RoutedEventArgs e)
        => StopRequested?.Invoke(this, EventArgs.Empty);

    private void OnClearClicked(object sender, RoutedEventArgs e)
        => Vm.Reset();

    private void OnRunFailedClicked(object sender, RoutedEventArgs e)
        => RunFailedRequested?.Invoke(this, null);

    // ── Filter bar ───────────────────────────────────────────────────────────

    private void OnFilterAll(object sender, RoutedEventArgs e)     => Vm.FilterMode = "All";
    private void OnFilterPassed(object sender, RoutedEventArgs e)  => Vm.FilterMode = "Passed";
    private void OnFilterFailed(object sender, RoutedEventArgs e)  => Vm.FilterMode = "Failed";
    private void OnFilterSkipped(object sender, RoutedEventArgs e) => Vm.FilterMode = "Skipped";

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        => Vm.SearchText = SearchBox.Text;

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        Vm.SearchText = string.Empty;
    }

    // ── Selection ────────────────────────────────────────────────────────────

    private void OnResultSelectionChanged(object sender, SelectionChangedEventArgs e)
        => Vm.SelectedResult = ResultsList.SelectedItem as TestResultRow;

    // ── Context menu ─────────────────────────────────────────────────────────

    private void OnRunThisTestClicked(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is TestResultRow row)
            RunThisTestRequested?.Invoke(this, row);
    }

    private void OnCopyTestNameClicked(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is TestResultRow row)
            Clipboard.SetText(row.Display);
    }

    private void OnCopyStackTraceClicked(object sender, RoutedEventArgs e)
    {
        var trace = Vm.SelectedResult?.StackTrace;
        if (!string.IsNullOrEmpty(trace))
            Clipboard.SetText(trace);
    }

    // ── Toolbar state sync ───────────────────────────────────────────────────

    private void UpdateToolbarState(bool isRunning)
    {
        RunButton.IsEnabled  = !isRunning;
        StopButton.IsEnabled =  isRunning;
    }
}
