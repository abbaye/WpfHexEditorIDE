// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/BreakpointHoverPopup.cs
// Description:
//     VS-style hover popup shown when the mouse dwells on a breakpoint-highlighted
//     line. Displays breakpoint type badge, condition (syntax-colored), code preview,
//     and action links (Edit Condition / Disable / Delete).
//     Uses ET_* theme tokens — no hardcoded colors.
// Architecture:
//     Grace-timer pattern (200ms) identical to EndBlockHintPopup.
//     Communicates via IBreakpointSource — no SDK dependency.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Hover popup for breakpoint lines — shows type, condition, code preview, and action links.
/// </summary>
internal sealed class BreakpointHoverPopup : Popup, IDisposable
{
    // ── Grace timer ──────────────────────────────────────────────────────────

    private readonly DispatcherTimer _graceTimer;
    private bool _mouseInsidePopup;

    // ── State ────────────────────────────────────────────────────────────────

    private IBreakpointSource? _source;
    private string _filePath = string.Empty;
    private int _line1;
    private Rect _anchorRect;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Fired when the user clicks "Edit Condition" — host should open the BreakpointInfoPopup.</summary>
    internal event Action<string, int>? EditConditionRequested;

    /// <summary>Fired when the user clicks "Disable" / "Enable".</summary>
    internal event Action<string, int, bool>? ToggleEnabledRequested;

    /// <summary>Fired when the user clicks "Delete".</summary>
    internal event Action<string, int>? DeleteRequested;

    // ── Constructor ──────────────────────────────────────────────────────────

    public BreakpointHoverPopup()
    {
        StaysOpen = true;
        AllowsTransparency = true;
        Placement = PlacementMode.Custom;
        CustomPopupPlacementCallback = PlacePopup;
        PopupAnimation = PopupAnimation.Fade;

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
        _source = source;
        _filePath = filePath;
        _line1 = line1;

        _anchorRect = anchorRect;
        PlacementTarget = host;
        Child = BuildContent(info, line1, filePath, documentLines, highlighter);
        IsOpen = true;
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
        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.35 },
            MinWidth = 280,
            MaxWidth = 500,
        };
        border.SetResourceReference(Border.BackgroundProperty, "ET_PopupBackground");
        border.SetResourceReference(Border.BorderBrushProperty, "ET_PopupBorderBrush");

        border.MouseEnter += (_, _) => { _mouseInsidePopup = true; _graceTimer.Stop(); };
        border.MouseLeave += (_, _) => { _mouseInsidePopup = false; _graceTimer.Start(); };

        var stack = new StackPanel { Margin = new Thickness(8, 6, 8, 6) };

        // ── Header: icon + type badge + location ────────────────────────────
        var headerPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };

        // BP circle indicator
        string typeLabel;
        Brush circleBrush;
        if (!info.IsEnabled)
        {
            typeLabel = "Disabled";
            circleBrush = Brushes.DimGray;
        }
        else if (!string.IsNullOrEmpty(info.Condition))
        {
            typeLabel = "Conditional";
            circleBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x1E));
        }
        else
        {
            typeLabel = "Breakpoint";
            circleBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x14, 0x00));
        }

        var circle = new Border
        {
            Width = 10, Height = 10,
            CornerRadius = new CornerRadius(5),
            Background = circleBrush,
            Margin = new Thickness(0, 2, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        headerPanel.Children.Add(circle);

        var typeBadge = new TextBlock
        {
            Text = typeLabel,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        typeBadge.SetResourceReference(TextBlock.ForegroundProperty, "ET_HeaderForeground");
        headerPanel.Children.Add(typeBadge);

        var locationText = new TextBlock
        {
            Text = $"{System.IO.Path.GetFileName(filePath)}:{line1}",
            FontSize = 11,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
        };
        locationText.SetResourceReference(TextBlock.ForegroundProperty, "ET_MetaForeground");
        headerPanel.Children.Add(locationText);

        stack.Children.Add(headerPanel);

        // ── Separator ───────────────────────────────────────────────────────
        var sep = new Border { Height = 1, Margin = new Thickness(-8, 2, -8, 4) };
        sep.SetResourceReference(Border.BackgroundProperty, "ET_PopupBorderBrush");
        stack.Children.Add(sep);

        // ── Condition row (if conditional) ──────────────────────────────────
        if (!string.IsNullOrEmpty(info.Condition))
        {
            var condLabel = new TextBlock
            {
                Text = "Condition:",
                FontSize = 11,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 2),
            };
            condLabel.SetResourceReference(TextBlock.ForegroundProperty, "ET_MetaForeground");
            stack.Children.Add(condLabel);

            var condValue = new TextBlock
            {
                Text = info.Condition,
                FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap,
            };
            condValue.SetResourceReference(TextBlock.ForegroundProperty, "ET_HeaderForeground");
            stack.Children.Add(condValue);
        }

        // ── Code preview (1-3 lines around breakpoint) ──────────────────────
        if (lines is not null && line1 >= 1 && line1 <= lines.Count)
        {
            var codePanel = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
            var codeBorder = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 4, 6, 4),
            };
            codeBorder.SetResourceReference(Border.BackgroundProperty, "ET_HeaderBackground");

            int startLine = Math.Max(0, line1 - 2);      // 0-based: 1 line before the BP
            int endLine   = Math.Min(lines.Count - 1, line1 - 1);  // 0-based: the BP line itself

            for (int i = startLine; i <= endLine; i++)
            {
                var lineText = lines[i].Text ?? string.Empty;
                var tb = new TextBlock
                {
                    FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
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
                        if (token.IsBold) run.FontWeight = FontWeights.Bold;
                        if (token.IsItalic) run.FontStyle = FontStyles.Italic;
                        tb.Inlines.Add(run);
                    }
                }
                else
                {
                    tb.Text = lineText;
                    tb.SetResourceReference(TextBlock.ForegroundProperty, "ET_HeaderForeground");
                }

                // Highlight the breakpoint line with a subtle marker
                if (i == line1 - 1)
                    tb.FontWeight = FontWeights.SemiBold;

                codePanel.Children.Add(tb);
            }

            codeBorder.Child = codePanel;
            stack.Children.Add(codeBorder);
        }

        // ── Action links ────────────────────────────────────────────────────
        var actionsPanel = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };

        actionsPanel.Children.Add(MakeActionLink("Edit Condition", () =>
        {
            IsOpen = false;
            EditConditionRequested?.Invoke(_filePath, _line1);
        }));

        actionsPanel.Children.Add(MakeSep());

        string toggleText = info.IsEnabled ? "Disable" : "Enable";
        actionsPanel.Children.Add(MakeActionLink(toggleText, () =>
        {
            _source?.SetEnabled(_filePath, _line1, !info.IsEnabled);
            IsOpen = false;
        }));

        actionsPanel.Children.Add(MakeSep());

        actionsPanel.Children.Add(MakeActionLink("Delete", () =>
        {
            _source?.Delete(_filePath, _line1);
            IsOpen = false;
        }));

        stack.Children.Add(actionsPanel);

        border.Child = stack;
        return border;
    }

    private static TextBlock MakeActionLink(string text, Action onClick)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 0),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "ET_AccentBrush");
        tb.MouseLeftButtonDown += (_, e) => { onClick(); e.Handled = true; };
        tb.MouseEnter += (_, _) => tb.TextDecorations = TextDecorations.Underline;
        tb.MouseLeave += (_, _) => tb.TextDecorations = null;
        return tb;
    }

    private static TextBlock MakeSep() => new()
    {
        Text = "  |  ",
        FontSize = 11,
        Opacity = 0.4,
    };

    // ── Placement callback ───────────────────────────────────────────────────

    private CustomPopupPlacement[] PlacePopup(Size popup, Size target, Point offset)
    {
        // Position based on mouse/anchor: top of popup = anchorY - 2 line heights.
        double lineH = _anchorRect.Height;
        double x = _anchorRect.X + 20;
        double y = _anchorRect.Y - 2 * lineH;

        // Clamp: if no room above, fall below the line.
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
