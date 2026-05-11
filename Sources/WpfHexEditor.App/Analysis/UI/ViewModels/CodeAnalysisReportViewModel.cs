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
using WpfHexEditor.App.Properties;

namespace WpfHexEditor.App.Analysis.UI.ViewModels;

public sealed class CodeAnalysisReportViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private CodeAnalysisReport? _report;
    private bool                _isRunning;
    private string              _statusText = "No analysis run yet.";
    private string              _scopeLabel = string.Empty;
    private IReadOnlyList<HistoryEntry> _history = [];

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

    public string ScopeLabel
    {
        get => _scopeLabel;
        private set { _scopeLabel = value; OnPropertyChanged(); }
    }

    /// <summary>Underlying report (Phase 7/8 — for export, AI prompt, drill-down).</summary>
    public CodeAnalysisReport? CurrentReport => _report;

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
    public ObservableCollection<DuplicationGroupViewModel> DuplicationGroups { get; } = [];
    public ObservableCollection<DeadSymbol>          DeadSymbols  { get; } = [];
    public ObservableCollection<FileMetricsViewModel> WorstFiles  { get; } = [];

    // ── Dependencies / Trends tabs ───────────────────────────────────────────

    public ObservableCollection<WpfHexEditor.App.Analysis.UI.Controls.DependencyNode> DependencyNodes { get; } = [];
    public ObservableCollection<WpfHexEditor.App.Analysis.UI.Controls.DependencyEdge> DependencyEdges { get; } = [];
    public ObservableCollection<WpfHexEditor.App.Analysis.UI.Controls.TrendChartSeries> TrendSeries   { get; } = [];

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

    private string _projectFilter = string.Empty;
    /// <summary>Empty = all projects. Set to a project name to filter every tab.</summary>
    public string ProjectFilter
    {
        get => _projectFilter;
        set { _projectFilter = value ?? string.Empty; OnPropertyChanged(); RefreshIssues(); OnPropertyChanged(nameof(HasProjectFilter)); }
    }
    public bool HasProjectFilter => !string.IsNullOrEmpty(_projectFilter);

    /// <summary>Cross-tab search query (file name / method name / issue id or message).</summary>
    private string _globalSearch = string.Empty;
    public string GlobalSearch
    {
        get => _globalSearch;
        set { _globalSearch = value ?? string.Empty; OnPropertyChanged(); RefreshIssues(); }
    }

    private string _groupByMode = "None"; // None / Severity / Rule / Project / File
    public string GroupByMode
    {
        get => _groupByMode;
        set { _groupByMode = value ?? "None"; OnPropertyChanged(); }
    }

    public IReadOnlyList<string> AvailableProjects =>
        Projects.Select(p => p.ProjectName).Prepend(AppResources.CodeAnalysis_AllProjects).ToList();

    /// <summary>AI insights markdown (Phase 8).</summary>
    private string _aiInsights = string.Empty;
    public string AiInsights
    {
        get => _aiInsights;
        set { _aiInsights = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(HasAiInsights)); }
    }
    public bool HasAiInsights => !string.IsNullOrEmpty(_aiInsights);

    /// <summary>Phase 5 — last N snapshots for sparkline / trending tab.</summary>
    public IReadOnlyList<int> RecentScores { get; private set; } = [];

    public void SetScope(AnalysisScope scope, string path)
    {
        var name = scope switch
        {
            AnalysisScope.Solution => string.Format(AppResources.CodeAnalysis_Scope_Solution, System.IO.Path.GetFileName(path.TrimEnd('\\', '/'))),
            AnalysisScope.Project  => string.Format(AppResources.CodeAnalysis_Scope_Project,  System.IO.Path.GetFileNameWithoutExtension(path)),
            _                      => string.Format(AppResources.CodeAnalysis_Scope_File,      System.IO.Path.GetFileName(path)),
        };
        ScopeLabel = name;
    }

    public void SetRecentScores(IReadOnlyList<int> scores)
    {
        RecentScores = scores;
        OnPropertyChanged(nameof(RecentScores));
    }

    // ── History (Phase 10 — trending sparkline) ──────────────────────────────

    /// <summary>Latest persisted snapshots — ordered ascending by Timestamp.</summary>
    public IReadOnlyList<HistoryEntry> History => _history;

    /// <summary>Scores as doubles for the Sparkline ItemsSource binding.</summary>
    public IReadOnlyList<double> HistoryScores =>
        _history.Select(e => (double)e.Score).ToList();

    public bool HasHistory => _history.Count >= 2;

    // ── Duplication aggregates (Phase 10B) ───────────────────────────────────
    // Cached field — Sum() runs only once in SetReport, not on every binding access.

    private int _totalDuplicatedLines;
    public int TotalDuplicatedLines => _totalDuplicatedLines;

    public double DuplicationRatioPercent =>
        TotalLines <= 0 ? 0 : (double)_totalDuplicatedLines / TotalLines * 100;

    public string DuplicationSummaryText =>
        string.Format(AppResources.CodeAnalysis_Duplication_Summary,
            DuplicationGroups.Count, _totalDuplicatedLines, DuplicationRatioPercent);

    public string HistorySummaryText
    {
        get
        {
            if (_history.Count == 0)
                return AppResources.CodeAnalysis_History_NoData;
            int days = _history.Count == 1
                ? 0
                : Math.Max(1, (int)(_history[^1].Timestamp - _history[0].Timestamp).TotalDays);
            int delta = _history.Count >= 2 ? _history[^1].Score - _history[0].Score : 0;
            string trend = delta > 0 ? $"▲ +{delta}" : delta < 0 ? $"▼ {delta}" : "—";
            return string.Format(AppResources.CodeAnalysis_History_Summary, _history.Count, days, trend);
        }
    }

    public void SetHistory(IReadOnlyList<HistoryEntry> entries)
    {
        _history = entries ?? [];
        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(HistoryScores));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(HistorySummaryText));
    }

    /// <summary>Phase 4 — cyclic project deps.</summary>
    public ObservableCollection<ProjectCycleInfo> ProjectCycles { get; } = [];

    /// <summary>Phase 3 — flat list of all files across projects (treemap source).</summary>
    public IReadOnlyList<FileMetrics> AllFiles =>
        _report?.Projects.SelectMany(p => p.Files).ToList() ?? [];

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

        DuplicationGroups.Clear();
        foreach (var d in report.Duplications)
            DuplicationGroups.Add(new DuplicationGroupViewModel(d));

        // Recompute cached aggregate from the fresh groups.
        _totalDuplicatedLines = DuplicationGroups.Sum(g => g.DuplicatedLines);

        DeadSymbols.Clear();
        foreach (var d in report.DeadSymbols) DeadSymbols.Add(d);

        ProjectCycles.Clear();
        foreach (var c in report.ProjectCycles) ProjectCycles.Add(c);

        WorstFiles.Clear();
        foreach (var f in report.Score.WorstFiles)
            WorstFiles.Add(new FileMetricsViewModel(f));

        // Dependency graph — nodes = types, edges = efferent dependencies.
        DependencyNodes.Clear();
        DependencyEdges.Clear();
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in TopCouplings)
        {
            if (nodeIds.Add(c.TypeName))
                DependencyNodes.Add(new WpfHexEditor.App.Analysis.UI.Controls.DependencyNode
                {
                    Id     = c.TypeName,
                    Label  = ShortName(c.TypeName),
                    Tag    = c.FilePath,
                    Weight = Math.Max(1, c.Ca + c.Ce),
                });
        }
        foreach (var c in TopCouplings)
            foreach (var dep in c.DependsOn)
                if (nodeIds.Contains(dep))
                    DependencyEdges.Add(new WpfHexEditor.App.Analysis.UI.Controls.DependencyEdge(c.TypeName, dep));

        // Trend series — read from history entries (most recent first → reverse for chronological).
        TrendSeries.Clear();
        if (_history.Count > 0)
        {
            var chrono = _history.Reverse().ToList();
            TrendSeries.Add(new WpfHexEditor.App.Analysis.UI.Controls.TrendChartSeries
            {
                Label  = "Quality",
                Stroke = System.Windows.Media.Brushes.SteelBlue,
                Values = chrono.Select(h => (double)h.Score).ToList(),
            });
            TrendSeries.Add(new WpfHexEditor.App.Analysis.UI.Controls.TrendChartSeries
            {
                Label  = "Files",
                Stroke = System.Windows.Media.Brushes.SeaGreen,
                Values = chrono.Select(h => (double)h.TotalFiles).ToList(),
            });
            TrendSeries.Add(new WpfHexEditor.App.Analysis.UI.Controls.TrendChartSeries
            {
                Label  = "Errors",
                Stroke = System.Windows.Media.Brushes.Crimson,
                Values = chrono.Select(h => (double)h.Errors).ToList(),
            });
            TrendSeries.Add(new WpfHexEditor.App.Analysis.UI.Controls.TrendChartSeries
            {
                Label  = "Warnings",
                Stroke = System.Windows.Media.Brushes.DarkOrange,
                Values = chrono.Select(h => (double)h.Warnings).ToList(),
            });
        }

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
        OnPropertyChanged(nameof(TotalDuplicatedLines));
        OnPropertyChanged(nameof(DuplicationRatioPercent));
        OnPropertyChanged(nameof(DuplicationSummaryText));
        OnPropertyChanged(nameof(DeadCodeScore));
        OnPropertyChanged(nameof(ConventionScore));
        OnPropertyChanged(nameof(HasReport));
        OnPropertyChanged(nameof(AvailableProjects));
        OnPropertyChanged(nameof(AllFiles));
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
                     || d.Severity.ToString() == _selectedSeverity)
            .Where(d => string.IsNullOrEmpty(_projectFilter)
                     || _projectFilter == "(All projects)"
                     || string.Equals(d.ProjectName, _projectFilter, StringComparison.Ordinal))
            .Where(d => string.IsNullOrEmpty(_globalSearch)
                     || d.Message.Contains(_globalSearch, StringComparison.OrdinalIgnoreCase)
                     || d.Id.Contains(_globalSearch, StringComparison.OrdinalIgnoreCase)
                     || d.FilePath.Contains(_globalSearch, StringComparison.OrdinalIgnoreCase));

        foreach (var d in filtered)
            Issues.Add(new IssueViewModel(d));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Returns the unqualified portion of a fully-qualified type name.</summary>
    private static string ShortName(string fqn)
    {
        if (string.IsNullOrEmpty(fqn)) return fqn;
        var dot = fqn.LastIndexOf('.');
        return dot >= 0 && dot < fqn.Length - 1 ? fqn[(dot + 1)..] : fqn;
    }
}
