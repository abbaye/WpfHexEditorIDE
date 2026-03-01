//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;

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
        var overflow = false;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            var desiredWidth = child.DesiredSize.Width;

            if (!overflow && usedWidth + desiredWidth <= availableSize.Width)
            {
                usedWidth += desiredWidth;
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
                child.Visibility = Visibility.Visible;
            }
            else
            {
                overflow = true;
                child.Visibility = Visibility.Collapsed;
                OverflowItems.Add(child);
            }
        }

        HasOverflow = overflow;
        return new Size(usedWidth, maxHeight);
    }

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
