// ==========================================================
// Project: WpfHexEditor.BuildSystem
// File: BuildDependencyResolver.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Computes the topological build order for a set of projects based on
//     their ProjectReference dependencies (Kahn's algorithm).
//     Detects circular references and returns an error result.
//
// Architecture Notes:
//     Pattern: Topological Sort (Kahn's BFS)
//     - Input: list of (projectId, dependencies[]) tuples.
//     - Output: ordered list of project IDs (safe to build in sequence).
//     - Cycle detection: returns false with cyclic project IDs identified.
// ==========================================================

namespace WpfHexEditor.BuildSystem;

/// <summary>
/// Computes topological build order for a set of projects.
/// </summary>
public sealed class BuildDependencyResolver
{
    /// <summary>
    /// Computes the build order for <paramref name="projects"/>.
    /// </summary>
    /// <param name="projects">
    /// Map of projectId → list of projectId dependencies.
    /// </param>
    /// <param name="buildOrder">
    /// Ordered project IDs when successful (null on cycle detection).
    /// </param>
    /// <param name="cyclicProjects">
    /// Project IDs involved in a cycle, when detected.
    /// </param>
    /// <returns><c>true</c> if sort succeeded; <c>false</c> if a cycle was detected.</returns>
    public bool TryResolve(
        IReadOnlyDictionary<string, IReadOnlyList<string>> projects,
        out IReadOnlyList<string>?  buildOrder,
        out IReadOnlyList<string>?  cyclicProjects)
    {
        // Kahn's algorithm.
        var inDegree  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var adjList   = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (id, _) in projects)
        {
            inDegree.TryAdd(id, 0);
            adjList[id] = [];
        }

        foreach (var (id, deps) in projects)
        {
            foreach (var dep in deps)
            {
                if (!adjList.ContainsKey(dep)) { adjList[dep] = []; inDegree.TryAdd(dep, 0); }
                adjList[dep].Add(id);
                inDegree[id] = inDegree.GetValueOrDefault(id) + 1;
            }
        }

        var queue  = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<string>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);
            foreach (var neighbour in adjList[node])
            {
                inDegree[neighbour]--;
                if (inDegree[neighbour] == 0)
                    queue.Enqueue(neighbour);
            }
        }

        if (result.Count < inDegree.Count)
        {
            buildOrder     = null;
            cyclicProjects = [.. inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key)];
            return false;
        }

        buildOrder     = result;
        cyclicProjects = null;
        return true;
    }
}
