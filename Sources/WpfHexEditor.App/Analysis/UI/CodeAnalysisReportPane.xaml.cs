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
using WpfHexEditor.App.Analysis.UI.ViewModels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Analysis.UI;

public partial class CodeAnalysisReportPane : UserControl
{
    private readonly CodeAnalysisReportViewModel _vm;
    private readonly IDocumentHostService?        _docHost;
    private          Func<Task>?                  _reRunCallback;

    public CodeAnalysisReportPane(
        CodeAnalysisReportViewModel vm,
        IDocumentHostService?       docHost = null)
    {
        InitializeComponent();
        _vm      = vm;
        _docHost = docHost;
        DataContext = vm;
    }

    internal void SetReRunCallback(Func<Task> callback)
        => _reRunCallback = callback;

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

    // ── Export ───────────────────────────────────────────────────────────────

    private void OnExportSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExportCombo.SelectedIndex <= 0) return;
        var fmt = ExportCombo.SelectedIndex == 1 ? "Markdown" : "CSV";
        ExportCombo.SelectedIndex = 0;
        Export(fmt);
    }

    private void Export(string format)
    {
        var dlg = new SaveFileDialog
        {
            FileName = format == "Markdown" ? "code-analysis-report.md" : "code-analysis-report.csv",
            Filter   = format == "Markdown" ? "Markdown files (*.md)|*.md" : "CSV files (*.csv)|*.csv",
        };
        if (dlg.ShowDialog() != true) return;

        var content = format == "Markdown"
            ? BuildMarkdown()
            : BuildCsv();

        File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
    }

    private string BuildMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Code Analysis Report");
        sb.AppendLine($"Score: **{_vm.Score}/100** ({_vm.Grade})  Trending: {_vm.TrendingText}");
        sb.AppendLine($"Files: {_vm.TotalFiles}  LOC: {_vm.TotalLines:N0}  Projects: {_vm.ProjectCount}");
        sb.AppendLine();
        sb.AppendLine("## Issues");
        sb.AppendLine("| Severity | ID | Message | File | Line |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var i in _vm.Issues)
            sb.AppendLine($"| {i.Severity} | {i.Id} | {i.Message.Replace("|","\\|")} | {i.FileName} | {i.Line} |");
        return sb.ToString();
    }

    private string BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Severity,ID,Message,FilePath,Line,Project");
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
        if (DataContext is CodeAnalysisReportViewModel vm && SeverityFilter.SelectedItem is ComboBoxItem ci)
            vm.SelectedSeverity = ci.Content?.ToString() ?? "All";
    }
}
