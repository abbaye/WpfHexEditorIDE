//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf.Controls;

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

    protected override Size MeasureOverride(Size availableSize)
    {
        OverflowItems.Clear();
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
        return new Size(usedWidth, maxHeight);
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
