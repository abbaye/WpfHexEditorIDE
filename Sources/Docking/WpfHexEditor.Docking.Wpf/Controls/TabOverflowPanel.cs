// ==========================================================
// Project: WpfHexEditor.Shell
// File: TabOverflowPanel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Custom WPF Panel that arranges tab items horizontally and collapses those
//     that overflow the available width. Exposes HasOverflow and OverflowItems
//     dependency properties for the companion TabOverflowButton to consume.
//
// Architecture Notes:
//     Overrides MeasureOverride and ArrangeOverride for custom layout logic.
//     OverflowItems is populated during measure pass so the dropdown button
//     always reflects the current overflow state without extra wiring.
//
//     ADR-TAB-OVF-01 (v2): Uses a _windowStart "scroll position" (index of the
//     leftmost visible non-sticky tab) and a _naturalWidths cache (last known
//     real width per tab, populated while the tab is Visible).
//
//     When the selected tab is outside the current window the panel shifts
//     _windowStart the minimum distance needed to reveal it at the near edge —
//     exactly like VS Code / Visual Studio tab bars. Previously-visible tabs on
//     the opposite side stay visible; only the far-edge tabs shift into overflow.
//
//     The SetVisibility guard (only writes when value actually changes) prevents
//     spurious InvalidateMeasure() calls, breaking the layout cycle that the
//     original two-phase approach (classify + EnsureSelectedTabVisible) caused.
//
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Shell.Controls;

/// <summary>
/// Custom panel that arranges tab items horizontally and collapses those
/// that overflow the available width. Exposes <see cref="HasOverflow"/>
/// and <see cref="OverflowItems"/> for the companion <see cref="TabOverflowButton"/>.
/// </summary>
public class TabOverflowPanel : Panel
{
    /// <summary>
    /// True when one or more children overflowed during the last measure pass.
    /// </summary>
    public static readonly DependencyProperty HasOverflowProperty =
        DependencyProperty.Register(nameof(HasOverflow), typeof(bool), typeof(TabOverflowPanel),
            new FrameworkPropertyMetadata(false));

    public bool HasOverflow
    {
        get => (bool)GetValue(HasOverflowProperty);
        private set => SetValue(HasOverflowProperty, value);
    }

    /// <summary>
    /// List of child elements that didn't fit and were collapsed.
    /// </summary>
    public List<UIElement> OverflowItems { get; } = [];

    // Cache: last known real width of each non-sticky tab (populated while Visible).
    // WPF returns DesiredSize=(0,0) for Collapsed elements, making it impossible to
    // use DesiredSize directly for layout decisions on overflow tabs.
    private readonly Dictionary<UIElement, double> _naturalWidths = new();

    // The index of the first visible non-sticky tab ("scroll position").
    // Shifts when the selected tab is outside the visible window.
    private int _windowStart = 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        OverflowItems.Clear();
        double usedWidth = 0.0;
        double maxHeight = 0.0;

        // 1. Measure only currently-Visible children.
        //    Collapsed children → DesiredSize=(0,0) — we use _naturalWidths instead.
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Visible)
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
        }

        // 2. Update natural-width cache for visible children with valid widths.
        foreach (UIElement child in InternalChildren)
            if (!IsSticky(child) && child.Visibility == Visibility.Visible && child.DesiredSize.Width > 0)
                _naturalWidths[child] = child.DesiredSize.Width;

        // 3. Sticky tabs are always visible.
        double stickyWidth = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (!IsSticky(child)) continue;
            SetVisibility(child, Visibility.Visible);
            stickyWidth += child.DesiredSize.Width;
            usedWidth   += child.DesiredSize.Width;
        }

        double available = availableSize.Width - stickyWidth;

        // 4. Build ordered list of non-sticky children.
        var ns = new List<UIElement>(InternalChildren.Count);
        foreach (UIElement child in InternalChildren)
            if (!IsSticky(child)) ns.Add(child);

        if (ns.Count == 0)
        {
            HasOverflow = false;
            return new Size(usedWidth, maxHeight);
        }

        // 5. Find the currently selected tab index.
        int selIdx = -1;
        for (int i = 0; i < ns.Count; i++)
            if (ns[i] is TabItem { IsSelected: true }) { selIdx = i; break; }

        // 6. Clamp _windowStart in case tabs were added/removed since last pass.
        _windowStart = Math.Max(0, Math.Min(_windowStart, ns.Count - 1));

        // 7. Adjust _windowStart to reveal the selected tab if it is outside the window.
        //    Only acts when the tab has a cached natural width (i.e. was Visible at least once).
        if (selIdx >= 0 && _naturalWidths.ContainsKey(ns[selIdx]))
        {
            int tempLast = ComputeLastFrom(ns, _windowStart, available);

            if (selIdx < _windowStart)
            {
                // Selected tab is to the LEFT of the window — shift window left.
                _windowStart = selIdx;
            }
            else if (selIdx > tempLast)
            {
                // Selected tab is to the RIGHT of the window — shift window right
                // so the selected tab appears as the last visible tab.
                _windowStart = ComputeFirstForLast(ns, selIdx, available);
            }
            // else: already in window — no change to _windowStart.
        }

        // 8. Compute actual [first..last] from current _windowStart.
        int first = _windowStart;
        int last  = ComputeLastFrom(ns, first, available);

        // 9. Extend the window left if there is unused space (e.g. after a tab is closed).
        //    This prevents a gap on the right when space opens up.
        {
            double used = 0;
            for (int i = first; i <= last; i++)
                used += _naturalWidths.TryGetValue(ns[i], out double w) ? w : 0;
            double leftover = available - used;
            while (first > 0
                   && _naturalWidths.TryGetValue(ns[first - 1], out double prevW)
                   && leftover >= prevW)
            {
                first--;
                leftover -= prevW;
            }
            _windowStart = first; // persist the adjusted start for stability
        }

        // 10. Apply visibility — only write when the value actually changes.
        //     This suppresses spurious InvalidateMeasure() calls and prevents layout cycles.
        for (int i = 0; i < ns.Count; i++)
        {
            bool show   = i >= first && i <= last;
            var  target = show ? Visibility.Visible : Visibility.Collapsed;
            SetVisibility(ns[i], target);

            if (show)
                usedWidth += _naturalWidths.TryGetValue(ns[i], out double w) ? w : ns[i].DesiredSize.Width;
            else
                OverflowItems.Add(ns[i]);
        }

        HasOverflow = OverflowItems.Count > 0;
        return new Size(usedWidth, maxHeight);
    }

    // -------------------------------------------------------------------------
    // Window helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the index of the last tab (inclusive) that fits when the window
    /// starts at <paramref name="start"/> and has <paramref name="available"/> px.
    /// Tabs with unknown cached width are included provisionally (they get cached
    /// on the next pass once they are Visible and measured).
    /// </summary>
    private int ComputeLastFrom(List<UIElement> ns, int start, double available)
    {
        double rem  = available;
        int    last = start - 1;
        for (int i = start; i < ns.Count; i++)
        {
            double w = _naturalWidths.TryGetValue(ns[i], out double cw) ? cw : 0;
            if (w > 0 && rem < w) break;
            rem -= w;
            last = i;
        }
        return last;
    }

    /// <summary>
    /// Returns the first index such that the contiguous range
    /// [first .. <paramref name="targetLast"/>] fits within <paramref name="available"/> px.
    /// Used to shift the window right so that <paramref name="targetLast"/> is the
    /// last (rightmost) visible tab.
    /// </summary>
    private int ComputeFirstForLast(List<UIElement> ns, int targetLast, double available)
    {
        double rem   = available;
        int    first = targetLast;
        for (int i = targetLast; i >= 0; i--)
        {
            double w = _naturalWidths.TryGetValue(ns[i], out double cw) ? cw : 0;
            if (w > 0 && rem < w) break;
            rem  -= w;
            first = i;
        }
        return Math.Min(first, targetLast);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Only assigns Visibility when it differs from the current value.</summary>
    private static void SetVisibility(UIElement element, Visibility target)
    {
        if (element.Visibility != target)
            element.Visibility = target;
    }

    /// <summary>Returns true when the child is a TabItem whose DockItem has IsSticky set.</summary>
    private static bool IsSticky(UIElement element) =>
        element is TabItem { Tag: DockItem { IsSticky: true } };

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }
        return finalSize;
    }
}
