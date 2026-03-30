// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/BreakpointHoverPopup.cs
// Description:
//     VS-style hover popup shown when the mouse dwells on a breakpoint-highlighted line.
//     Shows type badge + location, syntax-colored code preview, and a context-menu
//     action list with Segoe MDL2 icons (Edit Condition / Enable-Disable / Delete).
// Architecture:
//     Grace-timer pattern (300ms) identical to EndBlockHintPopup.
//     Communicates via IBreakpointSource — no SDK dependency.
//     All colours via SetResourceReference (ET_* tokens); syntax tokens via ISyntaxHighlighter.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Rendering;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

/// <summary>
/// Hover popup for breakpoint lines — shows type badge, syntax-colored code preview,
/// and an icon-equipped context-menu action list.
/// </summary>
internal sealed class BreakpointHoverPopup : Popup, IDisposable
{
    // ── Grace timer ──────────────────────────────────────────────────────────

    private readonly DispatcherTimer _graceTimer;
    private bool _mouseInsidePopup;

    // ── State ────────────────────────────────────────────────────────────────

    private IBreakpointSource? _source;
    private string _filePath = string.Empty;
    private int    _line1;
    private Rect   _anchorRect;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Fired when "Edit Condition" is clicked — host opens BreakpointInfoPopup.</summary>
    internal event Action<string, int>? EditConditionRequested;

    /// <summary>Fired when "Disable" / "Enable" is clicked.</summary>
    internal event Action<string, int, bool>? ToggleEnabledRequested;

    /// <summary>Fired when "Delete" is clicked.</summary>
    internal event Action<string, int>? DeleteRequested;

    // ── Constructor ──────────────────────────────────────────────────────────

    public BreakpointHoverPopup()
    {
        StaysOpen                  = true;
        AllowsTransparency         = true;
        Placement                  = PlacementMode.Custom;
        CustomPopupPlacementCallback = PlacePopup;
        PopupAnimation             = PopupAnimation.Fade;

        _graceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _graceTimer.Tick += (_, _) =>
        {
            _graceTimer.Stop();
            if (!_mouseInsidePopup) IsOpen = false;
        };

        Application.Current.Deactivated += (_, _) =>
            Dispatcher.BeginInvoke(() => IsOpen = false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void Show(
        FrameworkElement host,
        IBreakpointSource source,
        string filePath, int line1,
        BreakpointInfo info,
        IReadOnlyList<Models.CodeLine>? documentLines,
        ISyntaxHighlighter? highlighter,
        Rect anchorRect)
    {
        _source     = source;
        _filePath   = filePath;
        _line1      = line1;
        _anchorRect = anchorRect;

        PlacementTarget = host;
        Child           = BuildContent(info, line1, filePath, documentLines, highlighter);
        IsOpen          = true;
    }

    public void OnEditorMouseLeft()
    {
        if (!_mouseInsidePopup)
        {
            _graceTimer.Stop();
            _graceTimer.Start();
        }
    }

    // ── Content builder ──────────────────────────────────────────────────────

    private FrameworkElement BuildContent(
        BreakpointInfo info, int line1, string filePath,
        IReadOnlyList<Models.CodeLine>? lines,
        ISyntaxHighlighter? highlighter)
    {
        var outerBorder = new Border
        {
            CornerRadius    = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            MinWidth        = 240,
            MaxWidth        = 500,
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.35 },
        };
        outerBorder.SetResourceReference(Border.BackgroundProperty,  "ET_PopupBackground");
        outerBorder.SetResourceReference(Border.BorderBrushProperty, "ET_PopupBorderBrush");

        outerBorder.MouseEnter += (_, _) => { _mouseInsidePopup = true;  _graceTimer.Stop(); };
        outerBorder.MouseLeave += (_, _) => { _mouseInsidePopup = false; _graceTimer.Start(); };

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        // ── Header: circle + type badge + location + close ──────────────────
        string typeLabel;
        Brush  circleBrush;
        if (!info.IsEnabled)
        {
            typeLabel   = "Disabled";
            circleBrush = Brushes.DimGray;
        }
        else if (!string.IsNullOrEmpty(info.Condition))
        {
            typeLabel   = "Conditional";
            circleBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x1E));
        }
        else
        {
            typeLabel   = "Breakpoint";
            circleBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x14, 0x00));
        }

        var circle = new Border
        {
            Width = 10, Height = 10,
            CornerRadius      = new CornerRadius(5),
            Background        = circleBrush,
            Margin            = new Thickness(0, 2, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var typeBadge = new TextBlock
        {
            Text              = typeLabel,
            FontWeight        = FontWeights.SemiBold,
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
        };
        typeBadge.SetResourceReference(TextBlock.ForegroundProperty, "ET_HeaderForeground");

        var locationText = new TextBlock
        {
            Text              = $"{System.IO.Path.GetFileName(filePath)}:{line1}",
            FontSize          = 11,
            Opacity           = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
        };
        locationText.SetResourceReference(TextBlock.ForegroundProperty, "ET_MetaForeground");

        var closeGlyph = new TextBlock
        {
            Text              = "\uE711",
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 10,
            Cursor            = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0),
        };
        closeGlyph.SetResourceReference(TextBlock.ForegroundProperty, "ET_MetaForeground");
        closeGlyph.MouseLeftButtonUp += (_, _) => IsOpen = false;

        var headerRow = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            Margin            = new Thickness(10, 7, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
        };
        headerRow.Children.Add(circle);
        headerRow.Children.Add(typeBadge);
        headerRow.Children.Add(locationText);
        headerRow.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 16 });
        headerRow.Children.Add(closeGlyph);

        var headerBorder = new Border { Padding = new Thickness(0) };
        headerBorder.SetResourceReference(Border.BackgroundProperty, "ET_HeaderBackground");
        headerBorder.Child = headerRow;

        stack.Children.Add(headerBorder);

        // ── Separator ───────────────────────────────────────────────────────
        var sep1 = new Border { Height = 1 };
        sep1.SetResourceReference(Border.BackgroundProperty, "ET_PopupBorderBrush");
        stack.Children.Add(sep1);

        // ── Code preview (syntax colored) ────────────────────────────────────
        if (lines is not null && line1 >= 1 && line1 <= lines.Count)
        {
            var codeStack = new StackPanel();
            var codeBorder = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding      = new Thickness(8, 5, 8, 5),
                Margin       = new Thickness(8, 6, 8, 2),
            };
            codeBorder.SetResourceReference(Border.BackgroundProperty, "ET_HeaderBackground");

            int startLine = Math.Max(0, line1 - 2);
            int endLine   = Math.Min(lines.Count - 1, line1 - 1);

            for (int i = startLine; i <= endLine; i++)
            {
                var lineText = lines[i].Text ?? string.Empty;
                var tb = new TextBlock
                {
                    FontFamily   = new FontFamily("Cascadia Code, Consolas, Courier New"),
                    FontSize     = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight   = (i == line1 - 1) ? FontWeights.SemiBold : FontWeights.Normal,
                };

                if (highlighter is not null)
                {
                    var tokens = highlighter.Highlight(lineText, i);
                    foreach (var token in tokens)
                    {
                        var run = new Run(token.Text);
                        if (token.Foreground is not null)
                            run.Foreground = token.Foreground;
                        else
                            run.SetResourceReference(TextElement.ForegroundProperty, "ET_HeaderForeground");
                        if (token.IsBold)   run.FontWeight = FontWeights.Bold;
                        if (token.IsItalic) run.FontStyle  = FontStyles.Italic;
                        tb.Inlines.Add(run);
                    }
                }
                else
                {
                    tb.Text = lineText;
                    tb.SetResourceReference(TextBlock.ForegroundProperty, "ET_HeaderForeground");
                }

                codeStack.Children.Add(tb);
            }

            codeBorder.Child = codeStack;
            stack.Children.Add(codeBorder);

            var sep2 = new Border { Height = 1 };
            sep2.SetResourceReference(Border.BackgroundProperty, "ET_PopupBorderBrush");
            stack.Children.Add(sep2);
        }

        // ── Context-menu action list ─────────────────────────────────────────
        var menuPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2, 0, 4) };

        menuPanel.Children.Add(MakeMenuItem("\uE8AC", "Edit Condition", (_, _) =>
        {
            IsOpen = false;
            EditConditionRequested?.Invoke(_filePath, _line1);
        }));

        menuPanel.Children.Add(MakeMenuSeparator());

        string toggleLabel = info.IsEnabled ? "Disable" : "Enable";
        string toggleGlyph = info.IsEnabled ? "\uE73E" : "\uE73F";
        menuPanel.Children.Add(MakeMenuItem(toggleGlyph, toggleLabel, (_, _) =>
        {
            _source?.SetEnabled(_filePath, _line1, !info.IsEnabled);
            ToggleEnabledRequested?.Invoke(_filePath, _line1, !info.IsEnabled);
            IsOpen = false;
        }));

        menuPanel.Children.Add(MakeMenuSeparator());

        menuPanel.Children.Add(MakeMenuItem("\uE74D", "Delete Breakpoint", (_, _) =>
        {
            _source?.Delete(_filePath, _line1);
            DeleteRequested?.Invoke(_filePath, _line1);
            IsOpen = false;
        }, isDestructive: true));

        var menuBorder = new Border();
        menuBorder.SetResourceReference(Border.BackgroundProperty, "ET_PopupBackground");
        menuBorder.Child = menuPanel;
        stack.Children.Add(menuBorder);

        outerBorder.Child = stack;
        return outerBorder;
    }

    // ── Menu item helpers ─────────────────────────────────────────────────────

    private static FrameworkElement MakeMenuItem(string glyph, string label,
        RoutedEventHandler handler, bool isDestructive = false)
    {
        var fgKey = isDestructive ? "ET_AccentBrush" : "ET_HeaderForeground";

        var iconTb = new TextBlock
        {
            Text              = glyph,
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 12,
            Width             = 20,
            VerticalAlignment = VerticalAlignment.Center,
        };
        iconTb.SetResourceReference(TextBlock.ForegroundProperty, fgKey);

        var labelTb = new TextBlock
        {
            Text              = label,
            FontSize          = 11,
            Margin            = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        labelTb.SetResourceReference(TextBlock.ForegroundProperty, fgKey);

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(iconTb);
        row.Children.Add(labelTb);

        var bd = new Border
        {
            Padding    = new Thickness(10, 5, 16, 5),
            Cursor     = Cursors.Hand,
            Background = Brushes.Transparent,
            Child      = row,
        };
        bd.MouseEnter        += (_, _) => bd.SetResourceReference(Border.BackgroundProperty, "ET_HeaderBackground");
        bd.MouseLeave        += (_, _) => bd.Background = Brushes.Transparent;
        bd.MouseLeftButtonUp += (s, e) => handler(s, e);
        return bd;
    }

    private static Border MakeMenuSeparator()
    {
        var sep = new Border { Height = 1, Margin = new Thickness(10, 2, 10, 2) };
        sep.SetResourceReference(Border.BackgroundProperty, "ET_PopupBorderBrush");
        return sep;
    }

    // ── Placement callback ───────────────────────────────────────────────────

    private CustomPopupPlacement[] PlacePopup(Size popup, Size target, Point offset)
    {
        double lineH = _anchorRect.Height;
        double x     = _anchorRect.X + 20;
        double y     = _anchorRect.Y - 2 * lineH;

        if (y < 0)
            y = _anchorRect.Y + lineH;

        return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Horizontal)];
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _graceTimer.Stop();
        IsOpen = false;
    }
}
