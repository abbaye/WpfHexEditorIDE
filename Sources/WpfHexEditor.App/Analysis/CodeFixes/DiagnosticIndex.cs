// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/CodeFixes/DiagnosticIndex.cs
// Description: Fast lookup of the latest AnalysisDiagnostic by (filePath, line).
//              Rebuilt by CodeAnalysisCodeActionProvider every time a new report
//              arrives, so the lightbulb provider answers in O(1) per query.
// ==========================================================

using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.CodeFixes;

internal sealed class DiagnosticIndex
{
    private readonly Dictionary<(string File, int Line), List<AnalysisDiagnostic>> _byLocation
        = new(LocationComparer.Instance);

    internal DiagnosticIndex(IReadOnlyList<AnalysisDiagnostic> diagnostics)
    {
        foreach (var d in diagnostics)
        {
            if (string.IsNullOrEmpty(d.FilePath) || d.Line < 1) continue;
            var key = (d.FilePath, d.Line);
            if (!_byLocation.TryGetValue(key, out var list))
                _byLocation[key] = list = [];
            list.Add(d);
        }
    }

    /// <summary>Returns diagnostics whose Line matches (`line` is 1-based) ± `tolerance`.</summary>
    internal IEnumerable<AnalysisDiagnostic> At(string filePath, int line, int tolerance = 0)
    {
        if (string.IsNullOrEmpty(filePath)) yield break;
        for (int delta = -tolerance; delta <= tolerance; delta++)
        {
            if (_byLocation.TryGetValue((filePath, line + delta), out var list))
                foreach (var d in list) yield return d;
        }
    }

    private sealed class LocationComparer : IEqualityComparer<(string File, int Line)>
    {
        internal static readonly LocationComparer Instance = new();
        public bool Equals((string File, int Line) x, (string File, int Line) y)
            => x.Line == y.Line && string.Equals(x.File, y.File, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string File, int Line) obj)
            => HashCode.Combine(obj.File.ToLowerInvariant(), obj.Line);
    }
}
