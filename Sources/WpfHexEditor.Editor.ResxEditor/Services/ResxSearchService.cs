// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Services/ResxSearchService.cs
// Description:
//     Provides find/replace logic for the ISearchTarget
//     implementation in ResxEditor.  Operates on the live
//     ObservableCollection via the ViewModel.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.Editor.ResxEditor.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.Services;

/// <summary>Scope flags for search operations.</summary>
[Flags]
public enum ResxSearchScope
{
    None    = 0,
    Keys    = 1,
    Values  = 2,
    Comments = 4,
    All     = Keys | Values | Comments
}

/// <summary>Search/replace state for the RESX editor.</summary>
public sealed class ResxSearchService
{
    private int    _currentMatchIndex = -1;
    private string _lastPattern       = string.Empty;
    private bool   _lastCaseSensitive;
    private bool   _lastRegex;
    private ResxSearchScope _lastScope = ResxSearchScope.All;

    private List<ResxEntryViewModel> _matches = [];

    // -- Public API ---------------------------------------------------------

    /// <summary>
    /// Refreshes the match list.  Returns the number of matches.
    /// </summary>
    public int Refresh(
        IEnumerable<ResxEntryViewModel> entries,
        string                          pattern,
        bool                            caseSensitive,
        bool                            useRegex,
        ResxSearchScope                 scope = ResxSearchScope.All)
    {
        _lastPattern       = pattern;
        _lastCaseSensitive = caseSensitive;
        _lastRegex         = useRegex;
        _lastScope         = scope;
        _currentMatchIndex = -1;

        _matches = entries.Where(e => Matches(e, pattern, caseSensitive, useRegex, scope)).ToList();
        return _matches.Count;
    }

    /// <summary>Navigates to the next match.  Returns the match or null.</summary>
    public ResxEntryViewModel? FindNext()
    {
        if (_matches.Count == 0) return null;
        _currentMatchIndex = (_currentMatchIndex + 1) % _matches.Count;
        return _matches[_currentMatchIndex];
    }

    /// <summary>Navigates to the previous match.  Returns the match or null.</summary>
    public ResxEntryViewModel? FindPrevious()
    {
        if (_matches.Count == 0) return null;
        _currentMatchIndex = (_currentMatchIndex - 1 + _matches.Count) % _matches.Count;
        return _matches[_currentMatchIndex];
    }

    /// <summary>Replaces the value/comment in the current match.  Returns true on success.</summary>
    public bool Replace(ResxEntryViewModel? target, string replacement)
    {
        if (target is null) return false;
        if (_lastScope.HasFlag(ResxSearchScope.Values))
            target.Value = ReplaceIn(target.Value, _lastPattern, replacement, _lastCaseSensitive, _lastRegex);
        return true;
    }

    /// <summary>Replaces all matches.  Returns count of affected entries.</summary>
    public int ReplaceAll(IEnumerable<ResxEntryViewModel> entries, string replacement)
    {
        var changed = 0;
        foreach (var vm in entries)
        {
            if (!Matches(vm, _lastPattern, _lastCaseSensitive, _lastRegex, _lastScope)) continue;
            if (_lastScope.HasFlag(ResxSearchScope.Values))
                vm.Value = ReplaceIn(vm.Value, _lastPattern, replacement, _lastCaseSensitive, _lastRegex);
            if (_lastScope.HasFlag(ResxSearchScope.Comments))
                vm.Comment = ReplaceIn(vm.Comment, _lastPattern, replacement, _lastCaseSensitive, _lastRegex);
            changed++;
        }
        return changed;
    }

    // -- Helpers ------------------------------------------------------------

    private static bool Matches(
        ResxEntryViewModel vm,
        string             pattern,
        bool               caseSensitive,
        bool               useRegex,
        ResxSearchScope    scope)
    {
        if (string.IsNullOrEmpty(pattern)) return false;

        if (scope.HasFlag(ResxSearchScope.Keys)     && Contains(vm.Name,    pattern, caseSensitive, useRegex)) return true;
        if (scope.HasFlag(ResxSearchScope.Values)   && Contains(vm.Value,   pattern, caseSensitive, useRegex)) return true;
        if (scope.HasFlag(ResxSearchScope.Comments) && Contains(vm.Comment, pattern, caseSensitive, useRegex)) return true;

        return false;
    }

    private static bool Contains(string text, string pattern, bool caseSensitive, bool useRegex)
    {
        if (useRegex)
        {
            var opts = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            try { return Regex.IsMatch(text, pattern, opts); } catch { return false; }
        }
        return text.Contains(pattern, caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase);
    }

    private static string ReplaceIn(string text, string pattern, string replacement, bool caseSensitive, bool useRegex)
    {
        if (useRegex)
        {
            var opts = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            try { return Regex.Replace(text, pattern, replacement, opts); } catch { return text; }
        }
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return text.Replace(pattern, replacement, comparison);
    }
}
