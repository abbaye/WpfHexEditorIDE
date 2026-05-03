// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WorkspaceFindReplacePanel.xaml.cs
// Description:
//     Dockable panel for solution-wide find & replace (Ctrl+Shift+H).
//     Searches all text files in the active solution; results are
//     shown in a GridView with file / line / col / preview columns.
//     Double-click a result to open the file and navigate to the line.
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
    }

    private async Task RunSearchAsync()
    {
        var pattern = FindBox.Text;
        if (string.IsNullOrWhiteSpace(pattern)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _rows.Clear();
        FindAllBtn.IsEnabled    = false;
        ReplaceAllBtn.IsEnabled = false;
        StatusText.Text         = "Searching…";

        try
        {
            var results = await Task.Run(
                () => WorkspaceFindReplaceService.SearchAsync(
                    pattern,
                    RegexCheck.IsChecked   == true,
                    MatchCaseCheck.IsChecked == true,
                    WholeWordCheck.IsChecked == true,
                    _cts.Token),
                _cts.Token);

            foreach (var r in results)
                _rows.Add(new WorkspaceResultRow
                {
                    FilePath = r.FilePath,
                    Line     = r.Line,
                    Column   = r.Column,
                    Preview  = r.Preview,
                });

            StatusText.Text = results.Count == 0
                ? "No matches found."
                : $"{results.Count} match{(results.Count == 1 ? "" : "es")} in {results.Select(r => r.FilePath).Distinct().Count()} file(s).";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Search cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            FindAllBtn.IsEnabled    = true;
            ReplaceAllBtn.IsEnabled = true;
        }
    }

    // ── Replace All ───────────────────────────────────────────────────────────

    private async void OnReplaceAllClick(object sender, RoutedEventArgs e)
    {
        var pattern     = FindBox.Text;
        var replacement = ReplaceBox.Text;
        if (string.IsNullOrWhiteSpace(pattern)) return;

        var confirm = MessageBox.Show(
            $"Replace all occurrences of \"{pattern}\" with \"{replacement}\" across the entire solution?\n\nThis will modify files on disk.",
            "Replace All — Solution-wide",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        FindAllBtn.IsEnabled    = false;
        ReplaceAllBtn.IsEnabled = false;
        StatusText.Text         = "Replacing…";

        try
        {
            var changed = await Task.Run(
                () => WorkspaceFindReplaceService.ReplaceAllAsync(
                    pattern, replacement,
                    RegexCheck.IsChecked    == true,
                    MatchCaseCheck.IsChecked  == true,
                    WholeWordCheck.IsChecked  == true,
                    _cts.Token),
                _cts.Token);

            int total = changed.Sum(c => c.Count);
            StatusText.Text = total == 0
                ? "No replacements made."
                : $"Replaced {total} occurrence{(total == 1 ? "" : "s")} in {changed.Count} file(s).";

            // Refresh results after replace
            if (total > 0)
            {
                _rows.Clear();
                foreach (var (file, count) in changed)
                    _rows.Add(new WorkspaceResultRow
                    {
                        FilePath = file,
                        Preview  = $"{count} replacement{(count == 1 ? "" : "s")} made",
                    });
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Replace cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            FindAllBtn.IsEnabled    = true;
            ReplaceAllBtn.IsEnabled = true;
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnResultSelected(object sender, SelectionChangedEventArgs e) { }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is not WorkspaceResultRow row) return;
        NavigationRequested?.Invoke(this, new WorkspaceNavigationRequest(row.FilePath, row.Line, row.Column));
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        StatusText.Text = "Ready.";
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
