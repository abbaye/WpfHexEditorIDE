// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/ViewModels/DuplicationGroupViewModel.cs
// Description: Wraps a DuplicationGroup for the redesigned Duplication tab.
//              Exposes derived display fields (severity, primary file name)
//              and the two occurrence indices chosen for side-by-side preview.
// ==========================================================

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.UI.ViewModels;

public sealed class DuplicationGroupViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly DuplicationGroup _group;
    private int _selectedIndexA;
    private int _selectedIndexB = 1;

    public DuplicationGroupViewModel(DuplicationGroup group)
    {
        _group   = group;
        Severity = ComputeSeverity(group);
        if (group.Occurrences.Count <= 1)
            _selectedIndexB = Math.Max(0, group.Occurrences.Count - 1);
    }

    public int    LineCount        => _group.LineCount;
    public int    TokenCount       => _group.TokenCount;
    public int    OccurrenceCount  => _group.Occurrences.Count;
    public DuplicationSeverity Severity { get; }

    public IReadOnlyList<DuplicationOccurrence> Occurrences => _group.Occurrences;

    /// <summary>First occurrence's file name (no path) — for compact list display.</summary>
    public string PrimaryFileName =>
        _group.Occurrences.Count == 0 ? string.Empty : Path.GetFileName(_group.Occurrences[0].FilePath);

    /// <summary>Total "wasted" lines = LineCount × (Occurrences − 1).</summary>
    public int DuplicatedLines => Math.Max(0, LineCount * (OccurrenceCount - 1));

    /// <summary>0-based index of the left-pane occurrence.</summary>
    public int SelectedIndexA
    {
        get => _selectedIndexA;
        set { if (_selectedIndexA == value) return; _selectedIndexA = value; OnPropertyChanged(); OnPropertyChanged(nameof(OccurrenceA)); }
    }

    /// <summary>0-based index of the right-pane occurrence.</summary>
    public int SelectedIndexB
    {
        get => _selectedIndexB;
        set { if (_selectedIndexB == value) return; _selectedIndexB = value; OnPropertyChanged(); OnPropertyChanged(nameof(OccurrenceB)); }
    }

    public DuplicationOccurrence? OccurrenceA =>
        SelectedIndexA >= 0 && SelectedIndexA < _group.Occurrences.Count ? _group.Occurrences[SelectedIndexA] : null;

    public DuplicationOccurrence? OccurrenceB =>
        SelectedIndexB >= 0 && SelectedIndexB < _group.Occurrences.Count ? _group.Occurrences[SelectedIndexB] : null;

    /// <summary>Score-based derivation: LineCount × (Occurrences−1) × log2(Tokens/50).</summary>
    private static DuplicationSeverity ComputeSeverity(DuplicationGroup g)
    {
        if (g.LineCount <= 0 || g.Occurrences.Count < 2) return DuplicationSeverity.Low;
        double factor = Math.Max(1, Math.Log2(Math.Max(50, g.TokenCount) / 50.0));
        double score  = g.LineCount * (g.Occurrences.Count - 1) * factor;
        if (score >= 500) return DuplicationSeverity.High;
        if (score >= 100) return DuplicationSeverity.Medium;
        return DuplicationSeverity.Low;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
