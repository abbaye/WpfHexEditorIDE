// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Dialogs/AttachToProcessDialog.xaml.cs
// Description:
//     VS-style "Attach to Process" dialog. Lists running processes, supports
//     name/title filtering. Returns the selected PID on OK.
// Architecture:
//     Standalone Window — no SDK dependencies. Called from DebuggerPlugin.
// ==========================================================

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.Debugger.Dialogs;

public sealed record ProcessEntry(int Pid, string Name, string Title, string Path);

public partial class AttachToProcessDialog : Window
{
    private readonly ObservableCollection<ProcessEntry> _all = [];
    private readonly ObservableCollection<ProcessEntry> _filtered = [];

    /// <summary>The selected PID, or 0 if none was chosen.</summary>
    public int SelectedPid { get; private set; }

    public AttachToProcessDialog()
    {
        InitializeComponent();
        ProcessList.ItemsSource = _filtered;
        LoadProcesses();
    }

    private void LoadProcesses()
    {
        _all.Clear();
        foreach (var p in Process.GetProcesses().OrderBy(p => p.ProcessName))
        {
            string title = string.Empty;
            string path  = string.Empty;
            try { title = p.MainWindowTitle; } catch { }
            try { path  = p.MainModule?.FileName ?? string.Empty; } catch { }
            _all.Add(new ProcessEntry(p.Id, p.ProcessName, title, path));
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = FilterBox?.Text ?? string.Empty;
        _filtered.Clear();
        foreach (var e in _all)
        {
            if (string.IsNullOrEmpty(filter)
                || e.Name.Contains(filter,  StringComparison.OrdinalIgnoreCase)
                || e.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || e.Pid.ToString().Contains(filter))
                _filtered.Add(e);
        }
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => AttachButton.IsEnabled = ProcessList.SelectedItem is ProcessEntry;

    private void OnRefresh(object sender, RoutedEventArgs e) => LoadProcesses();

    private void OnAttach(object sender, RoutedEventArgs e)
    {
        if (ProcessList.SelectedItem is ProcessEntry p)
        {
            SelectedPid = p.Pid;
            DialogResult = true;
        }
    }

    private void OnDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProcessList.SelectedItem is ProcessEntry p)
        {
            SelectedPid  = p.Pid;
            DialogResult = true;
        }
    }

    /// <summary>
    /// Shows the dialog and returns the chosen PID, or 0 if cancelled.
    /// </summary>
    public static int Show(Window? owner)
    {
        var dlg = new AttachToProcessDialog { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.SelectedPid : 0;
    }
}
