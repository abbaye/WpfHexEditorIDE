using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Editor.TblEditor.ViewModels;

namespace WpfHexEditor.Editor.TblEditor.Services;

/// <summary>Fast search and filter service with hash-based indexing</summary>
public class TblSearchService
{
    private Dictionary<string, List<TblEntryViewModel>>? _entryIndex;
    private Dictionary<DteType, List<TblEntryViewModel>>? _typeIndex;
    private Dictionary<char, List<TblEntryViewModel>>? _valueFirstCharIndex;

    public async Task BuildIndexAsync(IEnumerable<TblEntryViewModel> entries) =>
        await Task.Run(() => BuildIndex(entries));

    public void BuildIndex(IEnumerable<TblEntryViewModel> entries)
    {
        var entryList = entries.ToList();
        _entryIndex = entryList.GroupBy(e => e.Entry.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.ToList());
        _typeIndex = entryList.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.ToList());
        _valueFirstCharIndex = entryList.Where(e => !string.IsNullOrEmpty(e.Value)).GroupBy(e => e.Value![0]).ToDictionary(g => g.Key, g => g.ToList());
    }

    public List<TblEntryViewModel> SearchByEntry(string hexPattern)
    {
        if (string.IsNullOrWhiteSpace(hexPattern) || _entryIndex == null) return [];
        var upper = hexPattern.ToUpperInvariant();
        if (_entryIndex.TryGetValue(upper, out var exact)) return [..exact];
        return _entryIndex.Where(kvp => kvp.Key.StartsWith(upper)).SelectMany(kvp => kvp.Value).ToList();
    }

    public List<TblEntryViewModel> SearchByValue(string text, bool caseSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(text) || _valueFirstCharIndex == null) return [];
        if (text.Length == 1)
        {
            var firstChar = caseSensitive ? text[0] : char.ToLowerInvariant(text[0]);
            if (_valueFirstCharIndex.TryGetValue(firstChar, out var matches)) return [..matches];
        }
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return _entryIndex?.Values.SelectMany(l => l).Where(e => e.Value?.Contains(text, comparison) == true).ToList() ?? [];
    }

    public List<TblEntryViewModel> FilterByType(DteType type) =>
        _typeIndex?.TryGetValue(type, out var matches) == true ? [..matches] : [];

    public List<TblEntryViewModel> ApplyFilters(IEnumerable<TblEntryViewModel> entries,
        string? searchText = null, DteType? filterType = null, bool showConflictsOnly = false)
    {
        var results = entries.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(searchText))
            results = results.Where(e => e.Entry.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                         (e.Value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true));
        if (filterType.HasValue) results = results.Where(e => e.Type == filterType.Value);
        if (showConflictsOnly) results = results.Where(e => e.HasConflict);
        return results.ToList();
    }
}
