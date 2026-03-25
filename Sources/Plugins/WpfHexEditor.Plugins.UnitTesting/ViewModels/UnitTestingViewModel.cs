// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: ViewModels/UnitTestingViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Updated: 2026-03-24 (ADR-UT-07 — VS-style TreeView hierarchy)
// Description:
//     ViewModel for the Unit Testing Panel.
//     Hierarchical test tree: TestProjectNode → TestClassNode → TestResultRow.
//     Filtering via IsVisible property on each node (DataTrigger in XAML).
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using WpfHexEditor.Plugins.UnitTesting.Models;
using WpfHexEditor.Plugins.UnitTesting.Options;
using System.Linq;

namespace WpfHexEditor.Plugins.UnitTesting.ViewModels;

/// <summary>
/// MVVM ViewModel for the UnitTestingPanel.
/// Test results are organized as a 3-level tree: Project → Class → TestMethod.
/// </summary>
public sealed class UnitTestingViewModel : INotifyPropertyChanged
{
    // ── Tree ─────────────────────────────────────────────────────────────────

    public ObservableCollection<TestProjectNode> ProjectNodes { get; } = [];

    private TestProjectNode? _runningProject;

    // ── Status ───────────────────────────────────────────────────────────────

    private string _statusText = "Ready";
    private bool   _isRunning;
    private int    _passCount;
    private int    _failCount;
    private int    _skipCount;

    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            Set(ref _isRunning, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRunFailed)));
        }
    }

    public int PassCount
    {
        get => _passCount;
        set { Set(ref _passCount, value); RaiseCounters(); }
    }

    public int FailCount
    {
        get => _failCount;
        set { Set(ref _failCount, value); RaiseCounters(); }
    }

    public int SkipCount
    {
        get => _skipCount;
        set { Set(ref _skipCount, value); RaiseCounters(); }
    }

    public int  TotalCount      => PassCount + FailCount + SkipCount;
    public bool ShowRatioBarNow => ShowRatioBar && (PassCount + FailCount + SkipCount > 0);

    private void RaiseCounters()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRunFailed)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowRatioBarNow)));
    }

    // ── Selection + detail ───────────────────────────────────────────────────

    private object? _selectedNode;

    /// <summary>
    /// Currently selected tree node — can be <see cref="TestProjectNode"/>,
    /// <see cref="TestClassNode"/>, or <see cref="TestResultRow"/>.
    /// Drives the context-sensitive detail pane (ADR-UT-12).
    /// </summary>
    public object? SelectedNode
    {
        get => _selectedNode;
        set
        {
            Set(ref _selectedNode, value);
            SelectedResult = value as TestResultRow;
        }
    }

    private TestResultRow? _selectedResult;

    public TestResultRow? SelectedResult
    {
        get => _selectedResult;
        set => Set(ref _selectedResult, value);
    }

    // ── Filter / Search ──────────────────────────────────────────────────────

    private string _filterMode = "All";
    private string _searchText = string.Empty;

    public string FilterMode
    {
        get => _filterMode;
        set
        {
            Set(ref _filterMode, value);
            RaiseFilterFlags();
            ApplyFilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { Set(ref _searchText, value); ApplyFilter(); }
    }

    public bool FilterAll     => _filterMode == "All";
    public bool FilterPassed  => _filterMode == "Passed";
    public bool FilterFailed  => _filterMode == "Failed";
    public bool FilterSkipped => _filterMode == "Skipped";

    public bool CanRunFailed => FailCount > 0 && !IsRunning;

    private void RaiseFilterFlags()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterAll)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterPassed)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterFailed)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterSkipped)));
    }

    // ── Layout options ───────────────────────────────────────────────────────

    private bool _showRatioBar = UnitTestingOptions.Instance.ShowRatioBar;

    public bool ShowRatioBar
    {
        get => _showRatioBar;
        set
        {
            Set(ref _showRatioBar, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowRatioBarNow)));
        }
    }

    // ── Constructor ──────────────────────────────────────────────────────────

    public UnitTestingViewModel() => ApplyOptions();

    /// <summary>Re-reads options and applies ShowRatioBar.</summary>
    public void ApplyOptions()
    {
        ShowRatioBar = UnitTestingOptions.Instance.ShowRatioBar;
    }

    // ── Mutation helpers ─────────────────────────────────────────────────────

    /// <summary>Clears all results and resets counters.</summary>
    public void Reset()
    {
        _runningProject = null;
        ProjectNodes.Clear();
        PassCount      = FailCount = SkipCount = 0;
        StatusText     = "Ready";
        IsRunning      = false;
        SelectedNode   = null;
    }

    /// <summary>
    /// Removes all NotRun rows from the tree (stale discovered tests).
    /// Preserves Passed/Failed/Skipped rows and their counters.
    /// Removes empty class nodes and empty/not-running project nodes.
    /// Called before re-discovering tests so Refresh always reflects current source.
    /// </summary>
    public void ClearNotRunTests()
    {
        foreach (var proj in ProjectNodes.ToList())
        {
            // Remember whether this node already had discovered content.
            // Freshly-added empty nodes (no classes yet) must NOT be pruned —
            // AddDiscoveredTests will populate them right after this call.
            bool hadClasses = proj.Classes.Count > 0;

            foreach (var cls in proj.Classes.ToList())
            {
                var stale = cls.Tests.Where(t => t.Outcome == TestOutcome.NotRun).ToList();
                foreach (var t in stale)
                {
                    cls.Tests.Remove(t);
                    cls.NotRunCount--;
                    proj.NotRunCount--;
                }
                if (cls.Tests.Count == 0)
                    proj.Classes.Remove(cls);
            }

            // Only remove project nodes that previously had classes and are now fully empty.
            if (hadClasses && proj.Classes.Count == 0 && !proj.IsRunning)
                ProjectNodes.Remove(proj);
        }
    }

    /// <summary>
    /// Updates a single test row in-place: adjusts counters for the old outcome,
    /// calls <see cref="TestResultRow.Update"/>, adjusts counters for the new outcome,
    /// and re-applies the current filter. Used by Run-This-Test so the tree is not reset.
    /// </summary>
    public void ReplaceResult(TestResultRow row, TestResult result)
    {
        foreach (var proj in ProjectNodes)
        {
            foreach (var cls in proj.Classes)
            {
                if (!cls.Tests.Contains(row)) continue;

                // Decrement counters for previous outcome.
                switch (row.Outcome)
                {
                    case TestOutcome.Passed:  cls.PassCount--;  proj.PassCount--;  PassCount--;  break;
                    case TestOutcome.Failed:  cls.FailCount--;  proj.FailCount--;  FailCount--;  break;
                    case TestOutcome.Skipped: cls.SkipCount--;  proj.SkipCount--;  SkipCount--;  break;
                    case TestOutcome.NotRun:  cls.NotRunCount--; proj.NotRunCount--; break;
                }

                var oldDurationMs = row.DurationMs;
                row.Update(result);

                // Increment counters for new outcome.
                switch (result.Outcome)
                {
                    case TestOutcome.Passed:  cls.PassCount++;  proj.PassCount++;  PassCount++;  break;
                    case TestOutcome.Failed:  cls.FailCount++;  proj.FailCount++;  FailCount++;  break;
                    default:                  cls.SkipCount++;  proj.SkipCount++;  SkipCount++;  break;
                }

                var newDurationMs = (int)result.Duration.TotalMilliseconds;
                cls.TotalDurationMs  += newDurationMs - oldDurationMs;
                proj.TotalDurationMs += newDurationMs - oldDurationMs;

                ApplyFilter();
                return;
            }
        }
    }

    /// <summary>Marks the project node as running (creates it if not present).</summary>
    public void AddRunningPlaceholder(string projectName)
    {
        var node = ProjectNodes.FirstOrDefault(p => p.ProjectName == projectName)
                ?? CreateAndAddProject(projectName);
        node.IsRunning  = true;
        _runningProject = node;
    }

    /// <summary>Clears the running state on the current project node.</summary>
    public void RemoveRunningPlaceholder()
    {
        if (_runningProject is not null) _runningProject.IsRunning = false;
        _runningProject = null;
    }

    /// <summary>
    /// Adds discovered (not-yet-run) tests to the tree without affecting counters.
    /// Idempotent — skips tests already present by display name.
    /// </summary>
    public void AddDiscoveredTests(IEnumerable<DiscoveredTest> tests)
    {
        foreach (var dt in tests)
        {
            var proj = ProjectNodes.FirstOrDefault(p => p.ProjectName == dt.ProjectName)
                    ?? CreateAndAddProject(dt.ProjectName);
            var cls  = proj.Classes.FirstOrDefault(c => c.FullClassName == dt.ClassName)
                    ?? CreateAndAddClass(proj, dt.ClassName);

            if (!cls.Tests.Any(t => t.Display == dt.TestName))
            {
                cls.Tests.Add(new TestResultRow(dt));
                cls.NotRunCount++;
                proj.NotRunCount++;
            }
        }
        ApplyFilter();
    }

    /// <summary>Groups results into the tree by ProjectName → ClassName → TestName.
    /// Merges into existing NotRun rows (discovered tests) when possible.</summary>
    public void AddResults(IEnumerable<TestResult> results)
    {
        foreach (var r in results)
        {
            var proj = ProjectNodes.FirstOrDefault(p => p.ProjectName == r.ProjectName)
                    ?? CreateAndAddProject(r.ProjectName);
            var cls  = proj.Classes.FirstOrDefault(c => c.FullClassName == r.ClassName)
                    ?? CreateAndAddClass(proj, r.ClassName);

            // Update existing discovered row in-place, or add a new one.
            var existing = cls.Tests.FirstOrDefault(
                t => t.Display == r.TestName && t.Outcome == TestOutcome.NotRun);
            if (existing is not null)
            {
                existing.Update(r);
                cls.NotRunCount--;
                proj.NotRunCount--;
            }
            else
            {
                cls.Tests.Add(new TestResultRow(r));
            }

            switch (r.Outcome)
            {
                case TestOutcome.Passed:  cls.PassCount++; proj.PassCount++; PassCount++; break;
                case TestOutcome.Failed:  cls.FailCount++; proj.FailCount++; FailCount++; break;
                default:                  cls.SkipCount++; proj.SkipCount++; SkipCount++; break;
            }

            var dMs = (int)r.Duration.TotalMilliseconds;
            cls.TotalDurationMs  += dMs;
            proj.TotalDurationMs += dMs;
        }
        ApplyFilter();
    }

    /// <summary>All leaf <see cref="TestResultRow"/> instances (for Run Failed filter building).</summary>
    public IEnumerable<TestResultRow> AllLeafResults =>
        ProjectNodes.SelectMany(p => p.Classes.SelectMany(c => c.Tests));

    /// <summary>
    /// Adds newly-discovered project nodes; removes empty stale nodes no longer in the solution.
    /// Does NOT clear results of projects that have already been run.
    /// First <paramref name="autoExpandCount"/> projects are expanded; the rest are collapsed.
    /// </summary>
    public void DiscoverProjects(IEnumerable<string> projectNames, int autoExpandCount = 0)
    {
        var names = projectNames.ToList();
        int i = 0;
        foreach (var name in names)
        {
            if (!ProjectNodes.Any(p => p.ProjectName == name))
                ProjectNodes.Add(new TestProjectNode(name) { IsExpanded = i < autoExpandCount });
            i++;
        }

        // Remove stale nodes that have no results (user never ran them).
        var stale = ProjectNodes
            .Where(p => !names.Contains(p.ProjectName) && p.TotalCount == 0 && !p.IsRunning)
            .ToList();
        foreach (var node in stale)
            ProjectNodes.Remove(node);
    }

    /// <summary>Expands or collapses all project and class nodes.</summary>
    public void SetAllExpanded(bool expanded)
    {
        foreach (var proj in ProjectNodes)
        {
            proj.IsExpanded = expanded;
            foreach (var cls in proj.Classes)
                cls.IsExpanded = expanded;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private TestProjectNode CreateAndAddProject(string name)
    {
        var node = new TestProjectNode(name);
        ProjectNodes.Add(node);
        return node;
    }

    private static TestClassNode CreateAndAddClass(TestProjectNode proj, string fullName)
    {
        var node = new TestClassNode(fullName);
        proj.Classes.Add(node);
        return node;
    }

    /// <summary>
    /// Walks the tree and sets IsVisible on every node based on current filter/search.
    /// Supports structured <c>key:value</c> queries (état/nom/classe/projet/erreur/espace).
    /// </summary>
    private void ApplyFilter()
    {
        var (filterKey, filterValue) = ParseSearchToken(_searchText);
        bool hasFilter = !string.IsNullOrWhiteSpace(filterValue);

        foreach (var proj in ProjectNodes)
        {
            bool anyProjVisible = false;
            foreach (var cls in proj.Classes)
            {
                bool anyClsVisible = false;
                foreach (var test in cls.Tests)
                {
                    bool matchOutcome = _filterMode == "All"
                        || test.Outcome == TestOutcome.NotRun
                        || test.Outcome.ToString() == _filterMode;

                    bool matchSearch = !hasFilter || filterKey switch
                    {
                        "état"   or "etat"  => MatchOutcome(test, filterValue),
                        "nom"               => test.Display.Contains(filterValue, StringComparison.OrdinalIgnoreCase),
                        "classe"            => cls.FullClassName.Contains(filterValue, StringComparison.OrdinalIgnoreCase),
                        "projet"            => proj.ProjectName.Contains(filterValue, StringComparison.OrdinalIgnoreCase),
                        "erreur"            => test.ErrorMessage?.Contains(filterValue, StringComparison.OrdinalIgnoreCase) == true,
                        "espace"            => GetNamespace(cls.FullClassName).Contains(filterValue, StringComparison.OrdinalIgnoreCase),
                        _                   => test.Display.Contains(filterValue, StringComparison.OrdinalIgnoreCase)
                                               || cls.FullClassName.Contains(filterValue, StringComparison.OrdinalIgnoreCase),
                    };

                    test.IsVisible = matchOutcome && matchSearch;
                    if (test.IsVisible) anyClsVisible = true;
                }
                cls.IsVisible = anyClsVisible;
                if (anyClsVisible) anyProjVisible = true;
            }
            proj.IsVisible = anyProjVisible || proj.IsRunning || proj.Classes.Count == 0;
        }
    }

    private static (string key, string value) ParseSearchToken(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (string.Empty, string.Empty);
        var colon = text.IndexOf(':');
        if (colon <= 0) return (string.Empty, text.Trim());
        return (text[..colon].Trim().ToLowerInvariant(), text[(colon + 1)..].Trim());
    }

    private static bool MatchOutcome(TestResultRow test, string value) =>
        value.ToLowerInvariant() switch
        {
            "réussite" or "reussite" or "passed" or "ok"  => test.Outcome == TestOutcome.Passed,
            "échec"    or "echec"    or "failed"  or "fail" => test.Outcome == TestOutcome.Failed,
            "ignoré"   or "ignore"   or "skipped" or "skip" => test.Outcome == TestOutcome.Skipped,
            _                                               => test.Outcome.ToString()
                                                               .Contains(value, StringComparison.OrdinalIgnoreCase),
        };

    private static string GetNamespace(string fullClassName)
    {
        var dot = fullClassName.LastIndexOf('.');
        return dot > 0 ? fullClassName[..dot] : string.Empty;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Tree node view-models
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Project-level node in the test tree.</summary>
public sealed class TestProjectNode : INotifyPropertyChanged
{
    public string ProjectName { get; }
    public ObservableCollection<TestClassNode> Classes { get; } = [];

    private bool _isExpanded = true;
    private bool _isRunning;
    private int  _passCount, _failCount, _skipCount, _notRunCount, _totalDurationMs;
    private bool _isVisible = true;

    public bool IsExpanded      { get => _isExpanded;      set => Set(ref _isExpanded,      value); }
    public bool IsRunning       { get => _isRunning;       set { Set(ref _isRunning,       value); Raise(nameof(OutcomeGlyph)); } }
    public int  PassCount       { get => _passCount;       set { Set(ref _passCount,       value); Raise(nameof(TotalCount)); Raise(nameof(OutcomeGlyph)); Raise(nameof(HasRun)); } }
    public int  FailCount       { get => _failCount;       set { Set(ref _failCount,       value); Raise(nameof(TotalCount)); Raise(nameof(OutcomeGlyph)); Raise(nameof(HasFailures)); Raise(nameof(HasRun)); } }
    public int  SkipCount       { get => _skipCount;       set { Set(ref _skipCount,       value); Raise(nameof(TotalCount)); Raise(nameof(HasRun)); } }
    public int  NotRunCount     { get => _notRunCount;     set { Set(ref _notRunCount,     value); Raise(nameof(TotalCount)); Raise(nameof(OutcomeGlyph)); } }
    public int  TotalDurationMs { get => _totalDurationMs; set { Set(ref _totalDurationMs, value); Raise(nameof(TotalDurationText)); } }
    public int  TotalCount   => PassCount + FailCount + SkipCount + NotRunCount;
    public bool HasFailures  => FailCount > 0;
    public bool HasRun       => PassCount + FailCount + SkipCount > 0;
    public bool IsVisible    { get => _isVisible;    set => Set(ref _isVisible,   value); }

    public string TotalDurationText => TotalDurationMs >= 1000
        ? $"{TotalDurationMs / 1000.0:F1} s"
        : $"{TotalDurationMs} ms";

    public string OutcomeGlyph =>
        IsRunning                    ? "\uE8A2"   // spinner
        : FailCount > 0              ? "\uEB90"   // red ✗
        : PassCount + SkipCount > 0  ? "\uE930"   // green ✓
        : "\uE73E";                               // pending circle = not yet run

    public TestProjectNode(string name) => ProjectName = name;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T f, T v, [CallerMemberName] string? n = null)
    { if (EqualityComparer<T>.Default.Equals(f, v)) return; f = v; PropertyChanged?.Invoke(this, new(n)); }
    private void Raise(string n) => PropertyChanged?.Invoke(this, new(n));
}

/// <summary>Class-level node in the test tree.</summary>
public sealed class TestClassNode : INotifyPropertyChanged
{
    public string FullClassName  { get; }
    public string ShortClassName { get; }
    public ObservableCollection<TestResultRow> Tests { get; } = [];

    private bool _isExpanded;  // collapsed by default — expanding the project shows classes, not all tests
    private int  _passCount, _failCount, _skipCount, _notRunCount, _totalDurationMs;
    private bool _isVisible = true;

    public bool IsExpanded      { get => _isExpanded;      set => Set(ref _isExpanded,      value); }
    public int  PassCount       { get => _passCount;       set { Set(ref _passCount,       value); Raise(nameof(TotalCount)); Raise(nameof(OutcomeGlyph)); Raise(nameof(HasRun)); } }
    public int  FailCount       { get => _failCount;       set { Set(ref _failCount,       value); Raise(nameof(TotalCount)); Raise(nameof(OutcomeGlyph)); Raise(nameof(HasFailures)); Raise(nameof(HasRun)); } }
    public int  SkipCount       { get => _skipCount;       set { Set(ref _skipCount,       value); Raise(nameof(TotalCount)); Raise(nameof(HasRun)); } }
    public int  NotRunCount     { get => _notRunCount;     set { Set(ref _notRunCount,     value); Raise(nameof(TotalCount)); Raise(nameof(OutcomeGlyph)); } }
    public int  TotalDurationMs { get => _totalDurationMs; set { Set(ref _totalDurationMs, value); Raise(nameof(TotalDurationText)); } }
    public int  TotalCount   => PassCount + FailCount + SkipCount + NotRunCount;
    public bool HasFailures  => FailCount > 0;
    public bool HasRun       => PassCount + FailCount + SkipCount > 0;
    public bool IsVisible    { get => _isVisible;    set => Set(ref _isVisible,   value); }

    public string TotalDurationText => TotalDurationMs >= 1000
        ? $"{TotalDurationMs / 1000.0:F1} s"
        : $"{TotalDurationMs} ms";

    public string OutcomeGlyph =>
        FailCount > 0               ? "\uEB90"   // red ✗
        : PassCount + SkipCount > 0 ? "\uE930"   // green ✓
        : "\uE73E";                              // pending circle = not yet run

    public TestClassNode(string fullName)
    {
        FullClassName = fullName;
        var dot = fullName.LastIndexOf('.');
        ShortClassName = dot >= 0 ? fullName[(dot + 1)..] : fullName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T f, T v, [CallerMemberName] string? n = null)
    { if (EqualityComparer<T>.Default.Equals(f, v)) return; f = v; PropertyChanged?.Invoke(this, new(n)); }
    private void Raise(string n) => PropertyChanged?.Invoke(this, new(n));
}

// ═══════════════════════════════════════════════════════════════════════════
// Leaf row view-model (reused for detail pane)
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Leaf view-model for a single test result.
/// Can be in "discovered" state (NotRun) before a run, then upgraded in-place via Update().
/// </summary>
public sealed class TestResultRow : INotifyPropertyChanged
{
    private TestResult? _result;
    private TestOutcome _outcome;
    private int         _durationMs;
    private bool        _isRunning;
    private bool        _isVisible = true;

    /// <summary>Creates a discovered (not-yet-run) row from <see cref="DiscoveredTest"/>.</summary>
    public TestResultRow(DiscoveredTest discovered)
    {
        Display   = discovered.TestName;
        ClassName = discovered.ClassName;
        _outcome  = TestOutcome.NotRun;
    }

    /// <summary>Creates a row directly from a run result.</summary>
    public TestResultRow(TestResult result)
    {
        _result     = result;
        Display     = result.TestName;
        ClassName   = result.ClassName;
        _outcome    = result.Outcome;
        _durationMs = (int)result.Duration.TotalMilliseconds;
    }

    /// <summary>Upgrades a discovered row in-place when a run result arrives.</summary>
    public void Update(TestResult result)
    {
        _result     = result;
        _outcome    = result.Outcome;
        _durationMs = (int)result.Duration.TotalMilliseconds;
        Notify(nameof(Outcome));
        Notify(nameof(DurationMs));
        Notify(nameof(OutcomeGlyph));
        Notify(nameof(ErrorMessage));
        Notify(nameof(StackTrace));
        Notify(nameof(HasDetail));
        Notify(nameof(AssemblyName));
        Notify(nameof(SourceFile));
        Notify(nameof(SourceLine));
        Notify(nameof(HasSource));
    }

    public string      Display    { get; }
    public string      ClassName  { get; }
    public TestOutcome Outcome    => _outcome;
    public int         DurationMs => _durationMs;
    public bool        IsOutput   => false;

    public string? ErrorMessage => _result?.ErrorMessage;
    public string? StackTrace   => _result?.StackTrace;
    public string  AssemblyName => _result?.AssemblyName ?? string.Empty;
    public bool    HasDetail    => ErrorMessage is not null || StackTrace is not null;

    public string? SourceFile => _result?.SourceFile;
    public int     SourceLine => _result?.SourceLine ?? 0;
    public bool    HasSource  => SourceFile is not null && File.Exists(SourceFile);

    /// <summary>Short class name (last dot-segment) shown in the Caractéristiques column.</summary>
    public string ShortClassName
    {
        get
        {
            var dot = ClassName.LastIndexOf('.');
            return dot >= 0 ? ClassName[(dot + 1)..] : ClassName;
        }
    }

    /// <summary>Truncated error message for the inline column (max 120 chars).</summary>
    public string ShortErrorMessage => ErrorMessage is null ? string.Empty
        : ErrorMessage.Length > 120 ? ErrorMessage[..120] + "…" : ErrorMessage;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning == value) return;
            _isRunning = value;
            Notify(nameof(IsRunning));
            Notify(nameof(OutcomeGlyph));
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            Notify(nameof(IsVisible));
        }
    }

    public string OutcomeGlyph => IsRunning ? "\uE8A2"
        : Outcome switch
        {
            TestOutcome.Passed  => "\uE930",
            TestOutcome.Failed  => "\uEB90",
            TestOutcome.NotRun  => "\uE73E",
            _                   => "\uE89A",
        };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ═══════════════════════════════════════════════════════════════════════════
// Value converters
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Converts an int count to a star-sized GridLength. Returns "1*" for zero.</summary>
public sealed class IntToStarGridLengthConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        return new System.Windows.GridLength(Math.Max(1, count), System.Windows.GridUnitType.Star);
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts null → Collapsed, non-null → Visible.</summary>
public sealed class NullToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
