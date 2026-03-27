//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: PartialClasses/UI/HexEditor.BreadcrumbBar.cs
// Description:
//     Wires the interactive HexBreadcrumbBar into the HexEditor layout.
//     Simple linear scan of ParsedFields + skip-if-same-field optimization.
//     Builds Format › GroupName › FieldName breadcrumb path.
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
    private ParsedFieldViewModel? _bcLastMatch;

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

        // Always update offset text (cheap)
        _breadcrumbBar.UpdateOffsetOnly(offset, selLen);

        // Find field at offset
        var panel = ParsedFieldsPanel;
        var fields = panel?.ParsedFields;
        ParsedFieldViewModel? match = null;

        if (fields != null && fields.Count > 0)
        {
            long bestLen = long.MaxValue;
            foreach (var f in fields)
            {
                if (f.Length <= 0) continue;
                if (offset >= f.Offset && offset < f.Offset + f.Length && f.Length < bestLen)
                {
                    match = f;
                    bestLen = f.Length;
                }
            }
        }

        // Skip segment rebuild if same field as last time
        if (match == _bcLastMatch && _bcLastMatch != null) return;
        _bcLastMatch = match;

        // Build segments
        var segments = BuildSegments(fields, match);
        _breadcrumbBar.SetSegments(segments);
        _breadcrumbBar.SetBookmarks(panel?.FormatInfo?.Bookmarks);
    }

    private List<BreadcrumbSegment> BuildSegments(
        System.Collections.ObjectModel.ObservableCollection<ParsedFieldViewModel>? fields,
        ParsedFieldViewModel? match)
    {
        var segments = new List<BreadcrumbSegment>(3);

        // 1. Format segment — siblings = all groups
        var formatName = _detectedFormat?.FormatName;
        if (!string.IsNullOrEmpty(formatName))
        {
            List<BreadcrumbSegment>? groupSegs = null;
            if (fields != null && fields.Count > 0)
            {
                var seenGroups = new HashSet<string>();
                groupSegs = new List<BreadcrumbSegment>();
                foreach (var f in fields)
                {
                    if (f.Length <= 0 || string.IsNullOrEmpty(f.GroupName)) continue;
                    if (!seenGroups.Add(f.GroupName!)) continue;

                    long gMin = long.MaxValue, gMax = 0;
                    foreach (var gf in fields)
                    {
                        if (gf.GroupName != f.GroupName || gf.Length <= 0) continue;
                        if (gf.Offset < gMin) gMin = gf.Offset;
                        var end = gf.Offset + gf.Length;
                        if (end > gMax) gMax = end;
                    }
                    groupSegs.Add(new BreadcrumbSegment
                    {
                        Name = f.GroupName!,
                        Offset = gMin,
                        Length = (int)Math.Min(gMax - gMin, int.MaxValue),
                        IsGroup = true,
                    });
                }
                groupSegs.Sort((a, b) => a.Offset.CompareTo(b.Offset));
            }

            segments.Add(new BreadcrumbSegment
            {
                Name = formatName!,
                Offset = 0,
                Length = (int)Math.Min(Length, int.MaxValue),
                IsFormat = true,
                Siblings = groupSegs,
            });
        }

        if (match == null || fields == null || !BreadcrumbShowFieldPath) return segments;

        // 2. Group segment — siblings = other groups
        if (!string.IsNullOrEmpty(match.GroupName))
        {
            long gMin = long.MaxValue, gMax = 0;
            foreach (var f in fields)
            {
                if (f.GroupName != match.GroupName || f.Length <= 0) continue;
                if (f.Offset < gMin) gMin = f.Offset;
                var end = f.Offset + f.Length;
                if (end > gMax) gMax = end;
            }

            var groupSiblings = new List<BreadcrumbSegment>();
            var seen = new HashSet<string> { match.GroupName! };
            foreach (var f in fields)
            {
                if (f.Length <= 0 || string.IsNullOrEmpty(f.GroupName)) continue;
                if (!seen.Add(f.GroupName!)) continue;

                long sMin = long.MaxValue, sMax = 0;
                foreach (var sf in fields)
                {
                    if (sf.GroupName != f.GroupName || sf.Length <= 0) continue;
                    if (sf.Offset < sMin) sMin = sf.Offset;
                    var se = sf.Offset + sf.Length;
                    if (se > sMax) sMax = se;
                }
                groupSiblings.Add(new BreadcrumbSegment
                {
                    Name = f.GroupName!,
                    Offset = sMin,
                    Length = (int)Math.Min(sMax - sMin, int.MaxValue),
                    IsGroup = true,
                });
            }

            segments.Add(new BreadcrumbSegment
            {
                Name = match.GroupName!,
                Offset = gMin,
                Length = (int)Math.Min(gMax - gMin, int.MaxValue),
                IsGroup = true,
                Siblings = groupSiblings,
            });
        }

        // 3. Field segment — siblings = other fields in same group
        var fieldSiblings = new List<BreadcrumbSegment>();
        foreach (var f in fields)
        {
            if (f == match || f.Length <= 0 || f.GroupName != match.GroupName) continue;
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
