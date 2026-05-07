// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/ViewModels/CodeAnalysisReportViewModel.cs
// Description: Root ViewModel for the Code Analysis report document.
//              Wraps CodeAnalysisReport and exposes observable collections
//              for each tab. Notifies the UI when a new report arrives.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.UI.ViewModels;

public sealed class CodeAnalysisReportViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private CodeAnalysisReport? _report;
    private bool                _isRunning;
    private string              _statusText = "No analysis run yet.";

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasReport)); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool HasReport => _report is not null && !_isRunning;

    // ── Score ────────────────────────────────────────────────────────────────

    public int    Score          => _report?.Score.Score ?? 0;
    public string Grade          => _report?.Score.Grade ?? "—";
    public int    TrendingDelta  => _report?.Score.TrendingDelta ?? 0;
    public string TrendingText   => TrendingDelta > 0 ? $"▲ +{TrendingDelta}" : TrendingDelta < 0 ? $"▼ {TrendingDelta}" : "—";
    public int    TotalFiles     => _report?.TotalFiles ?? 0;
    public int    TotalLines     => _report?.TotalLines ?? 0;
    public int    ProjectCount   => _report?.ProjectCount ?? 0;

    public int    VolumeScore     => _report?.Score.VolumeScore ?? 0;
    public int    ComplexityScore => _report?.Score.ComplexityScore ?? 0;
    public int    CouplingScore   => _report?.Score.CouplingScore ?? 0;
    public int    DuplicationScore => _report?.Score.DuplicationScore ?? 0;
    public int    DeadCodeScore   => _report?.Score.DeadCodeScore ?? 0;
    public int    ConventionScore => _report?.Score.ConventionScore ?? 0;

    // ── Tab collections ──────────────────────────────────────────────────────

    public ObservableCollection<ProjectMetrics>      Projects     { get; } = [];
    public ObservableCollection<IssueViewModel>      Issues       { get; } = [];
    public ObservableCollection<MethodMetrics>       TopMethods   { get; } = [];
    public ObservableCollection<CouplingMetrics>     TopCouplings { get; } = [];
    public ObservableCollection<DuplicationGroup>    Duplications { get; } = [];
    public ObservableCollection<DeadSymbol>          DeadSymbols  { get; } = [];
    public ObservableCollection<FileMetricsViewModel> WorstFiles  { get; } = [];

    // ── Filter ───────────────────────────────────────────────────────────────

    private string _issueFilter = string.Empty;
    public string IssueFilter
    {
        get => _issueFilter;
        set { _issueFilter = value; OnPropertyChanged(); RefreshIssues(); }
    }

    private string _selectedSeverity = "All";
    public string SelectedSeverity
    {
        get => _selectedSeverity;
        set { _selectedSeverity = value; OnPropertyChanged(); RefreshIssues(); }
    }

    // ── Update ───────────────────────────────────────────────────────────────

    public void SetReport(CodeAnalysisReport report)
    {
        _report    = report;
        IsRunning  = false;
        StatusText = $"Analysis complete — {report.Timestamp:g}";

        Projects.Clear();
        foreach (var p in report.Projects) Projects.Add(p);

        RefreshIssues();

        TopMethods.Clear();
        foreach (var m in report.Projects
            .SelectMany(p => p.Files)
            .SelectMany(f => f.Methods)
            .OrderByDescending(m => m.CyclomaticComplexity)
            .Take(50))
            TopMethods.Add(m);

        TopCouplings.Clear();
        foreach (var c in report.Projects
            .SelectMany(p => p.Files)
            .SelectMany(f => f.Couplings)
            .OrderByDescending(c => c.Instability)
            .Take(50))
            TopCouplings.Add(c);

        Duplications.Clear();
        foreach (var d in report.Duplications) Duplications.Add(d);

        DeadSymbols.Clear();
        foreach (var d in report.DeadSymbols) DeadSymbols.Add(d);

        WorstFiles.Clear();
        foreach (var f in report.Score.WorstFiles)
            WorstFiles.Add(new FileMetricsViewModel(f));

        OnPropertyChanged(nameof(Score));
        OnPropertyChanged(nameof(Grade));
        OnPropertyChanged(nameof(TrendingDelta));
        OnPropertyChanged(nameof(TrendingText));
        OnPropertyChanged(nameof(TotalFiles));
        OnPropertyChanged(nameof(TotalLines));
        OnPropertyChanged(nameof(ProjectCount));
        OnPropertyChanged(nameof(VolumeScore));
        OnPropertyChanged(nameof(ComplexityScore));
        OnPropertyChanged(nameof(CouplingScore));
        OnPropertyChanged(nameof(DuplicationScore));
        OnPropertyChanged(nameof(DeadCodeScore));
        OnPropertyChanged(nameof(ConventionScore));
        OnPropertyChanged(nameof(HasReport));
    }

    private void RefreshIssues()
    {
        Issues.Clear();
        if (_report is null) return;

        var filtered = _report.Diagnostics
            .Where(d => string.IsNullOrEmpty(_issueFilter)
                     || d.Message.Contains(_issueFilter, StringComparison.OrdinalIgnoreCase)
                     || d.Id.Contains(_issueFilter, StringComparison.OrdinalIgnoreCase)
                     || d.FilePath.Contains(_issueFilter, StringComparison.OrdinalIgnoreCase))
            .Where(d => _selectedSeverity == "All"
                     || d.Severity.ToString() == _selectedSeverity);

        foreach (var d in filtered)
            Issues.Add(new IssueViewModel(d));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
