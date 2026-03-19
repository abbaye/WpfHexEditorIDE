// ==========================================================
// Project: WpfHexEditor.Shell
// File: TabColorizerAttached.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Provides the AccentBrush attached dependency property used by
//     DockTabItemStyle to render the per-tab color stripe in the document tab bar.
//     Set by DocumentTabHost via TabColorService based on the active ColorMode.
//
// Architecture Notes:
//     Pure attached property host (static class). No logic — only the DP registration
//     and CLR accessors. Consumed by XAML styles as a DynamicResource alternative
//     for per-element color variation.
//
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Shell.Attached;

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
