//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: PartialClasses/UI/HexEditor.BreadcrumbBar.cs
// Description:
//     Context-aware 3-level breadcrumb: Format > Section > Field.
//     Uses CustomBackgroundService (no plugin dependency).
//     Section index built from block opacity + offset proximity.
//     Each dropdown shows contextually relevant items based on cursor position.
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.HexEditor.Controls;

namespace WpfHexEditor.HexEditor;

public partial class HexEditor
{
    private HexBreadcrumbBar? _breadcrumbBar;
    private CustomBackgroundBlock? _bcLastBlock;
    private List<FormatNavigationBookmark>? _bcCachedBookmarks;
    private List<BreadcrumbSection>? _bcSections;

    public event EventHandler<BreadcrumbEnrichEventArgs>? BreadcrumbEnrichRequested;

    // ── Section index ─────────────────────────────────────────────────────────

    private sealed class BreadcrumbSection
    {
        public string Name = "";
        public long Offset;
        public long EndOffset;
        public List<CustomBackgroundBlock> Fields = new();
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public bool ShowBreadcrumbBar
    {
        get => _breadcrumbBar?.Visibility == Visibility.Visible;
        set { EnsureBreadcrumbBar(); _breadcrumbBar!.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
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

    // ── Setup ─────────────────────────────────────────────────────────────────

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

    // ── Update ────────────────────────────────────────────────────────────────

    internal void UpdateBreadcrumb()
    {
        if (_breadcrumbBar is null || _breadcrumbBar.Visibility != Visibility.Visible) return;

        var offset = SelectionStart >= 0 ? SelectionStart : 0;
        var selLen = (SelectionStop > SelectionStart) ? SelectionStop - SelectionStart + 1 : 0;

        _breadcrumbBar.UpdateOffsetOnly(offset, selLen);

        var block = _customBackgroundService.GetBlockAt(offset);
        if (block == _bcLastBlock && _bcLastBlock != null) return;
        _bcLastBlock = block;

        // Ensure section index
        if (_bcSections == null)
            _bcSections = BuildSectionIndex();

        var segments = BuildContextSegments(offset, block);

        // Plugin enrichment
        var enrichArgs = new BreadcrumbEnrichEventArgs
        {
            Offset = offset,
            Segments = segments.Select(s => new BreadcrumbSegmentData
            {
                Name = s.Name, Offset = s.Offset, Length = s.Length,
                IsGroup = s.IsGroup, IsFormat = s.IsFormat, Color = s.Color,
                Siblings = s.Siblings?.Select(si => new BreadcrumbSegmentData
                {
                    Name = si.Name, Offset = si.Offset, Length = si.Length,
                    IsGroup = si.IsGroup, Color = si.Color,
                }).ToList(),
            }).ToList(),
        };
        BreadcrumbEnrichRequested?.Invoke(this, enrichArgs);
        if (enrichArgs.IsEnriched)
            segments = ConvertFromEnriched(enrichArgs.Segments);

        _breadcrumbBar.SetSegments(segments);

        if (_bcCachedBookmarks == null)
            _bcCachedBookmarks = ResolveBreadcrumbBookmarks();
        _breadcrumbBar.SetBookmarks(_bcCachedBookmarks);
    }

    internal void ResetBreadcrumbCache()
    {
        _bcLastBlock = null;
        _bcCachedBookmarks = null;
        _bcSections = null; // force rebuild on next update
    }

    // ── Section index building ────────────────────────────────────────────────

    private List<BreadcrumbSection> BuildSectionIndex()
    {
        var allBlocks = _customBackgroundService.GetAllBlocks()
            .Where(b => b.Length > 0 && !string.IsNullOrEmpty(b.Description))
            .OrderBy(b => b.StartOffset)
            .ToList();

        if (allBlocks.Count == 0) return new List<BreadcrumbSection>();

        var sections = new List<BreadcrumbSection>();

        // Pass 1: Find explicit section parents (repeating blocks with low opacity)
        var parentBlocks = allBlocks
            .Where(b => b.Opacity <= 0.2 && b.Length > 8)
            .OrderBy(b => b.StartOffset)
            .ToList();

        // Pass 2: Assign fields to parent sections or create proximity-based sections
        var childBlocks = allBlocks
            .Where(b => b.Opacity > 0.2 || b.Length <= 8)
            .OrderBy(b => b.StartOffset)
            .ToList();

        // Create sections from parent blocks
        foreach (var parent in parentBlocks)
        {
            sections.Add(new BreadcrumbSection
            {
                Name = CleanSectionName(parent.Description),
                Offset = parent.StartOffset,
                EndOffset = parent.StartOffset + parent.Length,
            });
        }

        // Assign child blocks to sections or create new proximity sections
        BreadcrumbSection? currentProxSection = null;

        foreach (var child in childBlocks)
        {
            // Check if child falls within an explicit parent section
            var parentSection = sections.FirstOrDefault(s =>
                child.StartOffset >= s.Offset && child.StartOffset < s.EndOffset);

            if (parentSection != null)
            {
                parentSection.Fields.Add(child);
                continue;
            }

            // Proximity grouping: if gap > 16 bytes from last field, start new section
            if (currentProxSection == null ||
                child.StartOffset > currentProxSection.EndOffset + 16)
            {
                currentProxSection = new BreadcrumbSection
                {
                    Name = CleanFieldName(child.Description),
                    Offset = child.StartOffset,
                    EndOffset = child.StartOffset + child.Length,
                };
                sections.Add(currentProxSection);
            }
            else
            {
                // Extend current section
                var end = child.StartOffset + child.Length;
                if (end > currentProxSection.EndOffset)
                    currentProxSection.EndOffset = end;
            }

            currentProxSection.Fields.Add(child);
        }

        // Sort sections by offset
        sections.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        // Name proximity sections after their first field area
        foreach (var sec in sections)
        {
            if (sec.Fields.Count > 0 && sec.Name == CleanFieldName(sec.Fields[0].Description))
            {
                // Try to derive a better section name from the field group
                if (sec.Fields.Count > 3)
                    sec.Name = $"{CleanFieldName(sec.Fields[0].Description)} area ({sec.Fields.Count} fields)";
            }
        }

        return sections;
    }

    private static string CleanSectionName(string description)
    {
        // "PE Section [0]" → "PE Section [0]"
        // "DOS MZ Signature: 23117 (MZ magic...)" → "DOS MZ Signature"
        var colonIdx = description.IndexOf(':');
        if (colonIdx > 0 && colonIdx < 40)
            return description.Substring(0, colonIdx).Trim();
        return description.Length > 50 ? description.Substring(0, 50) + "..." : description;
    }

    private static string CleanFieldName(string description)
    {
        var colonIdx = description.IndexOf(':');
        if (colonIdx > 0 && colonIdx < 40)
            return description.Substring(0, colonIdx).Trim();
        var parenIdx = description.IndexOf('(');
        if (parenIdx > 0 && parenIdx < 50)
            return description.Substring(0, parenIdx).Trim();
        return description.Length > 50 ? description.Substring(0, 50) + "..." : description;
    }

    // ── Context-aware segment building ────────────────────────────────────────

    private List<BreadcrumbSegment> BuildContextSegments(long offset, CustomBackgroundBlock? block)
    {
        var segments = new List<BreadcrumbSegment>(3);
        var formatName = _detectedFormat?.FormatName;

        // 1. Format segment → dropdown shows ALL sections
        if (!string.IsNullOrEmpty(formatName) && _bcSections != null)
        {
            var sectionSiblings = _bcSections
                .Select(s => new BreadcrumbSegment
                {
                    Name = s.Name,
                    Offset = s.Offset,
                    Length = (int)Math.Min(s.EndOffset - s.Offset, int.MaxValue),
                    IsGroup = true,
                })
                .ToList();

            segments.Add(new BreadcrumbSegment
            {
                Name = formatName!,
                Offset = 0,
                Length = (int)Math.Min(Length, int.MaxValue),
                IsFormat = true,
                Siblings = sectionSiblings,
            });
        }
        else if (!string.IsNullOrEmpty(formatName))
        {
            segments.Add(new BreadcrumbSegment
            {
                Name = formatName!,
                Offset = 0,
                Length = (int)Math.Min(Length, int.MaxValue),
                IsFormat = true,
            });
        }

        if (block == null || _bcSections == null || !BreadcrumbShowFieldPath) return segments;

        // Find which section contains the current block
        var currentSection = _bcSections.FirstOrDefault(s =>
            block.StartOffset >= s.Offset && block.StartOffset < s.EndOffset);

        // 2. Section segment → dropdown shows sibling sections
        if (currentSection != null)
        {
            var sectionSiblings = _bcSections
                .Where(s => s != currentSection)
                .Select(s => new BreadcrumbSegment
                {
                    Name = s.Name,
                    Offset = s.Offset,
                    Length = (int)Math.Min(s.EndOffset - s.Offset, int.MaxValue),
                    IsGroup = true,
                })
                .ToList();

            segments.Add(new BreadcrumbSegment
            {
                Name = currentSection.Name,
                Offset = currentSection.Offset,
                Length = (int)Math.Min(currentSection.EndOffset - currentSection.Offset, int.MaxValue),
                IsGroup = true,
                Siblings = sectionSiblings,
            });

            // 3. Field segment → dropdown shows sibling fields in THIS section
            var fieldSiblings = currentSection.Fields
                .Where(f => f != block)
                .Select(f => new BreadcrumbSegment
                {
                    Name = CleanFieldName(f.Description),
                    Offset = f.StartOffset,
                    Length = (int)Math.Min(f.Length, int.MaxValue),
                })
                .ToList();

            segments.Add(new BreadcrumbSegment
            {
                Name = CleanFieldName(block.Description ?? $"0x{block.StartOffset:X}"),
                Offset = block.StartOffset,
                Length = (int)Math.Min(block.Length, int.MaxValue),
                Siblings = fieldSiblings,
            });
        }
        else
        {
            // No section found — just show field
            segments.Add(new BreadcrumbSegment
            {
                Name = CleanFieldName(block.Description ?? $"0x{block.StartOffset:X}"),
                Offset = block.StartOffset,
                Length = (int)Math.Min(block.Length, int.MaxValue),
            });
        }

        return segments;
    }

    // ── Bookmarks (no plugin needed) ──────────────────────────────────────────

    private List<FormatNavigationBookmark>? ResolveBreadcrumbBookmarks()
    {
        var nav = _detectedFormat?.Navigation;
        if (nav?.Bookmarks == null || _detectionVariables == null) return null;

        var result = new List<FormatNavigationBookmark>();
        foreach (var bm in nav.Bookmarks)
        {
            if (string.IsNullOrEmpty(bm.OffsetVar)) continue;
            if (!_detectionVariables.TryGetValue(bm.OffsetVar, out var val)) continue;
            try
            {
                long bmOffset = Convert.ToInt64(val);
                if (bmOffset < 0 || bmOffset >= Length) continue;
                result.Add(new FormatNavigationBookmark
                {
                    Name = bm.Name ?? bm.OffsetVar,
                    Offset = bmOffset,
                    Icon = bm.Icon,
                    Color = bm.Color,
                });
            }
            catch { }
        }
        return result.Count > 0 ? result : null;
    }

    // ── Enrichment helpers ────────────────────────────────────────────────────

    private static List<BreadcrumbSegment> ConvertFromEnriched(List<BreadcrumbSegmentData> data)
    {
        return data.Select(d => new BreadcrumbSegment
        {
            Name = d.Name, Offset = d.Offset, Length = d.Length,
            IsGroup = d.IsGroup, IsFormat = d.IsFormat, Color = d.Color,
            Siblings = d.Siblings?.Select(s => new BreadcrumbSegment
            {
                Name = s.Name, Offset = s.Offset, Length = s.Length,
                IsGroup = s.IsGroup, Color = s.Color,
            }).ToList(),
        }).ToList();
    }
}
