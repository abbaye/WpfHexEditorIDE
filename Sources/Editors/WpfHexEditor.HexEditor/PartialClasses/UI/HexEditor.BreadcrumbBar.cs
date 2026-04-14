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
using System.Windows.Threading;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.HexEditor.Controls;

namespace WpfHexEditor.HexEditor;

public partial class HexEditor
{
    private HexBreadcrumbBar? _breadcrumbBar;
    private CustomBackgroundBlock? _bcLastBlock;
    private bool _bcLastBlockWasNull;    // disambiguates null from "not yet set"
    private bool _bcBookmarksRendered;   // true once SetBookmarks has been called for the current format
    private List<FormatNavigationBookmark>? _bcCachedBookmarks;
    private List<BreadcrumbSection>? _bcSections;

    // Set to true when OnBreadcrumbNavigate processes a click; cleared only when
    // UpdateBreadcrumb is called from a non-navigation source (real cursor move).
    // While true, UpdateBreadcrumb only updates the offset text — it never calls
    // SetSegments/SetBookmarks, so Children.Clear() never runs during a mouse event.
    private bool _bcNavigationPending;

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

        // The breadcrumb bar is already declared in the XAML template (x:Name="BreadcrumbBar").
        // Re-use that instance instead of injecting a second one into the grid at runtime,
        // which would produce a duplicate empty row above the real bar.
        _breadcrumbBar = this.FindName("BreadcrumbBar") as HexBreadcrumbBar;
        if (_breadcrumbBar is null) return;

        _breadcrumbBar.NavigateRequested += OnBreadcrumbNavigate;
    }

    private void OnBreadcrumbNavigate(object? sender, BreadcrumbNavigateEventArgs e)
    {
        // Block all visual tree mutations (SetSegments / SetBookmarks → Children.Clear) while
        // we are still inside the mouse event chain. We set _bcNavigationPending=true here and
        // clear it in the deferred action below, which runs at Render priority — after WPF has
        // fully processed and closed the originating MouseDown / MenuItem.Click event.
        // Until then, UpdateBreadcrumb() only refreshes the offset text and exits early,
        // so RenderPathSegments (which does Children.Clear + re-Add) is never called.
        _bcNavigationPending = true;

        SetPosition(e.Offset);
        if (e.Length > 0 && e.Length <= 256)
            SelectionStop = e.Offset + e.Length - 1;

        // Invalidate block cache so the deferred rebuild picks up the new position.
        _bcLastBlock = null;
        _bcLastBlockWasNull = false;

        // Use Render priority (higher than Input) so the flag is cleared and the rebuild
        // runs in a single dispatcher frame, after all input processing is done.
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            _bcNavigationPending = false;
            UpdateBreadcrumb();
        });
    }

    // ── Update ────────────────────────────────────────────────────────────────

    internal void UpdateBreadcrumb()
    {
        if (_breadcrumbBar is null || _breadcrumbBar.Visibility != Visibility.Visible) return;

        var offset = SelectionStart >= 0 ? SelectionStart : 0;
        var selLen = (SelectionStop > SelectionStart) ? SelectionStop - SelectionStart + 1 : 0;

        // Always update the offset text — it is safe (no Children.Clear).
        _breadcrumbBar.UpdateOffsetOnly(offset, selLen);

        // Do NOT rebuild segments while a BCB navigation click is still being processed.
        // SetSegments → RenderPathSegments does Children.Clear() + re-Add, which causes WPF
        // to re-dispatch MouseLeftButtonDown to the newly created element → infinite loop.
        // The deferred Render-priority action in OnBreadcrumbNavigate clears this flag and
        // calls UpdateBreadcrumb() again once the visual tree is safe to mutate.
        if (_bcNavigationPending) return;

        var block = _customBackgroundService.GetBlockAt(offset);

        // Early-exit when the cursor is still inside the same block.
        // _bcLastBlockWasNull tracks the previous null state so that "null block → null block"
        // (cursor moving through untagged bytes) also triggers the early exit instead of
        // rebuilding the segment list on every keystroke / mouse move.
        bool blockIsNull = block is null;
        if (block == _bcLastBlock && blockIsNull == _bcLastBlockWasNull) return;

        _bcLastBlock = block;
        _bcLastBlockWasNull = blockIsNull;

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

        // Bookmarks are format-level data: they never change during normal navigation.
        // Only call SetBookmarks once per format load (or after ResetBreadcrumbCache).
        // Calling it on every UpdateBreadcrumb causes Children.Clear() while a bookmark
        // chip's MouseDown is still on the call stack → WPF re-dispatches the event to
        // the newly-created chip at the same position → infinite NavigateRequested loop.
        if (!_bcBookmarksRendered)
        {
            if (_bcCachedBookmarks == null)
                _bcCachedBookmarks = ResolveBreadcrumbBookmarks();
            _breadcrumbBar.SetBookmarks(_bcCachedBookmarks);
            _bcBookmarksRendered = true;
        }
    }

    internal void ResetBreadcrumbCache()
    {
        _bcLastBlock = null;
        _bcLastBlockWasNull = false;
        _bcNavigationPending = false;
        _bcCachedBookmarks = null;
        _bcBookmarksRendered = false; // allow SetBookmarks on next UpdateBreadcrumb
        _bcSections = null;           // force section index rebuild on next update
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
