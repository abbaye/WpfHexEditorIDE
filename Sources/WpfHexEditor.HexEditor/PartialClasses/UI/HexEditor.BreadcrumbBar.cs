//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: PartialClasses/UI/HexEditor.BreadcrumbBar.cs
// Description:
//     Wires the interactive HexBreadcrumbBar into the HexEditor layout.
//     Searches the flat ParsedFields list by offset to build a 2-level breadcrumb:
//       Format Name › GroupName (section) › Field Name
//     Handles navigation + selection via SetPosition when user clicks segments.
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.HexEditor.Controls;

namespace WpfHexEditor.HexEditor;

public partial class HexEditor
{
    private HexBreadcrumbBar? _breadcrumbBar;

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

    /// <summary>Offset display format: Hex, Decimal, or Both.</summary>
    public BreadcrumbOffsetFormat BreadcrumbOffsetFormat
    {
        get => _breadcrumbBar?.OffsetFormat ?? Controls.BreadcrumbOffsetFormat.Both;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.OffsetFormat = value; }
    }

    /// <summary>Show format info (name + confidence) in breadcrumb.</summary>
    public bool BreadcrumbShowFormatInfo
    {
        get => _breadcrumbBar?.ShowFormatInfo ?? true;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.ShowFormatInfo = value; }
    }

    /// <summary>Show field path in breadcrumb.</summary>
    public bool BreadcrumbShowFieldPath
    {
        get => _breadcrumbBar?.ShowFieldPath ?? true;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.ShowFieldPath = value; }
    }

    /// <summary>Show selection length in breadcrumb.</summary>
    public bool BreadcrumbShowSelectionLength
    {
        get => _breadcrumbBar?.ShowSelectionLength ?? true;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.ShowSelectionLength = value; }
    }

    /// <summary>Font size for breadcrumb text.</summary>
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

        // Insert at the top of the root grid (row 0, before existing content)
        if (Content is Grid rootGrid)
        {
            rootGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });

            // Shift all existing children down by 1 row
            foreach (UIElement child in rootGrid.Children)
            {
                int row = Grid.GetRow(child);
                Grid.SetRow(child, row + 1);
            }

            Grid.SetRow(_breadcrumbBar, 0);
            Grid.SetColumnSpan(_breadcrumbBar, rootGrid.ColumnDefinitions.Count > 0
                ? rootGrid.ColumnDefinitions.Count : 1);
            rootGrid.Children.Add(_breadcrumbBar);
        }
    }

    private void OnBreadcrumbNavigate(object? sender, BreadcrumbNavigateEventArgs e)
    {
        SetPosition(e.Offset);
        // Only select range for small fields (≤256 bytes) to avoid freezing —
        // DataInspectorPanel reads all selected bytes synchronously via GetByte()
        if (e.Length > 0 && e.Length <= 256)
            SelectionStop = e.Offset + e.Length - 1;
    }

    /// <summary>Updates the breadcrumb bar with current state. Call from SelectionChanged handler.</summary>
    internal void UpdateBreadcrumb()
    {
        if (_breadcrumbBar is null || _breadcrumbBar.Visibility != Visibility.Visible) return;

        var offset = SelectionStart >= 0 ? SelectionStart : 0;
        var selLen = (SelectionStop > SelectionStart) ? SelectionStop - SelectionStart + 1 : 0;

        var formatName = _detectedFormat?.FormatName;
        var confidence = (_detectionCandidates?.Count > 0)
            ? (int)(_detectionCandidates[0].ConfidenceScore * 100)
            : 0;

        var segments = BuildBreadcrumbPath(offset, formatName, confidence);
        _breadcrumbBar.SetState(offset, selLen, formatName, confidence, segments);

        // Update bookmarks from parsed fields panel
        _breadcrumbBar.SetBookmarks(ParsedFieldsPanel?.FormatInfo?.Bookmarks);
    }

    // ── Flat list search + GroupName grouping ─────────────────────────────────

    private List<BreadcrumbSegment> BuildBreadcrumbPath(long offset, string? formatName, int confidence)
    {
        var segments = new List<BreadcrumbSegment>();

        // 1. Root format segment
        if (!string.IsNullOrEmpty(formatName))
        {
            segments.Add(new BreadcrumbSegment
            {
                Name = formatName!,
                Offset = 0,
                Length = (int)Math.Min(Length, int.MaxValue),
                IsFormat = true,
                Confidence = 0,
            });
        }

        // 2. Find field at current offset from flat ParsedFields list
        var panel = ParsedFieldsPanel;
        if (panel?.ParsedFields == null || !BreadcrumbShowFieldPath) return segments;

        var fields = panel.ParsedFields;
        if (fields.Count == 0) return segments;

        // Find the most specific field containing offset (smallest range)
        ParsedFieldViewModel? match = null;
        long bestLength = long.MaxValue;
        foreach (var f in fields)
        {
            if (f.Length <= 0) continue;
            if (offset >= f.Offset && offset < f.Offset + f.Length && f.Length < bestLength)
            {
                match = f;
                bestLength = f.Length;
            }
        }

        if (match == null) return segments;

        // 3. GroupName segment (section: "Signature", "Header Fields", "Data Fields", etc.)
        if (!string.IsNullOrEmpty(match.GroupName))
        {
            // Compute group offset range
            var groupFields = new List<ParsedFieldViewModel>();
            foreach (var f in fields)
                if (f.GroupName == match.GroupName && f.Length > 0)
                    groupFields.Add(f);

            long groupOffset = long.MaxValue;
            long groupEnd = 0;
            foreach (var gf in groupFields)
            {
                if (gf.Offset < groupOffset) groupOffset = gf.Offset;
                var end = gf.Offset + gf.Length;
                if (end > groupEnd) groupEnd = end;
            }

            // Siblings = other groups
            var seenGroups = new HashSet<string> { match.GroupName };
            var groupSiblings = new List<BreadcrumbSegment>();
            foreach (var f in fields)
            {
                if (f.Length <= 0 || string.IsNullOrEmpty(f.GroupName)) continue;
                if (!seenGroups.Add(f.GroupName)) continue;

                // Compute this sibling group's offset range
                long sOff = long.MaxValue, sEnd = 0;
                foreach (var sf in fields)
                {
                    if (sf.GroupName != f.GroupName || sf.Length <= 0) continue;
                    if (sf.Offset < sOff) sOff = sf.Offset;
                    var se = sf.Offset + sf.Length;
                    if (se > sEnd) sEnd = se;
                }

                groupSiblings.Add(new BreadcrumbSegment
                {
                    Name = f.GroupName,
                    Offset = sOff,
                    Length = (int)Math.Min(sEnd - sOff, int.MaxValue),
                    IsGroup = true,
                });
            }

            segments.Add(new BreadcrumbSegment
            {
                Name = match.GroupName,
                Offset = groupOffset,
                Length = (int)Math.Min(groupEnd - groupOffset, int.MaxValue),
                IsGroup = true,
                Siblings = groupSiblings,
            });
        }

        // 4. Field segment (leaf — the matched field)
        var fieldSiblings = new List<BreadcrumbSegment>();
        foreach (var f in fields)
        {
            if (f == match || f.Length <= 0) continue;
            if (f.GroupName != match.GroupName) continue;
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
