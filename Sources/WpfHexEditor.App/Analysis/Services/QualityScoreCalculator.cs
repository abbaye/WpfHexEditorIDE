// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/QualityScoreCalculator.cs
// Description: Computes the weighted quality score [0–100] and letter grade
//              from the raw analysis data and the user-configured thresholds.
// ==========================================================

using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal static class QualityScoreCalculator
{
    private const int MaxWorstFiles = 5;

    internal static QualityScore Calculate(
        IReadOnlyList<ProjectMetrics>    projects,
        IReadOnlyList<DuplicationGroup>  duplications,
        IReadOnlyList<DeadSymbol>        deadSymbols,
        IReadOnlyList<AnalysisDiagnostic> diagnostics,
        int                              totalLines,
        int                              trendingDelta,
        CodeAnalysisOptions              options)
    {
        var allFiles = projects.SelectMany(p => p.Files).ToList();

        int volScore  = ScoreVolume(allFiles, options);
        int ccScore   = ScoreComplexity(allFiles, options);
        int coupScore = ScoreCoupling(allFiles);
        int dupScore  = ScoreDuplication(duplications, totalLines, options);
        int deadScore = ScoreDeadCode(deadSymbols, allFiles.Count);
        int convScore = ScoreConventions(diagnostics);

        // Weighted average: complexity + coupling carry more weight
        double overall = volScore  * 0.10
                       + ccScore   * 0.25
                       + coupScore * 0.20
                       + dupScore  * 0.20
                       + deadScore * 0.15
                       + convScore * 0.10;

        int score = (int)Math.Round(overall);
        score = Math.Clamp(score, 0, 100);

        var worstFiles = allFiles
            .OrderBy(f => f.Score)
            .Take(MaxWorstFiles)
            .ToList();

        return new QualityScore
        {
            Score           = score,
            Grade           = ToGrade(score),
            TrendingDelta   = trendingDelta,
            VolumeScore     = volScore,
            ComplexityScore = ccScore,
            CouplingScore   = coupScore,
            DuplicationScore = dupScore,
            DeadCodeScore   = deadScore,
            ConventionScore = convScore,
            WorstFiles      = worstFiles,
        };
    }

    private static int ScoreVolume(List<FileMetrics> files, CodeAnalysisOptions o)
    {
        if (files.Count == 0) return 100;
        int violations = files.Count(f => f.TotalLines > o.FileLocError);
        int warnings   = files.Count(f => f.TotalLines > o.FileLocWarning && f.TotalLines <= o.FileLocError);

        // Magnitude penalty: each file >2x error threshold removes extra points (logarithmic)
        double magnitudePenalty = files
            .Where(f => f.TotalLines > o.FileLocError)
            .Sum(f => Math.Log10((double)f.TotalLines / Math.Max(1, o.FileLocError)) * 10);

        return Math.Max(0, 100 - violations * 10 - warnings * 3 - (int)magnitudePenalty);
    }

    private static int ScoreComplexity(List<FileMetrics> files, CodeAnalysisOptions o)
    {
        if (files.Count == 0) return 100;
        int errors   = files.Count(f => f.MaxCyclomaticComplexity > o.CcError);
        int warnings = files.Count(f => f.MaxCyclomaticComplexity > o.CcWarning && f.MaxCyclomaticComplexity <= o.CcError);
        return Math.Max(0, 100 - errors * 8 - warnings * 2);
    }

    private static int ScoreCoupling(List<FileMetrics> files)
    {
        if (files.Count == 0) return 100;
        int highInstability = files
            .SelectMany(f => f.Couplings)
            .Count(c => c.Instability > 0.8);
        return Math.Max(0, 100 - highInstability * 5);
    }

    private static int ScoreDuplication(IReadOnlyList<DuplicationGroup> groups, int totalLines, CodeAnalysisOptions o)
    {
        if (totalLines == 0) return 100;
        int dupLines = groups.Sum(g => g.LineCount * (g.Occurrences.Count - 1));
        double pct   = (double)dupLines / totalLines * 100;
        if (pct >= o.DupPercentError)   return Math.Max(0, 100 - (int)(pct * 4));
        if (pct >= o.DupPercentWarning) return Math.Max(0, 100 - (int)(pct * 2));
        return 100;
    }

    private static int ScoreDeadCode(IReadOnlyList<DeadSymbol> dead, int fileCount)
    {
        if (fileCount == 0) return 100;
        return Math.Max(0, 100 - dead.Count * 2);
    }

    private static int ScoreConventions(IReadOnlyList<AnalysisDiagnostic> diags)
    {
        int errors   = diags.Count(d => d.RuleSource == "Quality" && d.Severity == DiagnosticSeverity.Error);
        int warnings = diags.Count(d => d.RuleSource == "Quality" && d.Severity == DiagnosticSeverity.Warning);
        return Math.Max(0, 100 - errors * 5 - warnings * 1);
    }

    private static string ToGrade(int score) => score switch
    {
        >= 97 => "A+",
        >= 93 => "A",
        >= 90 => "A-",
        >= 87 => "B+",
        >= 83 => "B",
        >= 80 => "B-",
        >= 77 => "C+",
        >= 73 => "C",
        >= 70 => "C-",
        >= 60 => "D",
        _     => "F",
    };
}
