// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Collectors/CyclicDependencyDetector.cs
// Description: Detects cycles in the project-to-project dependency graph
//              using Tarjan's strongly-connected components algorithm.
//              A cycle means project A depends on B which depends on A
//              (transitively or directly) — usually a design error.
//
//              WH0050 — Cyclic project dependency
// Architecture Notes:
//     Stateless. Operates on the already-collected coupling graph.
// ==========================================================

using WpfHexEditor.App.Analysis.Models;
using Severity = WpfHexEditor.App.Analysis.Models.DiagnosticSeverity;

namespace WpfHexEditor.App.Analysis.Collectors;

internal static class CyclicDependencyDetector
{
    public readonly record struct ProjectCycle(IReadOnlyList<string> Projects);

    internal static (IReadOnlyList<ProjectCycle> Cycles, IReadOnlyList<AnalysisDiagnostic> Diagnostics)
        Detect(IReadOnlyList<ProjectMetrics> projects, IReadOnlyList<CouplingMetrics> couplings)
    {
        // Build adjacency: projectName → set of projects it depends on
        var nameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < projects.Count; i++) nameToIndex[projects[i].ProjectName] = i;

        var adj = new List<HashSet<int>>(projects.Count);
        for (int i = 0; i < projects.Count; i++) adj.Add([]);

        // Use file→project mapping to project couplings to project-level edges
        var fileToProject = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in projects)
        {
            int idx = nameToIndex[p.ProjectName];
            foreach (var f in p.Files) fileToProject[f.FilePath] = idx;
        }

        foreach (var c in couplings)
        {
            if (!fileToProject.TryGetValue(c.FilePath, out int from)) continue;
            // Coupling.DependsOn is a list of fully-qualified type names; we resolve
            // to project name only if a file/type from another project matches.
            // We approximate: if any other project's type set contains DependsOn name → edge.
            foreach (var dep in c.DependsOn)
            {
                int? to = ResolveProjectIndex(dep, projects, nameToIndex);
                if (to is int ti && ti != from) adj[from].Add(ti);
            }
        }

        // Tarjan SCC
        var sccs = TarjanScc(adj);
        var cycles = new List<ProjectCycle>();
        var diagnostics = new List<AnalysisDiagnostic>();
        foreach (var scc in sccs)
        {
            if (scc.Count < 2) continue; // single-node SCC = no cycle
            var names = scc.Select(i => projects[i].ProjectName).ToList();
            cycles.Add(new ProjectCycle(names));

            // Emit diagnostic on each project in the cycle (line 1 of project file)
            foreach (var i in scc)
            {
                var p = projects[i];
                diagnostics.Add(new AnalysisDiagnostic
                {
                    Id          = "WH0050",
                    Severity    = Severity.Error,
                    Message     = $"Cyclic project dependency: {string.Join(" → ", names)} → {names[0]}.",
                    FilePath    = p.ProjectPath,
                    Line        = 1,
                    Column      = 1,
                    ProjectName = p.ProjectName,
                    RuleSource  = "Quality",
                });
            }
        }
        return (cycles, diagnostics);
    }

    private static int? ResolveProjectIndex(string typeName, IReadOnlyList<ProjectMetrics> projects, Dictionary<string, int> nameToIndex)
    {
        // Heuristic: if type's namespace prefix matches a project name, attribute the dep there.
        foreach (var p in projects)
        {
            if (typeName.StartsWith(p.ProjectName + ".", StringComparison.Ordinal))
                return nameToIndex[p.ProjectName];
        }
        return null;
    }

    // ── Tarjan's strongly-connected components ────────────────────────────────

    private static List<List<int>> TarjanScc(List<HashSet<int>> adj)
    {
        int n = adj.Count;
        var index = new int[n];
        var lowlink = new int[n];
        var onStack = new bool[n];
        var visited = new bool[n];
        Array.Fill(index, -1);
        var stack = new Stack<int>();
        int counter = 0;
        var result = new List<List<int>>();

        for (int v = 0; v < n; v++)
            if (!visited[v]) StrongConnect(v);

        void StrongConnect(int v)
        {
            index[v]    = counter;
            lowlink[v]  = counter;
            counter++;
            visited[v]  = true;
            stack.Push(v);
            onStack[v]  = true;

            foreach (int w in adj[v])
            {
                if (!visited[w])
                {
                    StrongConnect(w);
                    lowlink[v] = Math.Min(lowlink[v], lowlink[w]);
                }
                else if (onStack[w])
                    lowlink[v] = Math.Min(lowlink[v], index[w]);
            }

            if (lowlink[v] == index[v])
            {
                var component = new List<int>();
                int w;
                do { w = stack.Pop(); onStack[w] = false; component.Add(w); }
                while (w != v);
                result.Add(component);
            }
        }

        return result;
    }
}
