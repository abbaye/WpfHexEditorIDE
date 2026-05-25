// Project     : WpfHexEditor.App
// File        : StringSimilarityClusterer.cs
// Description : Groups StringRuns by Levenshtein distance ≤ maxDistance for strings ≥ minLength.
//               Single-pass union-find on the filtered results; O(n²) on the working set.
// Architecture: Stateless static service; clustering is opt-in and triggered manually.

using WpfHexEditor.App.BinaryAnalysis.Services;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

internal static class StringSimilarityClusterer
{
    /// <summary>
    /// Assigns cluster IDs to <paramref name="runs"/>. Returns a dictionary mapping
    /// run → cluster ID (1-based). Runs below <paramref name="minLength"/> get cluster 0
    /// (unclustered). Runs within Levenshtein distance ≤ <paramref name="maxDistance"/>
    /// share the same cluster ID.
    /// </summary>
    public static Dictionary<StringRun, int> Cluster(
        IReadOnlyList<StringRun> runs,
        int minLength    = 6,
        int maxDistance  = 2)
    {
        var result   = new Dictionary<StringRun, int>(runs.Count);
        var eligible = runs.Where(r => r.Value.Length >= minLength).ToList();

        if (eligible.Count > 10_000)
        {
            foreach (var run in runs) result[run] = 0;
            return result;
        }

        // Union-Find over eligible indices.
        var parent = new int[eligible.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;

        int Find(int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }

        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        for (int i = 0; i < eligible.Count; i++)
        {
            for (int j = i + 1; j < eligible.Count; j++)
            {
                if (Math.Abs(eligible[i].Value.Length - eligible[j].Value.Length) > maxDistance) continue;
                if (Levenshtein(eligible[i].Value, eligible[j].Value, maxDistance) <= maxDistance)
                    Union(i, j);
            }
        }

        // Map root → cluster ID (only roots with ≥2 members get a non-zero ID).
        var rootCount = new Dictionary<int, int>();
        for (int i = 0; i < eligible.Count; i++)
        {
            int root = Find(i);
            rootCount.TryGetValue(root, out int c);
            rootCount[root] = c + 1;
        }

        int nextId = 1;
        var rootToId = new Dictionary<int, int>();
        foreach (var (root, count) in rootCount)
            if (count > 1) rootToId[root] = nextId++;

        for (int i = 0; i < eligible.Count; i++)
        {
            int root = Find(i);
            result[eligible[i]] = rootToId.TryGetValue(root, out int id) ? id : 0;
        }

        // Unclustered runs get 0.
        foreach (var run in runs.Where(r => r.Value.Length < minLength))
            result[run] = 0;

        return result;
    }

    /// <summary>
    /// Bounded Levenshtein — returns early with <paramref name="cap"/>+1 if the true
    /// distance exceeds the cap, saving work on clearly dissimilar pairs.
    /// </summary>
    private static int Levenshtein(string a, string b, int cap)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        // Keep only two rows; trim common prefix/suffix first.
        int start = 0;
        while (start < a.Length && start < b.Length && a[start] == b[start]) start++;
        int endA = a.Length - 1, endB = b.Length - 1;
        while (endA > start && endB > start && a[endA] == b[endB]) { endA--; endB--; }

        a = a[start..(endA + 1)];
        b = b[start..(endB + 1)];

        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        if (a.Length > b.Length) (a, b) = (b, a); // ensure a is shorter

        var prev = new int[a.Length + 1];
        var curr = new int[a.Length + 1];
        for (int i = 0; i <= a.Length; i++) prev[i] = i;

        for (int j = 1; j <= b.Length; j++)
        {
            curr[0] = j;
            int rowMin = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[i] = Math.Min(Math.Min(curr[i - 1] + 1, prev[i] + 1), prev[i - 1] + cost);
                rowMin = Math.Min(rowMin, curr[i]);
            }
            if (rowMin > cap) return cap + 1;
            (prev, curr) = (curr, prev);
        }
        return prev[a.Length];
    }
}
