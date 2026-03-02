//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Diagnostics;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Editor.TblEditor.Models;
using WpfHexEditor.Editor.TblEditor.ViewModels;

namespace WpfHexEditor.Editor.TblEditor.Services;

/// <summary>
/// Conflict analyzer using Trie algorithm for prefix detection
/// </summary>
public class TblConflictAnalyzer
{
    private class TrieNode
    {
        public Dictionary<char, TrieNode> Children = [];
        public TblEntryViewModel? Entry;
        public bool IsTerminal => Entry != null;
    }

    public async Task<List<TblConflict>> AnalyzeConflictsAsync(IEnumerable<TblEntryViewModel> entries, CancellationToken cancellationToken)
    {
        var snapshot = entries.ToList(); // Snapshot on calling thread to avoid cross-thread collection modification
        return await Task.Run(() => AnalyzeConflicts(snapshot, cancellationToken));
    }

    public List<TblConflict> AnalyzeConflicts(IEnumerable<TblEntryViewModel> entries, CancellationToken cancellationToken)
    {
        var conflicts = new List<TblConflict>();
        var sw = Stopwatch.StartNew();
        var entryList = entries is List<TblEntryViewModel> list ? list : entries.ToList();
        var trie = BuildTrie(entryList);
        if (!cancellationToken.IsCancellationRequested)
            conflicts.AddRange(DetectPrefixConflicts(trie, cancellationToken));
        if (!cancellationToken.IsCancellationRequested)
            conflicts.AddRange(DetectDuplicates(entryList, cancellationToken));
        Debug.WriteLine($"Conflict analysis: {sw.ElapsedMilliseconds}ms, {conflicts.Count} conflicts");
        return conflicts;
    }

    public List<TblConflict> CheckEntryConflicts(string newEntry, IEnumerable<TblEntryViewModel> existingEntries)
    {
        var conflicts = new List<TblConflict>();
        var upper = newEntry.ToUpperInvariant();
        foreach (var existing in existingEntries)
        {
            var eu = existing.Entry.ToUpperInvariant();
            if (eu.StartsWith(upper) && eu != upper)
                conflicts.Add(Conflict(ConflictType.PrefixConflict, ConflictSeverity.Warning, existing, $"Entry '{upper}' is prefix of '{eu}'"));
            if (upper.StartsWith(eu) && upper != eu)
                conflicts.Add(Conflict(ConflictType.PrefixConflict, ConflictSeverity.Warning, existing, $"Entry '{eu}' is prefix of '{upper}'"));
            if (upper == eu)
                conflicts.Add(Conflict(ConflictType.Duplicate, ConflictSeverity.Error, existing, $"Duplicate entry '{upper}'"));
        }
        return conflicts;
    }

    private TblConflict Conflict(ConflictType type, ConflictSeverity severity, TblEntryViewModel entry, string description) => new()
    {
        Type = type, Severity = severity,
        ConflictingEntries = [entry.ToDto()],
        Description = description,
        Suggestion = type == ConflictType.Duplicate ? "Remove duplicate or merge values" : "Remove shorter entry or use different byte values"
    };

    private TrieNode BuildTrie(List<TblEntryViewModel> entries)
    {
        var root = new TrieNode();
        foreach (var entry in entries)
        {
            var current = root;
            foreach (char c in entry.Entry.ToUpperInvariant())
            {
                if (!current.Children.ContainsKey(c)) current.Children[c] = new TrieNode();
                current = current.Children[c];
            }
            current.Entry = entry;
        }
        return root;
    }

    private List<TblConflict> DetectPrefixConflicts(TrieNode root, CancellationToken ct)
    {
        var conflicts = new List<TblConflict>();
        void DFS(TrieNode node)
        {
            if (ct.IsCancellationRequested) return;
            if (node.IsTerminal && node.Children.Count > 0)
            {
                var longer = new List<Dte>();
                CollectTerminals(node, longer);
                if (longer.Count > 0)
                    conflicts.Add(new TblConflict { Type = ConflictType.PrefixConflict, Severity = ConflictSeverity.Warning,
                        ConflictingEntries = [node.Entry!.ToDto(), ..longer],
                        Description = $"Entry '{node.Entry.Entry}' is prefix of {longer.Count} longer entries",
                        Suggestion = "Remove shorter entry or use different byte values" });
            }
            foreach (var child in node.Children.Values) DFS(child);
        }
        DFS(root);
        return conflicts;
    }

    private void CollectTerminals(TrieNode node, List<Dte> terminals)
    {
        foreach (var child in node.Children.Values)
        {
            if (child.IsTerminal) terminals.Add(child.Entry!.ToDto());
            CollectTerminals(child, terminals);
        }
    }

    private List<TblConflict> DetectDuplicates(List<TblEntryViewModel> entries, CancellationToken ct)
    {
        var conflicts = new List<TblConflict>();
        var seen = new Dictionary<string, TblEntryViewModel>();
        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) return conflicts;
            var key = entry.Entry.ToUpperInvariant();
            if (seen.TryGetValue(key, out var existing))
                conflicts.Add(new TblConflict { Type = ConflictType.Duplicate, Severity = ConflictSeverity.Error,
                    ConflictingEntries = [existing.ToDto(), entry.ToDto()],
                    Description = $"Duplicate entry '{key}'", Suggestion = "Remove duplicate or merge values" });
            else seen[key] = entry;
        }
        return conflicts;
    }
}
