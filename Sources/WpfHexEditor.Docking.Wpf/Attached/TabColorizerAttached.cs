//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Docking.Wpf.Attached;

/// <summary>
/// Provides the <c>AccentBrush</c> attached dependency property used by
/// <c>DockTabItemStyle</c> to render the per-tab color stripe.
/// Set by <see cref="DocumentTabHost"/> via <see cref="Services.TabColorService"/>.
/// </summary>
public static class TabColorizerAttached
{
    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.RegisterAttached(
            "AccentBrush",
            typeof(Brush),
            typeof(TabColorizerAttached),
            new PropertyMetadata(Brushes.Transparent));

    public static Brush GetAccentBrush(DependencyObject obj)
        => (Brush)obj.GetValue(AccentBrushProperty);

    public static void SetAccentBrush(DependencyObject obj, Brush value)
        => obj.SetValue(AccentBrushProperty, value);
}
