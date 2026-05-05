// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WorkspaceFindReplacePanel.xaml.cs
// Description:
//     Dockable panel for solution-wide find & replace (Ctrl+Shift+H).
//     Searches all text files in the active solution; results are
//     shown in a GridView with file / line / col / preview columns.
//     Double-click or context menu to open file and navigate to line.
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Shell.Panels.Properties;
using WpfHexEditor.Editor.Core.Dialogs;
#pragma warning disable IDE0060

namespace WpfHexEditor.Shell.Panels.Panels;

/// <summary>Row model bound to the results <see cref="ListView"/>.</summary>
internal sealed class WorkspaceResultRow
{
    public string FilePath { get; init; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public int    Line     { get; init; }
    public int    Column   { get; init; }
    public string Preview  { get; init; } = "";
}

/// <summary>Navigation request raised when the user double-clicks a result row.</summary>
public sealed class WorkspaceNavigationRequest(string filePath, int line, int column)
{
    public string FilePath { get; } = filePath;
    public int    Line     { get; } = line;
    public int    Column   { get; } = column;
}

/// <summary>
/// Dockable solution-wide find &amp; replace panel.
/// </summary>
public partial class WorkspaceFindReplacePanel : UserControl
{
    private readonly ObservableCollection<WorkspaceResultRow> _rows = [];
    private CancellationTokenSource? _cts;

    /// <summary>Raised when the user double-clicks a result — open file and navigate to line.</summary>
    public event EventHandler<WorkspaceNavigationRequest>? NavigationRequested;

    public WorkspaceFindReplacePanel()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _rows;
    }

    // ── Find All ──────────────────────────────────────────────────────────────

    private async void OnFindAllClick(object sender, RoutedEventArgs e)
        => await RunSearchAsync();

    private async void OnFindBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await RunSearchAsync();
        if (e.Key == Key.Escape) OnCancelClick(sender, e);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private async Task RunSearchAsync()
    {
        var pattern = FindBox.Text;
        if (string.IsNullOrWhiteSpace(pattern)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        bool useRegex  = RegexCheck.IsChecked     == true;
        bool matchCase = MatchCaseCheck.IsChecked  == true;
        bool wholeWord = WholeWordCheck.IsChecked  == true;
        var  ct        = _cts.Token;

        SetSearchingState(true);

        try
        {
            var results = await Task.Run(
                () => WorkspaceFindReplaceService.SearchAsync(
                    pattern, useRegex, matchCase, wholeWord, ct),
                ct);

            _rows.Clear();
            foreach (var r in results)
                _rows.Add(new WorkspaceResultRow
                {
                    FilePath = r.FilePath,
                    Line     = r.Line,
                    Column   = r.Column,
                    Preview  = r.Preview,
                });

            int fileCount = results.Select(r => r.FilePath).Distinct().Count();
            StatusText.Text = results.Count == 0
                ? ShellPanelsResources.WorkspaceFindReplace_StatusNoMatches
                : string.Format(ShellPanelsResources.WorkspaceFindReplace_StatusMatches,
                                results.Count, fileCount);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = ShellPanelsResources.WorkspaceFindReplace_StatusReady;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SetSearchingState(false);
        }
    }

    // ── Replace All ───────────────────────────────────────────────────────────

    private async void OnReplaceAllClick(object sender, RoutedEventArgs e)
    {
        var pattern     = FindBox.Text;
        var replacement = ReplaceBox.Text;
        if (string.IsNullOrWhiteSpace(pattern)) return;

        var confirm = IdeMessageBox.Show(
            string.Format(ShellPanelsResources.WorkspaceFindReplace_ConfirmMessage, pattern, replacement),
            ShellPanelsResources.WorkspaceFindReplace_ConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        bool useRegex2  = RegexCheck.IsChecked     == true;
        bool matchCase2 = MatchCaseCheck.IsChecked  == true;
        bool wholeWord2 = WholeWordCheck.IsChecked  == true;
        var  ct2        = _cts.Token;

        SetSearchingState(true);
        StatusText.Text = ShellPanelsResources.WorkspaceFindReplace_StatusReplacing;

        try
        {
            var changed = await Task.Run(
                () => WorkspaceFindReplaceService.ReplaceAllAsync(
                    pattern, replacement,
                    useRegex2, matchCase2, wholeWord2, ct2),
                ct2);

            int total = changed.Sum(c => c.Count);
            StatusText.Text = total == 0
                ? ShellPanelsResources.WorkspaceFindReplace_StatusNoReplacements
                : string.Format(ShellPanelsResources.WorkspaceFindReplace_StatusReplaced,
                                total, changed.Count);

            if (total > 0)
            {
                _rows.Clear();
                foreach (var (file, count) in changed)
                    _rows.Add(new WorkspaceResultRow
                    {
                        FilePath = file,
                        Preview  = string.Format(
                            ShellPanelsResources.WorkspaceFindReplace_StatusReplaced,
                            count, 1),
                    });
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = ShellPanelsResources.WorkspaceFindReplace_StatusReady;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SetSearchingState(false);
        }
    }

    // ── Search state toggle ───────────────────────────────────────────────────

    private void SetSearchingState(bool searching)
    {
        SearchProgress.Visibility   = searching ? Visibility.Visible   : Visibility.Collapsed;
        FindAllBtn.Visibility       = searching ? Visibility.Collapsed : Visibility.Visible;
        CancelBtn.Visibility        = searching ? Visibility.Visible   : Visibility.Collapsed;
        FindAllBtn.IsEnabled        = !searching;
        ReplaceAllBtn.IsEnabled     = !searching;

        if (searching)
        {
            _rows.Clear();
            StatusText.Text = ShellPanelsResources.WorkspaceFindReplace_StatusSearching;
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnResultSelected(object sender, SelectionChangedEventArgs e) { }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
        => NavigateToSelected();

    private void NavigateToSelected()
    {
        if (ResultsList.SelectedItem is not WorkspaceResultRow row) return;
        NavigationRequested?.Invoke(this,
            new WorkspaceNavigationRequest(row.FilePath, row.Line, row.Column));
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private void OnCM_OpenFile(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is not WorkspaceResultRow row) return;
        NavigationRequested?.Invoke(this,
            new WorkspaceNavigationRequest(row.FilePath, 0, 0));
    }

    private void OnCM_OpenAtLine(object sender, RoutedEventArgs e)
        => NavigateToSelected();

    private void OnCM_CopyPath(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is not WorkspaceResultRow row) return;
        try { Clipboard.SetText(row.FilePath); } catch { }
    }

    private void OnCM_CopyPreview(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is not WorkspaceResultRow row) return;
        try { Clipboard.SetText(row.Preview); } catch { }
    }

    private void OnCM_ExcludeRow(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is WorkspaceResultRow row)
            _rows.Remove(row);
    }

    private void OnCM_ExcludeFile(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is not WorkspaceResultRow row) return;
        var toRemove = _rows.Where(r => r.FilePath == row.FilePath).ToList();
        foreach (var r in toRemove) _rows.Remove(r);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        StatusText.Text = ShellPanelsResources.WorkspaceFindReplace_StatusReady;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Pre-fills the search box and runs a find immediately (called from Ctrl+Shift+H).</summary>
    public async Task ActivateWithQueryAsync(string? query = null)
    {
        if (!string.IsNullOrEmpty(query))
            FindBox.Text = query;

        FindBox.Focus();
        FindBox.SelectAll();

        if (!string.IsNullOrEmpty(FindBox.Text))
            await RunSearchAsync();
    }
}
