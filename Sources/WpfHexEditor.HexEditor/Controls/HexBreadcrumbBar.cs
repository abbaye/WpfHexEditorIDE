//////////////////////////////////////////////
// Project: WpfHexEditor.HexEditor
// File: Controls/HexBreadcrumbBar.cs
// Description:
//     Interactive navigable breadcrumb bar for the HexEditor.
//     Displays hierarchical format path (e.g. "PE/COFF Executable 95% › DOS Header › e_lfanew").
//     Each segment is clickable to navigate to the corresponding offset.
//     Chevron dropdowns show sibling nodes for quick jump navigation.
//     Navigation bookmarks rendered as icon chips for quick access.
// Architecture:
//     Border-based WPF control using StackPanel + TextBlock segments.
//     Follows LspBreadcrumbBar pattern (CodeEditor). Theme tokens: BC_*.
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.HexEditor.Controls;

/// <summary>How the offset is displayed in the breadcrumb bar.</summary>
public enum BreadcrumbOffsetFormat
{
    /// <summary>Show hex only: 0x0001A4</summary>
    Hex,
    /// <summary>Show decimal only: 420</summary>
    Decimal,
    /// <summary>Show both: 0x0001A4 (420)</summary>
    Both,
}

/// <summary>
/// A single segment in the breadcrumb path (format → group → field).
/// </summary>
public sealed class BreadcrumbSegment
{
    public string Name { get; init; } = "";
    public long Offset { get; init; }
    public int Length { get; init; }
    public bool IsGroup { get; init; }
    public string? Color { get; init; }
    public List<BreadcrumbSegment>? Siblings { get; init; }
    public bool IsFormat { get; init; }
    public int Confidence { get; init; }
}

/// <summary>
/// Event args for breadcrumb navigation (carries offset + length for selection).
/// </summary>
public sealed class BreadcrumbNavigateEventArgs : EventArgs
{
    public long Offset { get; init; }
    public int Length { get; init; }
}

/// <summary>
/// Interactive breadcrumb bar: clickable segments, chevron dropdowns, bookmark chips.
/// Fires <see cref="NavigateRequested"/> when user clicks a segment to jump to an offset.
/// </summary>
public sealed class HexBreadcrumbBar : Border
{
    // ── Layout ────────────────────────────────────────────────────────────────
    private readonly StackPanel _rootPanel;
    private readonly TextBlock _offsetText;
    private readonly TextBlock _selLenText;
    private readonly Border _separator1;
    private readonly StackPanel _pathPanel;
    private readonly Border _separator2;
    private readonly StackPanel _bookmarkPanel;

    // ── State ─────────────────────────────────────────────────────────────────
    private long _offset;
    private long _selectionLength;
    private List<BreadcrumbSegment> _segments = new();
    private Popup? _activePopup;

    // ── Configurable ──────────────────────────────────────────────────────────
    public BreadcrumbOffsetFormat OffsetFormat { get; set; } = BreadcrumbOffsetFormat.Both;
    public bool ShowFormatInfo { get; set; } = true;
    public bool ShowFieldPath { get; set; } = true;
    public bool ShowSelectionLength { get; set; } = true;
    public new double FontSize { get; set; } = 11.5;
    public double BarHeight
    {
        get => Height;
        set => Height = value;
    }

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Raised when user clicks a breadcrumb segment or bookmark to navigate.</summary>
    public event EventHandler<BreadcrumbNavigateEventArgs>? NavigateRequested;

    // ── Static brushes ────────────────────────────────────────────────────────
    private static readonly Brush SeparatorLineBrush;

    static HexBreadcrumbBar()
    {
        SeparatorLineBrush = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128));
        SeparatorLineBrush.Freeze();
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public HexBreadcrumbBar()
    {
        Height = 22;
        Padding = new Thickness(6, 0, 6, 0);
        SnapsToDevicePixels = true;
        SetResourceReference(BackgroundProperty, "BC_Background");
        BorderThickness = new Thickness(0, 0, 0, 1);
        BorderBrush = SeparatorLineBrush;

        _offsetText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
            FontSize = FontSize,
            Cursor = Cursors.Arrow,
        };
        _offsetText.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarButtonActiveBrush");

        _selLenText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = FontSize - 1.5,
            Margin = new Thickness(6, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        _selLenText.SetResourceReference(TextBlock.ForegroundProperty, "BC_Foreground");

        _separator1 = CreateSeparatorBorder();
        _pathPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _separator2 = CreateSeparatorBorder();
        _separator2.Visibility = Visibility.Collapsed;
        _bookmarkPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        _rootPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _rootPanel.Children.Add(_offsetText);
        _rootPanel.Children.Add(_selLenText);
        _rootPanel.Children.Add(_separator1);
        _rootPanel.Children.Add(_pathPanel);
        _rootPanel.Children.Add(_separator2);
        _rootPanel.Children.Add(_bookmarkPanel);

        Child = _rootPanel;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Updates the breadcrumb state. Call on selection change.</summary>
    public void SetState(long offset, long selectionLength, string? formatName, int confidence,
        List<BreadcrumbSegment>? segments)
    {
        _offset = offset;
        _selectionLength = selectionLength;
        _segments = segments ?? new List<BreadcrumbSegment>();

        UpdateOffsetDisplay();
        UpdateSelectionDisplay();
        RenderPathSegments();
    }

    /// <summary>Sets navigation bookmark chips from the format's bookmark definitions.</summary>
    public void SetBookmarks(IEnumerable<FormatNavigationBookmark>? bookmarks)
    {
        _bookmarkPanel.Children.Clear();

        if (bookmarks == null)
        {
            _separator2.Visibility = Visibility.Collapsed;
            return;
        }

        var list = bookmarks.ToList();
        if (list.Count == 0)
        {
            _separator2.Visibility = Visibility.Collapsed;
            return;
        }

        _separator2.Visibility = Visibility.Visible;

        foreach (var bm in list)
        {
            var chip = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                ToolTip = $"{bm.Name}\nOffset: 0x{bm.Offset:X8}",
                Tag = bm.Offset,
                SnapsToDevicePixels = true,
            };
            chip.SetResourceReference(BackgroundProperty, "BC_HoverBackground");

            var chipText = new TextBlock
            {
                FontSize = FontSize - 2,
                VerticalAlignment = VerticalAlignment.Center,
                Text = string.IsNullOrEmpty(bm.Icon) ? bm.Name : $"{bm.Icon} {bm.Name}",
            };
            chipText.SetResourceReference(TextBlock.ForegroundProperty, "BC_Foreground");
            chip.Child = chipText;

            chip.MouseEnter += (_, _) => chip.SetResourceReference(BackgroundProperty, "SE_HoverBrush");
            chip.MouseLeave += (_, _) => chip.SetResourceReference(BackgroundProperty, "BC_HoverBackground");
            chip.MouseLeftButtonDown += (s, e) =>
            {
                if (s is Border b && b.Tag is long bmOffset)
                {
                    NavigateRequested?.Invoke(this, new BreadcrumbNavigateEventArgs
                    {
                        Offset = bmOffset,
                        Length = 0,
                    });
                    e.Handled = true;
                }
            };

            _bookmarkPanel.Children.Add(chip);
        }
    }

    // ── Private: Offset display ───────────────────────────────────────────────

    private void UpdateOffsetDisplay()
    {
        _offsetText.FontSize = FontSize;
        _offsetText.Text = OffsetFormat switch
        {
            BreadcrumbOffsetFormat.Hex => $"0x{_offset:X8}",
            BreadcrumbOffsetFormat.Decimal => $"{_offset:N0}",
            _ => $"0x{_offset:X8} ({_offset:N0})",
        };
    }

    private void UpdateSelectionDisplay()
    {
        if (ShowSelectionLength && _selectionLength > 1)
        {
            _selLenText.Text = $"[{_selectionLength:N0} bytes]";
            _selLenText.FontSize = FontSize - 1.5;
            _selLenText.Visibility = Visibility.Visible;
        }
        else
        {
            _selLenText.Visibility = Visibility.Collapsed;
        }
    }

    // ── Private: Path segments ────────────────────────────────────────────────

    private void RenderPathSegments()
    {
        _pathPanel.Children.Clear();

        if (_segments.Count == 0)
        {
            _separator1.Visibility = Visibility.Collapsed;
            return;
        }

        _separator1.Visibility = Visibility.Visible;

        for (int i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            bool isLast = i == _segments.Count - 1;

            // Separator chevron before each segment (except first)
            if (i > 0)
            {
                var sep = new TextBlock
                {
                    Text = " \u203A ",  // ›
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = FontSize,
                };
                sep.SetResourceReference(TextBlock.ForegroundProperty, "BC_SeparatorForeground");
                _pathPanel.Children.Add(sep);
            }

            // Segment container (label + optional chevron)
            var segPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Format segment: name only (no confidence badge)
            if (seg.IsFormat && ShowFormatInfo)
            {
                var fmtLabel = CreateSegmentLabel(seg, isLast);
                segPanel.Children.Add(fmtLabel);
            }
            else if (!seg.IsFormat)
            {
                var label = CreateSegmentLabel(seg, isLast);
                segPanel.Children.Add(label);
            }
            else
            {
                // Format segment but ShowFormatInfo is false — skip
                continue;
            }

            // Chevron dropdown button (for non-leaf segments with siblings)
            if (!isLast && seg.Siblings?.Count > 0)
            {
                var chevron = new TextBlock
                {
                    Text = " \u25BE",  // ▾
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = FontSize - 2,
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(1, 0, 2, 0),
                    Tag = seg,
                };
                chevron.SetResourceReference(TextBlock.ForegroundProperty, "BC_SeparatorForeground");
                chevron.MouseEnter += (_, _) => chevron.SetResourceReference(TextBlock.ForegroundProperty, "BC_Foreground");
                chevron.MouseLeave += (_, _) => chevron.SetResourceReference(TextBlock.ForegroundProperty, "BC_SeparatorForeground");
                chevron.MouseLeftButtonDown += OnChevronClick;
                segPanel.Children.Add(chevron);
            }

            _pathPanel.Children.Add(segPanel);
        }
    }

    private TextBlock CreateSegmentLabel(BreadcrumbSegment seg, bool isLast)
    {
        var lbl = new TextBlock
        {
            Text = seg.Name,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = FontSize,
            FontWeight = isLast ? FontWeights.SemiBold : FontWeights.Normal,
            Cursor = Cursors.Hand,
            Padding = new Thickness(2, 1, 2, 1),
            Tag = seg,
        };

        if (isLast)
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "Panel_ToolbarButtonActiveBrush");
        else
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "BC_Foreground");

        lbl.MouseEnter += (_, _) => lbl.SetResourceReference(BackgroundProperty, "BC_HoverBackground");
        lbl.MouseLeave += (_, _) => lbl.Background = Brushes.Transparent;
        lbl.MouseLeftButtonDown += OnSegmentClick;

        return lbl;
    }

    // ── Click handlers ────────────────────────────────────────────────────────

    private void OnSegmentClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is BreadcrumbSegment seg)
        {
            NavigateRequested?.Invoke(this, new BreadcrumbNavigateEventArgs
            {
                Offset = seg.Offset,
                Length = seg.Length,
            });
            e.Handled = true;
        }
    }

    private void OnChevronClick(object sender, MouseButtonEventArgs e)
    {
        CloseActivePopup();

        if (sender is not TextBlock chevron || chevron.Tag is not BreadcrumbSegment seg)
            return;
        if (seg.Siblings == null || seg.Siblings.Count == 0)
            return;

        var popup = new Popup
        {
            PlacementTarget = chevron,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
        };

        var listBox = new ListBox
        {
            MaxHeight = 300,
            MinWidth = 180,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
        };
        listBox.SetResourceReference(BackgroundProperty, "BC_Background");
        listBox.SetResourceReference(BorderBrushProperty, "BC_SeparatorForeground");

        // Add current node (highlighted) + siblings
        var allItems = new List<BreadcrumbSegment> { seg };
        allItems.AddRange(seg.Siblings);
        allItems.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        foreach (var item in allItems)
        {
            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 2, 4, 2),
            };

            var nameText = new TextBlock
            {
                Text = item.Name,
                FontSize = FontSize,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = item.Offset == seg.Offset ? FontWeights.SemiBold : FontWeights.Normal,
            };
            nameText.SetResourceReference(TextBlock.ForegroundProperty, "BC_Foreground");

            var offsetLabel = new TextBlock
            {
                Text = $"  0x{item.Offset:X}",
                FontSize = FontSize - 2,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                Opacity = 0.6,
            };
            offsetLabel.SetResourceReference(TextBlock.ForegroundProperty, "BC_Foreground");

            itemPanel.Children.Add(nameText);
            itemPanel.Children.Add(offsetLabel);

            var lbi = new ListBoxItem
            {
                Content = itemPanel,
                Tag = item.Offset,
                Cursor = Cursors.Hand,
                Padding = new Thickness(6, 3, 6, 3),
            };

            if (item.Offset == seg.Offset)
                lbi.SetResourceReference(BackgroundProperty, "BC_HoverBackground");

            var capturedItem = item;
            lbi.MouseLeftButtonUp += (s, _) =>
            {
                if (s is ListBoxItem)
                {
                    NavigateRequested?.Invoke(this, new BreadcrumbNavigateEventArgs
                    {
                        Offset = capturedItem.Offset,
                        Length = capturedItem.Length,
                    });
                    popup.IsOpen = false;
                }
            };

            listBox.Items.Add(lbi);
        }

        popup.Child = new Border
        {
            Child = listBox,
            CornerRadius = new CornerRadius(4),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8,
                Opacity = 0.3,
                ShadowDepth = 2,
            },
        };

        popup.Closed += (_, _) => { if (_activePopup == popup) _activePopup = null; };
        _activePopup = popup;
        popup.IsOpen = true;
        e.Handled = true;
    }

    private void CloseActivePopup()
    {
        if (_activePopup is { IsOpen: true })
            _activePopup.IsOpen = false;
        _activePopup = null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Border CreateSeparatorBorder()
    {
        return new Border
        {
            Width = 1,
            Margin = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = SeparatorLineBrush,
        };
    }
}
