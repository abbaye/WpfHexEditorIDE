// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginDependencyGraph.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Adjacency-list dependency graph for the plugin system.
//     Provides topological load ordering with versioned constraint
//     validation, cascading unload/reload order, and startup error
//     detection (missing dependencies, version mismatches, cycles).
//
// Architecture Notes:
//     Forward edges: A → B means A depends on B (B must load first).
//     Reverse edges: B → A means B is required by A (A must unload first).
//     Kahn's BFS algorithm is used for topological sort (same as existing
//     WpfPluginHost inline implementation, now extracted here).
// ==========================================================

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>Kind of dependency validation error detected at startup.</summary>
public enum DependencyErrorKind
{
    /// <summary>Required plugin ID is not registered at all.</summary>
    Missing,
    /// <summary>Required plugin is present but its version does not satisfy the constraint.</summary>
    VersionMismatch,
    /// <summary>A cyclic dependency was detected (no valid load order exists).</summary>
    Circular,
}

/// <summary>A dependency validation error for a specific plugin pair.</summary>
public sealed record DependencyValidationError(
    string DependentPluginId,
    string RequiredPluginId,
    string? RequiredVersionExpression,
    DependencyErrorKind Kind);

/// <summary>
/// Builds and queries the plugin dependency graph.
/// Thread-safe for reads after <see cref="Build"/> completes.
/// </summary>
internal sealed class PluginDependencyGraph
{
    // Forward edges: pluginId → set of IDs it depends on.
    private readonly Dictionary<string, List<PluginDependencySpec>> _forwardEdges = new(StringComparer.OrdinalIgnoreCase);
    // Reverse edges: pluginId → set of IDs that depend on it.
    private readonly Dictionary<string, HashSet<string>> _reverseEdges = new(StringComparer.OrdinalIgnoreCase);
    // All known plugin IDs in the graph.
    private readonly HashSet<string> _allIds = new(StringComparer.OrdinalIgnoreCase);
    // Manifests indexed by ID for load-order output.
    private Dictionary<string, PluginManifest> _manifests = new(StringComparer.OrdinalIgnoreCase);

    // -------------------------------------------------------------------------
    // Build
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the graph from a set of discovered manifests.
    /// Must be called before any query methods.
    /// </summary>
    public void Build(IEnumerable<PluginManifest> manifests)
    {
        _forwardEdges.Clear();
        _reverseEdges.Clear();
        _allIds.Clear();
        _manifests = manifests.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

        // Register all IDs first so reverse edges can reference them.
        foreach (var id in _manifests.Keys)
        {
            _allIds.Add(id);
            if (!_forwardEdges.ContainsKey(id))
                _forwardEdges[id] = [];
            if (!_reverseEdges.ContainsKey(id))
                _reverseEdges[id] = [];
        }

        foreach (var manifest in _manifests.Values)
        {
            foreach (var rawDep in manifest.Dependencies)
            {
                var spec = PluginDependencySpec.Parse(rawDep);
                _forwardEdges[manifest.Id].Add(spec);

                // Add reverse edge if the dependency ID is known.
                if (_allIds.Contains(spec.PluginId))
                {
                    if (!_reverseEdges.ContainsKey(spec.PluginId))
                        _reverseEdges[spec.PluginId] = [];
                    _reverseEdges[spec.PluginId].Add(manifest.Id);
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Topological Load Order (Kahn's BFS)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns manifests sorted in topological order (dependencies before dependents).
    /// Version constraints are checked against <paramref name="loadedEntries"/>;
    /// plugins with unresolved dependencies are excluded from the result and marked
    /// as <see cref="PluginState.Incompatible"/> via <see cref="DependencyValidationError"/>s.
    /// </summary>
    public IReadOnlyList<PluginManifest> GetLoadOrder(
        IReadOnlyDictionary<string, PluginEntry> loadedEntries)
    {
        // Compute in-degree (number of satisfied dependencies).
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in _allIds)
            inDegree[id] = 0;

        foreach (var (id, deps) in _forwardEdges)
        {
            foreach (var dep in deps)
            {
                if (_allIds.Contains(dep.PluginId))
                    inDegree[id]++;
            }
        }

        var queue = new Queue<string>(
            inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<PluginManifest>();

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (_manifests.TryGetValue(id, out var manifest))
                result.Add(manifest);

            foreach (var dependent in _reverseEdges.GetValueOrDefault(id) ?? [])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        // Any node still with in-degree > 0 is part of a cycle — append at end
        // (WpfPluginHost will detect and mark them Incompatible via Validate()).
        foreach (var (id, deg) in inDegree)
        {
            if (deg > 0 && _manifests.TryGetValue(id, out var manifest))
                result.Add(manifest);
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Cascading Queries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all plugin IDs that directly or transitively depend on <paramref name="pluginId"/>.
    /// Result is in BFS order (nearest dependents first).
    /// </summary>
    public IReadOnlyList<string> GetDependents(string pluginId)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        foreach (var dep in _reverseEdges.GetValueOrDefault(pluginId) ?? [])
        {
            if (visited.Add(dep))
                queue.Enqueue(dep);
        }

        var result = new List<string>();
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            result.Add(id);
            foreach (var transitive in _reverseEdges.GetValueOrDefault(id) ?? [])
            {
                if (visited.Add(transitive))
                    queue.Enqueue(transitive);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns the ordered unload sequence for <paramref name="pluginId"/>:
    /// dependents (transitive, farthest first) followed by the target plugin itself.
    /// </summary>
    public IReadOnlyList<string> GetCascadedUnloadOrder(string pluginId)
    {
        var dependents = GetDependents(pluginId);
        // Dependents must unload first (reverse topological — farthest dependent first).
        var order = new List<string>(dependents);
        order.Reverse();
        order.Add(pluginId);
        return order;
    }

    /// <summary>
    /// Returns the ordered reload sequence for <paramref name="pluginId"/>:
    /// target plugin first, then its dependents in topological order.
    /// </summary>
    public IReadOnlyList<string> GetCascadedReloadOrder(string pluginId)
    {
        var dependents = GetDependents(pluginId);
        var order = new List<string> { pluginId };
        order.AddRange(dependents);
        return order;
    }

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates all dependency constraints against the currently registered entries.
    /// Returns errors for missing dependencies, version mismatches, and cycles.
    /// </summary>
    public IReadOnlyList<DependencyValidationError> Validate(
        IReadOnlyDictionary<string, PluginEntry> entries)
    {
        var errors = new List<DependencyValidationError>();

        // Check for missing or version-mismatched dependencies.
        foreach (var (pluginId, deps) in _forwardEdges)
        {
            foreach (var spec in deps)
            {
                if (!entries.TryGetValue(spec.PluginId, out var depEntry))
                {
                    errors.Add(new DependencyValidationError(
                        pluginId,
                        spec.PluginId,
                        spec.Constraint.ToString(),
                        DependencyErrorKind.Missing));
                    continue;
                }

                // Version check: manifest may declare a version string.
                var depVersionStr = depEntry.Manifest.Version;
                if (!string.IsNullOrWhiteSpace(depVersionStr)
                    && Version.TryParse(NormalizeVersion(depVersionStr), out var depVersion)
                    && !spec.IsSatisfiedBy(depVersion))
                {
                    errors.Add(new DependencyValidationError(
                        pluginId,
                        spec.PluginId,
                        spec.Constraint.ToString(),
                        DependencyErrorKind.VersionMismatch));
                }
            }
        }

        // Cycle detection: any node with in-degree > 0 after Kahn's pass is in a cycle.
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in _allIds)
            inDegree[id] = 0;

        foreach (var (id, deps) in _forwardEdges)
        {
            foreach (var dep in deps)
            {
                if (_allIds.Contains(dep.PluginId))
                    inDegree[id]++;
            }
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            foreach (var dep in _reverseEdges.GetValueOrDefault(id) ?? [])
            {
                inDegree[dep]--;
                if (inDegree[dep] == 0)
                    queue.Enqueue(dep);
            }
        }

        foreach (var (id, deg) in inDegree)
        {
            if (deg > 0)
            {
                errors.Add(new DependencyValidationError(
                    id, id, null, DependencyErrorKind.Circular));
            }
        }

        return errors;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the direct (non-transitive) dependency IDs for a plugin.
    /// Used by UI to display the "Requires" chip list.
    /// </summary>
    public IReadOnlyList<PluginDependencySpec> GetDirectDependencies(string pluginId)
        => _forwardEdges.TryGetValue(pluginId, out var deps) ? deps : [];

    /// <summary>
    /// Returns the direct (non-transitive) dependents for a plugin.
    /// Used by UI to display the "Required by" list.
    /// </summary>
    public IReadOnlyList<string> GetDirectDependents(string pluginId)
        => _reverseEdges.TryGetValue(pluginId, out var deps) ? [.. deps] : [];

    private static string NormalizeVersion(string v)
    {
        // Ensure at least 2 components so Version.TryParse succeeds (e.g. "1" → "1.0").
        var parts = v.Split('.');
        return parts.Length < 2 ? v + ".0" : v;
    }
}
