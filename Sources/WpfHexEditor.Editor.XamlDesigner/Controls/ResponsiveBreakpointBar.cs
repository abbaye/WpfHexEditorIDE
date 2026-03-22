// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResponsiveBreakpointBar.cs
// Description:
//     Horizontal bar showing 5 responsive preset buttons:
//     Phone(375) / Tablet(768) / Desktop(1024) / HD(1280) / FHD(1920).
//     Clicking a preset fires BreakpointSelected with the corresponding
//     canvas width. The active preset is highlighted.
//
// Architecture Notes:
//     WPF Border built in code, no XAML.
//     Stateless — DesignCanvas drives the active preset via SetActive().
//     Theme-aware via XD_BreakpointActiveBackground token.
// ==========================================================

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Responsive breakpoint preset selector bar.
/// </summary>
public sealed class ResponsiveBreakpointBar : Border
{
    private static readonly (string Label, double Width, string Tooltip)[] Presets =
    [
        ("375",  375,  "Phone — 375px"),
        ("768",  768,  "Tablet — 768px"),
        ("1024", 1024, "Desktop — 1024px"),
        ("1280", 1280, "HD — 1280px"),
        ("1920", 1920, "Full HD — 1920px"),
    ];

    private readonly List<Button>  _buttons = new();
    private          double?       _activeWidth;

    /// <summary>Fired when the user selects a breakpoint. Arg is the target canvas width.</summary>
    public event EventHandler<double>? BreakpointSelected;

    public ResponsiveBreakpointBar()
    {
        Background      = Application.Current?.TryFindResource("XD_CanvasBackground") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        BorderThickness = new Thickness(0, 0, 0, 1);
        BorderBrush     = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
        Padding         = new Thickness(4, 2, 4, 2);
        Height          = 30;

        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        var btnStyle = Application.Current?.TryFindResource("DockDarkToolBarButtonStyle") as Style;

        foreach (var (label, width, tooltip) in Presets)
        {
            double capturedWidth = width;
            var btn = new Button
            {
                Content         = label,
                Padding         = new Thickness(8, 2, 8, 2),
                FontSize        = 9.5,
                ToolTip         = tooltip,
                Margin          = new Thickness(1, 0, 1, 0),
            };

            if (btnStyle is not null)
                btn.Style = btnStyle;
            else
            {
                btn.Background      = Brushes.Transparent;
                btn.Foreground      = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB));
                btn.BorderThickness = new Thickness(0);
                btn.Cursor          = Cursors.Hand;
            }

            btn.Click += (_, _) =>
            {
                SetActive(capturedWidth);
                BreakpointSelected?.Invoke(this, capturedWidth);
            };

            _buttons.Add(btn);
            panel.Children.Add(btn);
        }

        Child = panel;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Highlights the button matching <paramref name="width"/>, or clears all highlights
    /// when null.
    /// </summary>
    public void SetActive(double? width)
    {
        _activeWidth = width;

        var activeBg = Application.Current?.TryFindResource("XD_BreakpointActiveBackground") as Brush
                       ?? new SolidColorBrush(Color.FromArgb(80, 0, 122, 204));

        for (int i = 0; i < _buttons.Count; i++)
        {
            bool isActive = width.HasValue && Math.Abs(Presets[i].Width - width.Value) < 0.5;
            // Override only Background; Style owns Foreground and hover behaviour.
            _buttons[i].Background = isActive ? activeBg : Brushes.Transparent;
        }
    }
}
