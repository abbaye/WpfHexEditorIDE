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

    // Set by EnsureSelectedTabVisible — tab to place at front (after stickies) in ArrangeOverride.
    private UIElement? _forceFrontTab;

    protected override Size MeasureOverride(Size availableSize)
    {
        OverflowItems.Clear();
        _forceFrontTab = null;
        var usedWidth = 0.0;
        var maxHeight = 0.0;

        // Measure all children first so DesiredSize is available.
        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));

        // Reserve width for sticky tabs — they are always visible regardless of overflow.
        double stickyWidth = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (IsSticky(child))
                stickyWidth += child.DesiredSize.Width;
        }

        // Remaining space is distributed to non-sticky tabs in order.
        double remainingWidth = availableSize.Width - stickyWidth;

        foreach (UIElement child in InternalChildren)
        {
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);

            if (IsSticky(child))
            {
                // Sticky tabs are always visible — they consume their reserved space.
                child.Visibility = Visibility.Visible;
                usedWidth += child.DesiredSize.Width;
            }
            else if (remainingWidth >= child.DesiredSize.Width)
            {
                remainingWidth -= child.DesiredSize.Width;
                usedWidth      += child.DesiredSize.Width;
                child.Visibility = Visibility.Visible;
            }
            else
            {
                child.Visibility = Visibility.Collapsed;
                OverflowItems.Add(child);
            }
        }

        HasOverflow = OverflowItems.Count > 0;

        // C2: Ensure the selected tab is always visible — mirrors VS behavior.
        // If the selected tab ended up in overflow, evict the rightmost non-sticky visible tab
        // to make room for it.
        EnsureSelectedTabVisible();

        return new Size(usedWidth, maxHeight);
    }

    /// <summary>
    /// If the currently selected TabItem is in the overflow list, swap it with the
    /// rightmost visible non-sticky tab so it is always shown in the strip.
    /// </summary>
    private void EnsureSelectedTabVisible()
    {
        // Find the selected tab that overflowed.
        TabItem? selectedOverflow = null;
        foreach (var item in OverflowItems)
        {
            if (item is TabItem { IsSelected: true } ti)
            {
                selectedOverflow = ti;
                break;
            }
        }

        if (selectedOverflow is null) return;

        // Find the rightmost visible non-sticky tab to evict.
        UIElement? candidate = null;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Visible && !IsSticky(child))
                candidate = child; // keep updating — last one wins (rightmost)
        }

        if (candidate is null) return; // no evictable tab, can't help

        // Swap: hide the candidate, show the selected overflow tab.
        candidate.Visibility = Visibility.Collapsed;
        OverflowItems.Remove(selectedOverflow);
        OverflowItems.Insert(0, candidate); // push evicted tab to front of overflow list
        selectedOverflow.Visibility = Visibility.Visible;
        HasOverflow = OverflowItems.Count > 0;

        // Mark the selected tab to be placed at the leftmost non-sticky position in ArrangeOverride,
        // mimicking VS scroll-into-view-from-left behavior.
        _forceFrontTab = selectedOverflow;
    }

    /// <summary>Returns true when the child is a TabItem whose DockItem has IsSticky set.</summary>
    private static bool IsSticky(UIElement element) =>
        element is TabItem { Tag: DockItem { IsSticky: true } };

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;

        if (_forceFrontTab is null)
        {
            // Normal case: single left-to-right pass in children order.
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed) continue;
                child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
                x += child.DesiredSize.Width;
            }
        }
        else
        {
            // Force-front case: sticky tabs → forced tab → remaining non-sticky tabs.
            // Sticky tabs keep their natural order and always appear at the left edge.
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed || !IsSticky(child)) continue;
                child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
                x += child.DesiredSize.Width;
            }

            // Place the forced tab immediately after stickies (leftmost non-sticky position).
            if (_forceFrontTab.Visibility == Visibility.Visible)
            {
                _forceFrontTab.Arrange(new Rect(x, 0, _forceFrontTab.DesiredSize.Width, finalSize.Height));
                x += _forceFrontTab.DesiredSize.Width;
            }

            // Remaining visible non-sticky tabs follow in their natural order.
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed || IsSticky(child) || child == _forceFrontTab) continue;
                child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
                x += child.DesiredSize.Width;
            }
        }

        return finalSize;
    }
}
