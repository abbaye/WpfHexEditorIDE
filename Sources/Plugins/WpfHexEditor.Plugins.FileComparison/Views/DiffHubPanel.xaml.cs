// Project      : WpfHexEditorControl
// File         : Views/DiffHubPanel.xaml.cs
// Description  : Code-behind for the simplified DiffHubPanel launcher.
//                Handles browse dialogs, drag-and-drop, and history re-open.
//                Comparison results now open as a DiffViewerDocument tab via
//                the CompareCompleted event on DiffHubViewModel.
// Architecture : Thin code-behind — delegates data work to DiffHubViewModel.

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Plugins.FileComparison.ViewModels;

namespace WpfHexEditor.Plugins.FileComparison.Views;

public sealed partial class DiffHubPanel : UserControl
{
    private readonly DiffHubViewModel _vm;

    /// <summary>
    /// Fired when the user double-clicks a history entry (to re-open it as a document tab).
    /// Also fired when CompareAsync completes — the plugin subscribes to open/refresh the tab.
    /// </summary>
    public event EventHandler<DiffEngineResult>? CompareCompleted;

    public DiffHubPanel()
    {
        InitializeComponent();
        _vm = new DiffHubViewModel();
        DataContext = _vm;

        // Forward VM event to the panel's public event (plugin subscribes here)
        _vm.CompareCompleted += (s, result) => CompareCompleted?.Invoke(this, result);

        // Ctrl+Enter shortcut to compare
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                _vm.CompareCommand.Execute(null);
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SuggestFile1(string path) => _vm.SuggestFile1(path);

    public void LoadHistory(IEnumerable<ComparisonHistoryEntry> history) => _vm.LoadHistory(history);

    /// <summary>
    /// Programmatically loads both file paths and starts the comparison.
    /// Used by the terminal <c>diff-open</c> command via the DiffServiceAdapter.
    /// </summary>
    public void OpenFiles(string leftPath, string rightPath)
    {
        _vm.File1Path = leftPath;
        _vm.File2Path = rightPath;
        _ = _vm.CompareAsync();
    }

    // ── Browse buttons ────────────────────────────────────────────────────────

    private void OnBrowseFile1_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFile("Select File 1 (Left)");
        if (path is not null) _vm.File1Path = path;
    }

    private void OnBrowseFile2_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFile("Select File 2 (Right)");
        if (path is not null) _vm.File2Path = path;
    }

    private static string? PickFile(string title)
    {
        var dlg = new OpenFileDialog { Title = title };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    // ── Drag-and-drop ─────────────────────────────────────────────────────────

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFile1Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _vm.File1Path = files[0];
    }

    private void OnFile2Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _vm.File2Path = files[0];
    }

    // ── History double-click ─────────────────────────────────────────────────

    private void OnHistoryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is ComparisonHistoryEntry entry)
        {
            _vm.File1Path = entry.LeftPath;
            _vm.File2Path = entry.RightPath;
            _ = _vm.CompareAsync();
        }
    }
}
