//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: PartialClasses/UI/HexEditor.BreadcrumbBar.cs
// Description:
//     Wires the interactive HexBreadcrumbBar into the HexEditor layout.
//     Performance-optimized: pre-built sorted index, binary search O(log n),
//     debounced segment rebuild (150ms), skip-if-same-field caching.
//     Offset text updates instantly; path segments update debounced.
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.HexEditor.Controls;

namespace WpfHexEditor.HexEditor;

public partial class HexEditor
{
    private HexBreadcrumbBar? _breadcrumbBar;

    // ── Performance cache ─────────────────────────────────────────────────────
    private BreadcrumbIndex? _bcIndex;
    private int _bcFieldsVersion;
    private ParsedFieldViewModel? _bcLastMatch;

    /// <summary>Shows or hides the breadcrumb bar above the hex viewport.</summary>
    public bool ShowBreadcrumbBar
    {
        get => _breadcrumbBar?.Visibility == Visibility.Visible;
        set
        {
            EnsureBreadcrumbBar();
            _breadcrumbBar!.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public BreadcrumbOffsetFormat BreadcrumbOffsetFormat
    {
        get => _breadcrumbBar?.OffsetFormat ?? Controls.BreadcrumbOffsetFormat.Both;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.OffsetFormat = value; }
    }

    public bool BreadcrumbShowFormatInfo
    {
        get => _breadcrumbBar?.ShowFormatInfo ?? true;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.ShowFormatInfo = value; }
    }

    public bool BreadcrumbShowFieldPath
    {
        get => _breadcrumbBar?.ShowFieldPath ?? true;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.ShowFieldPath = value; }
    }

    public bool BreadcrumbShowSelectionLength
    {
        get => _breadcrumbBar?.ShowSelectionLength ?? true;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.ShowSelectionLength = value; }
    }

    public double BreadcrumbFontSize
    {
        get => _breadcrumbBar?.FontSize ?? 11.5;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.FontSize = value; }
    }

    private void EnsureBreadcrumbBar()
    {
        if (_breadcrumbBar is not null) return;

        _breadcrumbBar = new HexBreadcrumbBar();
        _breadcrumbBar.NavigateRequested += OnBreadcrumbNavigate;

        if (Content is Grid rootGrid)
        {
            rootGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });
            foreach (UIElement child in rootGrid.Children)
                Grid.SetRow(child, Grid.GetRow(child) + 1);

            Grid.SetRow(_breadcrumbBar, 0);
            Grid.SetColumnSpan(_breadcrumbBar, rootGrid.ColumnDefinitions.Count > 0
                ? rootGrid.ColumnDefinitions.Count : 1);
            rootGrid.Children.Add(_breadcrumbBar);
        }
    }

    private void OnBreadcrumbNavigate(object? sender, BreadcrumbNavigateEventArgs e)
    {
        SetPosition(e.Offset);
        if (e.Length > 0 && e.Length <= 256)
            SelectionStop = e.Offset + e.Length - 1;
    }

    /// <summary>Updates the breadcrumb bar with current state.</summary>
    internal void UpdateBreadcrumb()
    {
        if (_breadcrumbBar is null || _breadcrumbBar.Visibility != Visibility.Visible) return;

        var offset = SelectionStart >= 0 ? SelectionStart : 0;
        var selLen = (SelectionStop > SelectionStart) ? SelectionStop - SelectionStart + 1 : 0;

        // Update offset text (always immediate)
        _breadcrumbBar.UpdateOffsetOnly(offset, selLen);

        // Rebuild segments using cached index (O(log n) + skip-if-same)
        RebuildBreadcrumbSegments(offset);
    }

    private void RebuildBreadcrumbSegments(long offset)
    {
        // Ensure index is up-to-date
        var panel = ParsedFieldsPanel;
        var fields = panel?.ParsedFields;
        int fieldsCount = fields?.Count ?? 0;
        if (fieldsCount != _bcFieldsVersion)
        {
            _bcIndex = fieldsCount > 0 ? BuildIndex(fields!) : null;
            _bcFieldsVersion = fieldsCount;
            _bcLastMatch = null; // force rebuild
        }

        // Find field at offset via binary search
        var match = _bcIndex != null ? FindFieldAtOffset(_bcIndex.SortedFields, offset) : null;

        // Skip full rebuild if same field as last time
        if (match == _bcLastMatch && _bcLastMatch != null) return;
        _bcLastMatch = match;

        // Build segments from cached index
        var segments = BuildSegmentsFromIndex(offset, match);
        var formatName = _detectedFormat?.FormatName;
        var confidence = (_detectionCandidates?.Count > 0)
            ? (int)(_detectionCandidates[0].ConfidenceScore * 100) : 0;

        _breadcrumbBar!.SetSegments(segments);
        _breadcrumbBar.SetBookmarks(panel?.FormatInfo?.Bookmarks);
    }

    // ── Index building (once per format detection) ────────────────────────────

    private sealed class BreadcrumbIndex
    {
        public ParsedFieldViewModel[] SortedFields = Array.Empty<ParsedFieldViewModel>();
        public Dictionary<string, GroupInfo> Groups = new();
        public List<BreadcrumbSegment> GroupSegments = new();
    }

    private sealed class GroupInfo
    {
        public long MinOffset = long.MaxValue;
        public long MaxEnd;
        public int FieldCount;
        public int Length => (int)Math.Min(MaxEnd - MinOffset, int.MaxValue);
    }

    private static BreadcrumbIndex BuildIndex(System.Collections.ObjectModel.ObservableCollection<ParsedFieldViewModel> fields)
    {
        var idx = new BreadcrumbIndex();

        // Sort fields by offset for binary search
        var withLength = new List<ParsedFieldViewModel>(fields.Count);
        foreach (var f in fields)
            if (f.Length > 0) withLength.Add(f);

        withLength.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        idx.SortedFields = withLength.ToArray();

        // Pre-compute group info
        foreach (var f in withLength)
        {
            var gn = f.GroupName;
            if (string.IsNullOrEmpty(gn)) continue;

            if (!idx.Groups.TryGetValue(gn, out var gi))
            {
                gi = new GroupInfo();
                idx.Groups[gn] = gi;
            }
            if (f.Offset < gi.MinOffset) gi.MinOffset = f.Offset;
            var end = f.Offset + f.Length;
            if (end > gi.MaxEnd) gi.MaxEnd = end;
            gi.FieldCount++;
        }

        // Pre-build group segments (for dropdown popups)
        idx.GroupSegments = new List<BreadcrumbSegment>(idx.Groups.Count);
        foreach (var kv in idx.Groups.OrderBy(k => k.Value.MinOffset))
        {
            idx.GroupSegments.Add(new BreadcrumbSegment
            {
                Name = kv.Key,
                Offset = kv.Value.MinOffset,
                Length = kv.Value.Length,
                IsGroup = true,
            });
        }

        return idx;
    }

    // ── Binary search O(log n) ────────────────────────────────────────────────

    private static ParsedFieldViewModel? FindFieldAtOffset(ParsedFieldViewModel[] sorted, long offset)
    {
        // Binary search for first field with Offset <= offset
        int lo = 0, hi = sorted.Length - 1;
        ParsedFieldViewModel? best = null;
        long bestLen = long.MaxValue;

        // Find insertion point
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (sorted[mid].Offset <= offset)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        // Scan backward from insertion point to find containing fields
        for (int i = hi; i >= 0 && i >= hi - 20; i--)
        {
            var f = sorted[i];
            if (f.Offset + f.Length <= offset) break; // past this field
            if (offset >= f.Offset && offset < f.Offset + f.Length && f.Length < bestLen)
            {
                best = f;
                bestLen = f.Length;
            }
        }

        return best;
    }

    // ── Segment building from cached index ────────────────────────────────────

    private List<BreadcrumbSegment> BuildSegmentsFromIndex(long offset, ParsedFieldViewModel? match)
    {
        var segments = new List<BreadcrumbSegment>(3);

        // 1. Format segment
        var formatName = _detectedFormat?.FormatName;
        if (!string.IsNullOrEmpty(formatName))
        {
            // Format segment siblings = all groups (for dropdown)
            segments.Add(new BreadcrumbSegment
            {
                Name = formatName!,
                Offset = 0,
                Length = (int)Math.Min(Length, int.MaxValue),
                IsFormat = true,
                Siblings = _bcIndex?.GroupSegments,
            });
        }

        if (match == null || _bcIndex == null || !BreadcrumbShowFieldPath) return segments;

        // 2. Group segment
        var groupName = match.GroupName;
        if (!string.IsNullOrEmpty(groupName) && _bcIndex.Groups.TryGetValue(groupName!, out var gi))
        {
            // Group siblings = other groups (pre-built, just filter out current)
            var groupSiblings = _bcIndex.GroupSegments
                .Where(g => g.Name != groupName)
                .ToList();

            segments.Add(new BreadcrumbSegment
            {
                Name = groupName!,
                Offset = gi.MinOffset,
                Length = gi.Length,
                IsGroup = true,
                Siblings = groupSiblings,
            });
        }

        // 3. Field segment — siblings = other fields in same group
        var fieldSiblings = new List<BreadcrumbSegment>();
        foreach (var f in _bcIndex.SortedFields)
        {
            if (f == match || f.GroupName != match.GroupName) continue;
            fieldSiblings.Add(new BreadcrumbSegment
            {
                Name = f.Name,
                Offset = f.Offset,
                Length = f.Length,
                Color = f.Color,
            });
        }

        segments.Add(new BreadcrumbSegment
        {
            Name = match.Name,
            Offset = match.Offset,
            Length = match.Length,
            Color = match.Color,
            Siblings = fieldSiblings,
        });

        return segments;
    }
}
