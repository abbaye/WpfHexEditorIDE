// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/CodeAnalysisRunner.cs
// Description: Orchestrates the full code analysis pipeline.
//              Loads source files, builds Roslyn AdhocWorkspace,
//              runs all collectors in parallel, assembles the report.
//              Progress is reported via IProgress<AnalysisProgressUpdate>.
// Architecture Notes:
//     Does not depend on any WPF types — safe to call from a background thread.
//     The caller is responsible for dispatching report delivery to the UI thread.
// ==========================================================

using System.Collections.Concurrent;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WpfHexEditor.App.Analysis.Collectors;
using WpfHexEditor.App.Analysis.Models;
using Severity = WpfHexEditor.App.Analysis.Models.DiagnosticSeverity;

namespace WpfHexEditor.App.Analysis.Services;

public sealed class AnalysisProgressUpdate
{
    public string Phase   { get; init; } = string.Empty;
    public int    Percent { get; init; }
}

internal sealed class CodeAnalysisRunner
{
    private readonly CodeAnalysisOptionsService _optionsService;
    private readonly AnalysisSnapshotService    _snapshotService;

    internal CodeAnalysisRunner(
        CodeAnalysisOptionsService optionsService,
        AnalysisSnapshotService    snapshotService)
    {
        _optionsService  = optionsService;
        _snapshotService = snapshotService;
    }

    internal async Task<CodeAnalysisReport> RunAsync(
        AnalysisScope scope,
        string        scopePath,
        IProgress<AnalysisProgressUpdate>? progress = null,
        CancellationToken ct = default)
    {
        var opts = _optionsService.Options;

        Report(progress, "Discovering files…", 2);
        var csFiles = DiscoverFiles(scope, scopePath, opts.IncludeGeneratedFiles);
        Report(progress, $"Found {csFiles.Count} .cs files in: {scopePath}", 3);
        if (csFiles.Count == 0)
            return new CodeAnalysisReport { ScopePath = scopePath, Scope = scope };

        Report(progress, "Parsing syntax trees…", 8);
        var parsedTrees = await ParseTreesAsync(csFiles, ct);
        Report(progress, $"Parsed {parsedTrees.Count} trees", 9);

        // Group by project (heuristic: directory containing .csproj)
        var byProject = GroupByProject(parsedTrees);
        Report(progress, $"Grouped into {byProject.Count} projects", 10);

        var allDiagnostics  = new ConcurrentBag<AnalysisDiagnostic>();
        var allFiles        = new ConcurrentBag<FileMetrics>();
        var projectMetrics  = new List<ProjectMetrics>();

        int step = 0; int totalSteps = byProject.Count;

        Report(progress, "Analyzing volume + complexity…", 15);

        foreach (var (projName, projPath, trees) in byProject)
        {
            ct.ThrowIfCancellationRequested();

            // Build a per-project compilation for semantic analysis
            var compilation = CSharpCompilation.Create(
                projName,
                trees,
                GetBasicReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Volume + complexity per file (parallel)
            var fileMetricsList = new ConcurrentBag<FileMetrics>();
            await Parallel.ForEachAsync(trees, ct, (tree, token) =>
            {
                token.ThrowIfCancellationRequested();
                SemanticModel? model = null;
                try { model = compilation.GetSemanticModel(tree); } catch { }

                var volume  = VolumeMetricsCollector.Collect(tree, model, projName);
                var methods = ComplexityMetricsCollector.Collect(tree);

                // Convention checks
                var conventions = ConventionChecker.Check(tree, projName, opts);
                foreach (var d in conventions) allDiagnostics.Add(d);

                // Threshold-based complexity diagnostics
                foreach (var m in methods)
                {
                    if (m.CyclomaticComplexity > opts.CcError)
                        allDiagnostics.Add(ThresholdDiag("WH0001", Severity.Error,
                            $"Method '{m.Name}' has cyclomatic complexity {m.CyclomaticComplexity} (error threshold: {opts.CcError}).",
                            tree.FilePath, m.Line, projName));
                    else if (m.CyclomaticComplexity > opts.CcWarning)
                        allDiagnostics.Add(ThresholdDiag("WH0001", Severity.Warning,
                            $"Method '{m.Name}' has cyclomatic complexity {m.CyclomaticComplexity} (warning threshold: {opts.CcWarning}).",
                            tree.FilePath, m.Line, projName));

                    if (m.Loc > opts.MethodLocError)
                        allDiagnostics.Add(ThresholdDiag("WH0003", Severity.Error,
                            $"Method '{m.Name}' is {m.Loc} lines (error threshold: {opts.MethodLocError}).",
                            tree.FilePath, m.Line, projName));
                    else if (m.Loc > opts.MethodLocWarning)
                        allDiagnostics.Add(ThresholdDiag("WH0003", Severity.Warning,
                            $"Method '{m.Name}' is {m.Loc} lines (warning threshold: {opts.MethodLocWarning}).",
                            tree.FilePath, m.Line, projName));

                    if (m.ParameterCount > opts.MaxParamsError)
                        allDiagnostics.Add(ThresholdDiag("WH0005", Severity.Error,
                            $"Method '{m.Name}' has {m.ParameterCount} parameters (error threshold: {opts.MaxParamsError}).",
                            tree.FilePath, m.Line, projName));
                    else if (m.ParameterCount > opts.MaxParamsWarning)
                        allDiagnostics.Add(ThresholdDiag("WH0005", Severity.Warning,
                            $"Method '{m.Name}' has {m.ParameterCount} parameters (warning threshold: {opts.MaxParamsWarning}).",
                            tree.FilePath, m.Line, projName));
                }

                // File LOC thresholds
                if (volume.TotalLines > opts.FileLocError)
                    allDiagnostics.Add(ThresholdDiag("WH0004", Severity.Error,
                        $"File has {volume.TotalLines} lines (error threshold: {opts.FileLocError}).",
                        tree.FilePath, 1, projName));
                else if (volume.TotalLines > opts.FileLocWarning)
                    allDiagnostics.Add(ThresholdDiag("WH0004", Severity.Info,
                        $"File has {volume.TotalLines} lines (warning threshold: {opts.FileLocWarning}).",
                        tree.FilePath, 1, projName));

                // Coupling is attached below once the per-project compilation is available
                var fileWithMethods = volume with { Methods = methods };
                fileMetricsList.Add(fileWithMethods);
                return ValueTask.CompletedTask;
            });

            Report(progress, $"Coupling analysis — {projName}…", 35 + step * 20 / totalSteps);

            // Coupling (needs full compilation)
            var couplings = CouplingMetricsCollector.Collect(compilation);

            // Map couplings back to files
            var coupByFile = couplings.GroupBy(c => c.FilePath)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<CouplingMetrics>)g.ToList());

            Report(progress, $"Roslyn diagnostics — {projName}…", 55 + step * 10 / totalSteps);
            var roslynDiags = RoslynDiagnosticsCollector.Collect(compilation, projName);
            foreach (var d in roslynDiags) allDiagnostics.Add(d);

            // Rebuild file metrics with couplings attached + per-file score
            var finalFiles = fileMetricsList.Select(f =>
            {
                var fc = coupByFile.TryGetValue(f.FilePath, out var cl) ? cl : [];
                int fileScore = ComputeFileScore(f, fc, opts);
                return f with { Couplings = fc, Score = fileScore };
            }).ToList();

            foreach (var f in finalFiles) allFiles.Add(f);

            int projTotalLines = finalFiles.Sum(f => f.TotalLines);
            double avgCc = finalFiles.Count > 0
                ? finalFiles.Average(f => f.AvgCyclomaticComplexity) : 0;

            projectMetrics.Add(new ProjectMetrics
            {
                ProjectName             = projName,
                ProjectPath             = projPath,
                TotalFiles              = finalFiles.Count,
                TotalLines              = projTotalLines,
                CodeLines               = finalFiles.Sum(f => f.CodeLines),
                TypeCount               = finalFiles.Sum(f => f.TypeCount),
                MethodCount             = finalFiles.Sum(f => f.MethodCount),
                AvgCyclomaticComplexity = Math.Round(avgCc, 1),
                MaxCyclomaticComplexity = finalFiles.Count > 0 ? finalFiles.Max(f => f.MaxCyclomaticComplexity) : 0,
                Score                   = finalFiles.Count > 0 ? (int)finalFiles.Average(f => f.Score) : 0,
                Files                   = finalFiles,
            });

            step++;
        }

        Report(progress, "Detecting duplications…", 70);
        var allTrees   = parsedTrees.Select(t => t.tree).ToList();
        var duplications = DuplicationDetector.Detect(allTrees, opts.DupMinTokens);

        // Emit duplication diagnostics
        foreach (var grp in duplications)
        {
            foreach (var occ in grp.Occurrences)
            {
                var projName = allFiles.FirstOrDefault(f => f.FilePath == occ.FilePath)?.ProjectName ?? string.Empty;
                allDiagnostics.Add(ThresholdDiag("WH0020", Severity.Warning,
                    $"Duplication clone: {grp.LineCount} lines, {grp.Occurrences.Count} occurrences.",
                    occ.FilePath, occ.StartLine, projName));
            }
        }

        Report(progress, "Detecting dead code…", 82);
        var allDeadSymbols = new List<DeadSymbol>();
        foreach (var (projName, _, trees) in byProject)
        {
            var compilation = CSharpCompilation.Create(
                projName, trees, GetBasicReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var dead = DeadCodeDetector.Detect(compilation);
            allDeadSymbols.AddRange(dead);

            foreach (var d in dead)
            {
                var ruleId = d.IsInternal ? "WH0011" : "WH0010";
                allDiagnostics.Add(ThresholdDiag(ruleId, Severity.Warning,
                    $"Dead {d.Kind.ToString().ToLower()} '{d.Name}'.",
                    d.FilePath, d.Line, d.ProjectName));
            }
        }

        Report(progress, "Computing quality score…", 92);
        int totalLines = projectMetrics.Sum(p => p.TotalLines);
        var snapshot   = _snapshotService.LoadLatest();

        var diagList = allDiagnostics.ToList();

        // Patch project grades
        foreach (var pm in projectMetrics)
        {
            var patched = pm with { Grade = ScoreToGrade(pm.Score) };
            projectMetrics[projectMetrics.IndexOf(pm)] = patched;
        }

        var score = QualityScoreCalculator.Calculate(
            projectMetrics, duplications, allDeadSymbols, diagList,
            totalLines, 0, opts);

        // Compute trending delta now that we have the score
        int finalDelta = snapshot is null ? 0 : score.Score - snapshot.Score;
        score = score with { TrendingDelta = finalDelta };

        var report = new CodeAnalysisReport
        {
            Timestamp    = DateTime.UtcNow,
            ScopePath    = scopePath,
            Scope        = scope,
            TotalFiles   = allFiles.Count,
            TotalLines   = totalLines,
            ProjectCount = projectMetrics.Count,
            Score        = score,
            Projects     = projectMetrics,
            Diagnostics  = diagList,
            Duplications = duplications,
            DeadSymbols  = allDeadSymbols,
        };

        Report(progress, "Saving snapshot…", 98);
        _snapshotService.Save(report);

        Report(progress, "Done.", 100);
        return report;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void Report(IProgress<AnalysisProgressUpdate>? p, string phase, int pct)
        => p?.Report(new AnalysisProgressUpdate { Phase = phase, Percent = pct });

    private static List<string> DiscoverFiles(AnalysisScope scope, string path, bool includeGenerated)
    {
        var searchPath = scope == AnalysisScope.File
            ? Path.GetDirectoryName(path) ?? path
            : path;

        if (!Directory.Exists(searchPath) && File.Exists(searchPath))
            searchPath = Path.GetDirectoryName(searchPath) ?? searchPath;

        if (!Directory.Exists(searchPath)) return [];

        return Directory
            .EnumerateFiles(searchPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => scope != AnalysisScope.File || f == path)
            .Where(f => includeGenerated || !IsGenerated(f))
            .Where(f => !IsExcluded(f))
            .ToList();
    }

    private static bool IsGenerated(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] ExcludedSegments = [@"\obj\", @"\bin\", "/obj/", "/bin/"];

    private static bool IsExcluded(string path)
    {
        // Only exclude obj/bin *subdirectories*, not paths that happen to contain
        // these strings in a parent segment (e.g. C:\...\bin\Debug\net8.0\Sources\...)
        var normalized = path.Replace('/', '\\');
        return normalized.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<List<(SyntaxTree tree, string projName, string projPath)>> ParseTreesAsync(
        List<string> files, CancellationToken ct)
    {
        var results = new ConcurrentBag<(SyntaxTree, string, string)>();
        await Parallel.ForEachAsync(files, ct, async (file, token) =>
        {
            token.ThrowIfCancellationRequested();
            string source;
            try   { source = await File.ReadAllTextAsync(file, token); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { return; } // skip unreadable files — IOException, UnauthorizedAccessException, etc.

            if (string.IsNullOrWhiteSpace(source)) return;

            var tree                 = CSharpSyntaxTree.ParseText(source, path: file, cancellationToken: token);
            var (proj, projPath)     = FindProjectInfo(file);
            results.Add((tree, proj, projPath));
        });
        return results.ToList();
    }

    private static List<(string projName, string projPath, IReadOnlyList<SyntaxTree> trees)> GroupByProject(
        List<(SyntaxTree tree, string projName, string projPath)> parsed)
    {
        return parsed
            .GroupBy(t => t.projName)
            .Select(g => (g.Key, g.First().projPath, (IReadOnlyList<SyntaxTree>)g.Select(x => x.tree).ToList()))
            .ToList();
    }

    private static (string name, string path) FindProjectInfo(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        while (!string.IsNullOrEmpty(dir))
        {
            var csproj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
            if (csproj is not null)
                return (Path.GetFileNameWithoutExtension(csproj), dir);
            dir = Path.GetDirectoryName(dir);
        }
        return ("Unknown", string.Empty);
    }

    private static IEnumerable<MetadataReference> GetBasicReferences()
    {
        // Minimal references for compilation (enough for semantic analysis)
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator) ?? [];

        return trustedPlatformAssemblies
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll"
                     or "netstandard.dll" or "mscorlib.dll")
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));
    }

    private static int ComputeFileScore(FileMetrics f, IReadOnlyList<CouplingMetrics> couplings, CodeAnalysisOptions opts)
    {
        int score = 100;
        if (f.MaxCyclomaticComplexity > opts.CcError)        score -= 20;
        else if (f.MaxCyclomaticComplexity > opts.CcWarning) score -= 8;
        if (f.TotalLines > opts.FileLocError)                score -= 15;
        else if (f.TotalLines > opts.FileLocWarning)         score -= 5;
        if (couplings.Any(c => c.Instability > 0.8))        score -= 10;
        return Math.Max(0, score);
    }

    private static string ScoreToGrade(int score) => score switch
    {
        >= 93 => "A", >= 87 => "B+", >= 80 => "B",
        >= 70 => "C", >= 60 => "D",  _ => "F",
    };

    private static AnalysisDiagnostic ThresholdDiag(
        string id, Severity severity, string message,
        string file, int line, string project) => new()
    {
        Id          = id,
        Severity    = severity,
        Message     = message,
        FilePath    = file,
        Line        = line,
        ProjectName = project,
        RuleSource  = "Quality",
    };
}
