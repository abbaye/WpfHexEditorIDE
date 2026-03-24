// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: ViewModels/UnitTestingViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Updated: 2026-03-24 (ADR-UT-03 — overkill upgrade)
// Description:
//     ViewModel for the Unit Testing Panel. Holds the test result list,
//     counters, status text, filter state, running indicator,
//     selection detail, and commands (Run, Run Failed, Stop, Clear).
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using WpfHexEditor.Plugins.UnitTesting.Models;

namespace WpfHexEditor.Plugins.UnitTesting.ViewModels;

/// <summary>
/// MVVM ViewModel for the UnitTestingPanel.
/// </summary>
public sealed class UnitTestingViewModel : INotifyPropertyChanged
{
    // ── Observable results ───────────────────────────────────────────────────

    private readonly ObservableCollection<TestResultRow> _results = [];

    /// <summary>All (unfiltered) results.</summary>
    public IReadOnlyList<TestResultRow> Results => _results;

    /// <summary>Filtered + searched view — bind ListView.ItemsSource to this.</summary>
    public ICollectionView FilteredResults { get; }

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
        set => Set(ref _isRunning, value);
    }

    public int PassCount
    {
        get => _passCount;
        set
        {
            Set(ref _passCount, value);
            RaiseCounters();
        }
    }

    public int FailCount
    {
        get => _failCount;
        set
        {
            Set(ref _failCount, value);
            RaiseCounters();
        }
    }

    public int SkipCount
    {
        get => _skipCount;
        set
        {
            Set(ref _skipCount, value);
            RaiseCounters();
        }
    }

    public int TotalCount => PassCount + FailCount + SkipCount;

    private void RaiseCounters()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRunFailed)));
    }

    // ── Currently running row ────────────────────────────────────────────────

    private TestResultRow? _runningRow;

    public TestResultRow? RunningRow
    {
        get => _runningRow;
        set
        {
            if (_runningRow is not null) _runningRow.IsRunning = false;
            Set(ref _runningRow, value);
            if (value is not null) value.IsRunning = true;
        }
    }

    // ── Selection + detail ───────────────────────────────────────────────────

    private TestResultRow? _selectedResult;

    public TestResultRow? SelectedResult
    {
        get => _selectedResult;
        set => Set(ref _selectedResult, value);
    }

    // ── Filter / Search ──────────────────────────────────────────────────────

    private string _filterMode  = "All";
    private string _searchText  = string.Empty;

    public string FilterMode
    {
        get => _filterMode;
        set
        {
            Set(ref _filterMode, value);
            FilteredResults.Refresh();
            // Raise CanRun* for filter buttons
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterAll)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterPassed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterFailed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterSkipped)));
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { Set(ref _searchText, value); FilteredResults.Refresh(); }
    }

    public bool FilterAll     => _filterMode == "All";
    public bool FilterPassed  => _filterMode == "Passed";
    public bool FilterFailed  => _filterMode == "Failed";
    public bool FilterSkipped => _filterMode == "Skipped";

    public bool CanRunFailed  => FailCount > 0 && !IsRunning;

    // ── Constructor ──────────────────────────────────────────────────────────

    public UnitTestingViewModel()
    {
        FilteredResults = CollectionViewSource.GetDefaultView(_results);
        FilteredResults.Filter = FilterRow;
    }

    private bool FilterRow(object obj)
    {
        if (obj is not TestResultRow r) return true;
        if (_filterMode != "All" && r.Outcome.ToString() != _filterMode) return false;
        if (!string.IsNullOrWhiteSpace(_searchText) &&
            !r.Display.Contains(_searchText, StringComparison.OrdinalIgnoreCase) &&
            !r.ClassName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    // ── Mutation helpers ─────────────────────────────────────────────────────

    /// <summary>Clears all results and resets counters.</summary>
    public void Reset()
    {
        RunningRow  = null;
        _results.Clear();
        PassCount   = FailCount = SkipCount = 0;
        StatusText  = "Ready";
        IsRunning   = false;
        SelectedResult = null;
    }

    /// <summary>Inserts a placeholder row for the currently running project.</summary>
    public void AddRunningPlaceholder(string projectName)
    {
        var row = new TestResultRow(projectName);
        _results.Add(row);
        RunningRow = row;
    }

    /// <summary>Removes placeholder row once results are available.</summary>
    public void RemoveRunningPlaceholder(TestResultRow? placeholder)
    {
        if (placeholder is not null)
            _results.Remove(placeholder);
        RunningRow = null;
    }

    /// <summary>Appends a batch of results and updates counters.</summary>
    public void AddResults(IEnumerable<TestResult> results)
    {
        foreach (var r in results)
        {
            _results.Add(new TestResultRow(r));
            switch (r.Outcome)
            {
                case TestOutcome.Passed:  PassCount++; break;
                case TestOutcome.Failed:  FailCount++; break;
                default:                  SkipCount++; break;
            }
        }
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

/// <summary>
/// Row view-model for a single test result (or a running placeholder).
/// Implements INotifyPropertyChanged so IsRunning can animate the row in real time.
/// </summary>
public sealed class TestResultRow : INotifyPropertyChanged
{
    private bool _isRunning;

    /// <summary>Constructs from a parsed <see cref="TestResult"/>.</summary>
    public TestResultRow(TestResult result)
    {
        Result     = result;
        Display    = result.TestName;
        Outcome    = result.Outcome;
        DurationMs = (int)result.Duration.TotalMilliseconds;
        IsPlaceholder = false;
    }

    /// <summary>Constructs a running-placeholder row (no result yet).</summary>
    public TestResultRow(string projectName)
    {
        Result        = null;
        Display       = projectName;
        Outcome       = TestOutcome.Skipped; // neutral glyph until real result arrives
        DurationMs    = 0;
        IsPlaceholder = true;
    }

    public TestResult?  Result        { get; }
    public string       Display       { get; }
    public TestOutcome  Outcome       { get; }
    public int          DurationMs    { get; }
    public bool         IsPlaceholder { get; }
    public bool         IsOutput      => false;

    // Detail fields
    public string? ErrorMessage  => Result?.ErrorMessage;
    public string? StackTrace    => Result?.StackTrace;
    public string  ClassName     => Result?.ClassName ?? string.Empty;
    public string  AssemblyName  => Result?.AssemblyName ?? string.Empty;
    public bool    HasDetail     => ErrorMessage is not null || StackTrace is not null;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning == value) return;
            _isRunning = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning)));
        }
    }

    public string OutcomeGlyph => IsRunning ? "\uE8A2"  // Sync (spinner)
        : Outcome switch
        {
            TestOutcome.Passed  => "\uE930", // CheckMark circle
            TestOutcome.Failed  => "\uEB90", // Error badge
            _                   => "\uE89A", // Info
        };

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Converts an int count to a star-sized GridLength (e.g. 3 → "3*").
/// Returns "1*" for zero so columns never collapse completely.
/// </summary>
public sealed class IntToStarGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        return new GridLength(Math.Max(1, count), GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts null → Collapsed, non-null → Visible.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
