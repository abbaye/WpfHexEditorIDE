// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/XRefViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for the XRefs tab in the detail pane.
//     Owns the grouped cross-reference results displayed in the tab.
//     LoadAsync scans the assembly on a background thread (XRefService).
//     NavigateRequested fires when the user double-clicks an entry to navigate.
//
// Architecture Notes:
//     Pattern: MVVM — populated by AssemblyDetailViewModel.ShowNodeAsync.
//     Results are grouped into XRefGroupViewModel collections (CalledBy / Calls /
//     FieldReads / FieldWrites), each containing XRefEntryViewModel items.
//     LRU cache (max 20 entries) prevents re-scanning on repeated selections.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.AssemblyAnalysis.Models; // XRefEntry, XRefResult, MemberModel
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

// ── Entry VM ──────────────────────────────────────────────────────────────────

/// <summary>A single cross-reference entry displayed in the XRefs tab list.</summary>
public sealed class XRefEntryViewModel
{
    private readonly Action<int> _navigate;

    public XRefEntryViewModel(XRefEntry entry, Action<int> navigate)
    {
        Entry     = entry;
        _navigate = navigate;
        NavigateCommand = new RelayCommand(() => _navigate(Entry.MetadataToken));
    }

    public XRefEntry Entry            { get; }
    public string    TypeName         => Entry.TypeFullName;
    public string    MemberSignature  => Entry.MemberSignature;
    public ICommand  NavigateCommand  { get; }
}

// ── Group VM ──────────────────────────────────────────────────────────────────

/// <summary>A named group of cross-reference entries (e.g. "Called By 3").</summary>
public sealed class XRefGroupViewModel
{
    public XRefGroupViewModel(string label, IEnumerable<XRefEntryViewModel> entries)
    {
        Label   = label;
        Entries = new ObservableCollection<XRefEntryViewModel>(entries);
    }

    public string                                  Label   { get; }
    public ObservableCollection<XRefEntryViewModel> Entries { get; }
    public bool                                    IsEmpty => Entries.Count == 0;
}


// ── XRef tab root VM ─────────────────────────────────────────────────────────

/// <summary>
/// Provides grouped cross-reference data for the XRefs tab.
/// </summary>
public sealed class XRefViewModel : AssemblyNodeViewModel
{
    // ── LRU cache keyed by (filePath, metadataToken) ──────────────────────────
    private const int MaxCacheSize = 20;
    private readonly Dictionary<(string, int), XRefResult>        _cache      = new();
    private readonly LinkedList<(string, int)>                     _cacheOrder = new();

    // ── AssemblyNodeViewModel overrides ───────────────────────────────────────
    public override string DisplayName => "XRefs";
    public override string IconGlyph   => "\uE71B"; // Search

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    private bool _isAvailable;
    public bool IsAvailable
    {
        get => _isAvailable;
        private set => SetField(ref _isAvailable, value);
    }

    public ObservableCollection<XRefGroupViewModel> Groups { get; } = [];

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>Fired when the user double-clicks an XRef entry to navigate to it.</summary>
    public event Action<int>? NavigateRequested;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Asynchronously scans the assembly for cross-references to <paramref name="member"/>.
    /// Uses an LRU cache to avoid redundant scans on repeated selections.
    /// </summary>
    public async Task LoadAsync(
        MemberModel       member,
        string            filePath,
        CancellationToken ct)
    {
        Groups.Clear();
        IsAvailable = false;
        IsLoading   = true;

        try
        {
            var key = (filePath, member.MetadataToken);
            XRefResult? result = null;

            if (_cache.TryGetValue(key, out var cached))
                result = cached;

            if (result is null)
            {
                result = await Task.Run(
                    () => XRefService.BuildXRefs(member, filePath, ct), ct);

                if (ct.IsCancellationRequested) return;
                CacheSet(key, result);
            }

            BuildGroups(result);
            IsAvailable = Groups.Any(g => !g.IsEmpty);
        }
        catch (OperationCanceledException) { /* Silent */ }
        catch (Exception ex)
        {
            Groups.Add(new XRefGroupViewModel($"// XRef scan failed: {ex.Message}", []));
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Clears results and resets loading state.</summary>
    public void Clear()
    {
        Groups.Clear();
        IsAvailable = false;
        IsLoading   = false;
    }

    /// <summary>Evicts all cached results for the given file path.</summary>
    public void InvalidateFile(string filePath)
    {
        var keys = _cache.Keys.Where(k => k.Item1 == filePath).ToList();
        foreach (var key in keys)
        {
            _cache.Remove(key);
            var node = _cacheOrder.Find(key);
            if (node is not null) _cacheOrder.Remove(node);
        }
    }

    // ── Group builder ─────────────────────────────────────────────────────────

    private void BuildGroups(XRefResult result)
    {
        Groups.Clear();
        AddGroup($"Called By ({result.CalledBy.Count})",   result.CalledBy);
        AddGroup($"Calls ({result.Calls.Count})",          result.Calls);
        AddGroup($"Field Reads ({result.FieldReads.Count})", result.FieldReads);
        AddGroup($"Field Writes ({result.FieldWrites.Count})", result.FieldWrites);
    }

    private void AddGroup(string label, IReadOnlyList<XRefEntry> entries)
    {
        var entryVMs = entries.Select(e => new XRefEntryViewModel(e, t => NavigateRequested?.Invoke(t)));
        Groups.Add(new XRefGroupViewModel(label, entryVMs));
    }

    // ── LRU cache ─────────────────────────────────────────────────────────────

    private void CacheSet((string, int) key, XRefResult value)
    {
        if (_cache.ContainsKey(key))
        {
            _cacheOrder.Remove(_cacheOrder.Find(key)!);
        }
        else if (_cache.Count >= MaxCacheSize)
        {
            var oldest = _cacheOrder.First!.Value;
            _cacheOrder.RemoveFirst();
            _cache.Remove(oldest);
        }
        _cache[key] = value;
        _cacheOrder.AddLast(key);
    }
}
