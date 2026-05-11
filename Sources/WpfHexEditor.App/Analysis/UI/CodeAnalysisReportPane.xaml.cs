// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/CodeAnalysisReportPane.xaml.cs
// Description: Code-behind for the Code Analysis report document tab.
//              Handles navigation to file on double-click, export, and re-run.
// ==========================================================

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Analysis.Suppressions;
using WpfHexEditor.App.Analysis.UI.ContextMenus;
using WpfHexEditor.App.Analysis.UI.ViewModels;
using WpfHexEditor.App.Properties;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Analysis.UI;

public partial class CodeAnalysisReportPane : UserControl
{
    private readonly CodeAnalysisReportViewModel _vm;
    private readonly IDocumentHostService?        _docHost;
    private          Func<Task>?                  _reRunCallback;
    private          Func<Task>?                  _runSolutionCallback;
    private          Func<string, Task>?          _runFileCallback;
    private          SuppressionApplyService?     _suppress;

    public CodeAnalysisReportPane(
        CodeAnalysisReportViewModel vm,
        IDocumentHostService?       docHost = null)
    {
        InitializeComponent();
        _vm      = vm;
        _docHost = docHost;
        DataContext = vm;

        // Re-apply filter/sort + drop the snippet cache whenever a fresh report
        // arrives. DuplicationSummaryText is raised at the end of SetReport so
        // it's a stable, allocation-free signal.
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(CodeAnalysisReportViewModel.DuplicationSummaryText)) return;
            Controls.DuplicationCodePreview.InvalidateCache();
            if (IsLoaded) ApplyDupView();
        };

        // Phase 7 — keyboard shortcuts (F5 = re-run, Ctrl+F = focus search)
        InputBindings.Add(new KeyBinding(
            new RelayCmd(_ => _ = (_reRunCallback?.Invoke() ?? Task.CompletedTask)),
            new KeyGesture(Key.F5)));
        InputBindings.Add(new KeyBinding(
            new RelayCmd(_ => GlobalSearchBox.Focus()),
            new KeyGesture(Key.F, ModifierKeys.Control)));
    }

    internal void SetReRunCallback(Func<Task> rerun, Func<Task> runSolution, Func<string, Task> runFile)
    {
        _reRunCallback       = rerun;
        _runSolutionCallback = runSolution;
        _runFileCallback     = runFile;
    }

    internal void SetSuppressionService(SuppressionApplyService suppress)
        => _suppress = suppress;

    // Phase 7 — Context menu opening (call site: DataGrid.ContextMenuOpening)
    private void OnGridContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var builder = new AnalysisContextMenuBuilder(_docHost, _vm, scopedReRun: null, suppress: _suppress);
        grid.ContextMenu = grid.CurrentItem switch
        {
            ProjectMetrics p              => builder.BuildForProject(p),
            FileMetrics f                 => builder.BuildForFile(f),
            FileMetricsViewModel fm       => builder.BuildForFileVm(fm),
            MethodMetrics m               => builder.BuildForMethod(m, null),
            IssueViewModel iv             => builder.BuildForIssue(iv),
            CouplingMetrics c             => builder.BuildForCoupling(c),
            DuplicationGroupViewModel dvm => builder.BuildForDuplicationVm(dvm),
            DuplicationGroup d            => builder.BuildForDuplication(d),
            DeadSymbol ds                 => builder.BuildForDeadSymbol(ds),
            _                             => null,
        };
        if (grid.ContextMenu is null) e.Handled = true;
    }

    private sealed class RelayCmd : ICommand
    {
        private readonly Action<object?> _exec;
        public RelayCmd(Action<object?> exec) => _exec = exec;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p)    => _exec(p);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        switch (grid.CurrentItem)
        {
            case IssueViewModel issue when !string.IsNullOrEmpty(issue.FilePath):
                Navigate(issue.FilePath, issue.Line);
                break;
            case FileMetricsViewModel fm when !string.IsNullOrEmpty(fm.FilePath):
                Navigate(fm.FilePath, 1);
                break;
            case MethodMetrics m when !string.IsNullOrEmpty(m.FullyQualifiedName):
                // FullyQualifiedName does not carry file path — best effort via Issues
                break;
            case CouplingMetrics c when !string.IsNullOrEmpty(c.FilePath):
                Navigate(c.FilePath, c.Line);
                break;
            case DeadSymbol d when !string.IsNullOrEmpty(d.FilePath):
                Navigate(d.FilePath, d.Line);
                break;
        }
    }

    private void Navigate(string filePath, int line)
    {
        if (_docHost is null || !File.Exists(filePath)) return;
        _docHost.ActivateAndNavigateTo(filePath, Math.Max(1, line), 1);
    }

    // ── Re-run ───────────────────────────────────────────────────────────────

    private void OnReRunClicked(object sender, RoutedEventArgs e)
    {
        if (_reRunCallback is null) return;
        _ = _reRunCallback();
    }

    private void OnReRunDropdownClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is { } cm)
        {
            cm.PlacementTarget = btn;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            cm.IsOpen = true;
        }
    }

    private void OnRunSolutionClicked(object sender, RoutedEventArgs e)
    {
        if (_runSolutionCallback is null) return;
        _ = _runSolutionCallback();
    }

    // ── Treemap context menu ──────────────────────────────────────────────────

    private void OnTreemapContextMenuRequested(object? sender, Controls.TreemapContextMenuEventArgs e)
    {
        var cm = new ContextMenu();

        var openItem = new MenuItem { Header = AppResources.CodeAnalysis_Treemap_OpenFile };
        openItem.Click += (_, _) => Navigate(e.File.FilePath, 1);
        cm.Items.Add(openItem);

        var copyPath = new MenuItem { Header = AppResources.CodeAnalysis_Treemap_CopyPath };
        copyPath.Click += (_, _) => Clipboard.SetText(e.File.FilePath);
        cm.Items.Add(copyPath);

        var copyMetrics = new MenuItem { Header = AppResources.CodeAnalysis_Treemap_CopyMetrics };
        copyMetrics.Click += (_, _) => Clipboard.SetText(BuildFileMetricsText(e.File));
        cm.Items.Add(copyMetrics);

        cm.Items.Add(new Separator());

        var runFile = new MenuItem { Header = AppResources.CodeAnalysis_Treemap_RunOnFile };
        runFile.IsEnabled = _runFileCallback is not null;
        runFile.Click += (_, _) => { if (_runFileCallback is not null) _ = _runFileCallback(e.File.FilePath); };
        cm.Items.Add(runFile);

        cm.Items.Add(new Separator());

        var filterProj = new MenuItem { Header = AppResources.CodeAnalysis_Treemap_FilterProject };
        filterProj.Click += (_, _) => _vm.ProjectFilter = e.File.ProjectName;
        cm.Items.Add(filterProj);

        var highlight = new MenuItem { Header = AppResources.CodeAnalysis_Treemap_HighlightHotspots };
        highlight.Click += (_, _) => FileTreemap.ToggleHotspotMode();
        cm.Items.Add(highlight);

        cm.IsOpen = true;
    }

    private static string BuildFileMetricsText(FileMetrics f) =>
        $"""
         File      : {f.FileName}
         Project   : {f.ProjectName}
         Path      : {f.FilePath}
         Score     : {f.Score}/100
         LOC       : {f.TotalLines:N0}  ({f.CodeLines:N0} code · {f.CommentLines:N0} comments)
         Max CC    : {f.MaxCyclomaticComplexity}
         Max Cog   : {f.MaxCognitiveComplexity}
         MI        : {f.MaintainabilityIndex:F0}
         Methods   : {f.MethodCount}
         """;


    // ── Export ───────────────────────────────────────────────────────────────

    private void OnExportSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExportCombo.SelectedIndex <= 0) return;
        var fmt = ExportCombo.SelectedIndex switch
        {
            1 => "Markdown",
            2 => "CSV",
            3 => "SARIF",
            4 => "Checklist",
            _ => "CSV",
        };
        ExportCombo.SelectedIndex = 0;
        Export(fmt);
    }

    private void OnProjectFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CodeAnalysisReportViewModel vm
            && ProjectFilterCombo.SelectedItem is string sel)
            vm.ProjectFilter = sel == AppResources.CodeAnalysis_AllProjects ? string.Empty : sel;
    }

    private void OnTreemapItemActivated(object? sender, FileMetrics file)
    {
        if (string.IsNullOrEmpty(file.FilePath)) return;
        Navigate(file.FilePath, 1);
    }

    private void OnGroupByChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not CodeAnalysisReportViewModel vm) return;

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(vm.Issues);
        view.GroupDescriptions.Clear();

        // Index 0 = None, 1 = Severity, 2 = Rule, 3 = Project, 4 = File
        string? prop = GroupByCombo.SelectedIndex switch
        {
            1 => nameof(IssueViewModel.Severity),
            2 => nameof(IssueViewModel.Id),
            3 => nameof(IssueViewModel.ProjectName),
            4 => nameof(IssueViewModel.FileName),
            _ => null,
        };
        if (prop is not null)
            view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(prop));
    }

    private void Export(string format)
    {
        (string fileName, string filter) = format switch
        {
            "Markdown"  => (AppResources.CodeAnalysis_Export_FileName_Markdown,  AppResources.CodeAnalysis_Export_Filter_Markdown),
            "SARIF"     => (AppResources.CodeAnalysis_Export_FileName_Sarif,     AppResources.CodeAnalysis_Export_Filter_Sarif),
            "Checklist" => (AppResources.CodeAnalysis_Export_FileName_Checklist, AppResources.CodeAnalysis_Export_Filter_Markdown),
            _           => (AppResources.CodeAnalysis_Export_FileName_Csv,       AppResources.CodeAnalysis_Export_Filter_Csv),
        };
        var dlg = new SaveFileDialog { FileName = fileName, Filter = filter };
        if (dlg.ShowDialog() != true) return;

        if (format == "SARIF")
        {
            if (_vm.CurrentReport is not null)
                Services.SarifExporter.Export(_vm.CurrentReport, dlg.FileName);
            return;
        }
        if (format == "Checklist")
        {
            if (_vm.CurrentReport is not null)
                File.WriteAllText(dlg.FileName,
                    Services.CodeReviewChecklistBuilder.Build(_vm.CurrentReport), Encoding.UTF8);
            return;
        }

        var content = format == "Markdown" ? BuildMarkdown() : BuildCsv();
        File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
    }

    private string BuildMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine(AppResources.CodeAnalysis_Markdown_Title);
        sb.AppendLine(string.Format(AppResources.CodeAnalysis_Markdown_ScoreLine,
            _vm.Score, _vm.Grade, _vm.TrendingText));
        sb.AppendLine(string.Format(AppResources.CodeAnalysis_Markdown_FilesLine,
            _vm.TotalFiles, _vm.TotalLines, _vm.ProjectCount));
        sb.AppendLine();
        sb.AppendLine(AppResources.CodeAnalysis_Markdown_IssuesHeading);
        sb.AppendLine(AppResources.CodeAnalysis_Markdown_IssuesTableHeader);
        sb.AppendLine(AppResources.CodeAnalysis_Markdown_IssuesTableSeparator);
        foreach (var i in _vm.Issues)
            sb.AppendLine($"| {i.Severity} | {i.Id} | {i.Message.Replace("|","\\|")} | {i.FileName} | {i.Line} |");
        return sb.ToString();
    }

    private string BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(AppResources.CodeAnalysis_Csv_Header);
        foreach (var i in _vm.Issues)
            sb.AppendLine($"{i.Severity},{i.Id},{EscCsv(i.Message)},{EscCsv(i.FilePath)},{i.Line},{i.ProjectName}");
        return sb.ToString();
    }

    private static string EscCsv(string s)
        => s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

    // ── Filter events ────────────────────────────────────────────────────────

    private void OnSeverityFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not CodeAnalysisReportViewModel vm) return;
        // Map by SelectedIndex so localization of ComboBoxItem.Content does not
        // break the VM's stable English-side severity tokens.
        vm.SelectedSeverity = SeverityFilter.SelectedIndex switch
        {
            1 => "Error",
            2 => "Warning",
            3 => "Info",
            _ => "All",
        };
    }

    // ── Duplication tab (Phase 10B) ──────────────────────────────────────────

    private void OnDupFilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => ApplyDupView();

    private void OnDupSortChanged(object sender, SelectionChangedEventArgs e)
        => ApplyDupView();

    private void ApplyDupView()
    {
        if (DataContext is not CodeAnalysisReportViewModel vm) return;
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(vm.DuplicationGroups);

        string query = DupFilterBox?.Text?.Trim() ?? string.Empty;
        view.Filter = string.IsNullOrEmpty(query)
            ? null
            : new Predicate<object>(o =>
                o is DuplicationGroupViewModel d
                && d.Occurrences.Any(occ => occ.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase)));

        view.SortDescriptions.Clear();
        string prop = DupSortCombo?.SelectedIndex switch
        {
            1 => nameof(DuplicationGroupViewModel.TokenCount),
            2 => nameof(DuplicationGroupViewModel.OccurrenceCount),
            _ => nameof(DuplicationGroupViewModel.LineCount),
        };
        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(prop,
            System.ComponentModel.ListSortDirection.Descending));
    }

    private void OnDupExportClicked(object sender, RoutedEventArgs e)
    {
        if (_vm.CurrentReport is null) return;
        var dlg = new SaveFileDialog
        {
            FileName = AppResources.CodeAnalysis_Duplication_Export_FileName,
            Filter   = AppResources.CodeAnalysis_Export_Filter_Markdown,
        };
        if (dlg.ShowDialog() != true) return;
        var content = Services.DuplicationReportExporter.Build(_vm);
        File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
    }
}
