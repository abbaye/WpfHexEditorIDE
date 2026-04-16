// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Layers/LspInlayHintsLayer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     DrawingVisual overlay that renders parameter-name inlay hints
//     (e.g. "paramName:" before each argument) from ILspClient.InlayHintsAsync.
//     Positioned above the text but below the caret layer.
//
// Architecture Notes:
//     Pattern: Decorator / Overlay (drawn above text in CodeEditor's visual stack).
//     Only queries LSP when the capability flag HasInlayHintsProvider is true (LSP 3.17+).
//     Debounced 500ms on scroll/edit to avoid hammering the language server.
//     Theme tokens: CE_InlayHintForeground, CE_InlayHintBackground.
// ==========================================================

using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Layers;

/// <summary>
/// Renders LSP inlay hints (parameter name labels) as a <see cref="DrawingVisual"/>
/// overlay on top of the code text layer.
/// </summary>
public sealed class LspInlayHintsLayer : FrameworkElement
{
    // ── State ─────────────────────────────────────────────────────────────────
    private ILspClient?              _lspClient;
    private string?                  _filePath;
    private int                      _firstVisibleLine;
    private int                      _lastVisibleLine;
    private double                   _charWidth;
    private double                   _lineHeight;
    private double                   _horizontalScrollOffset;
    private IReadOnlyList<LspInlayHint> _hints = Array.Empty<LspInlayHint>();

    private readonly DrawingVisual   _visual = new();
    private readonly DispatcherTimer _debounce;
    private CancellationTokenSource? _cts;

    // ── Constructor ───────────────────────────────────────────────────────────

    public LspInlayHintsLayer()
    {
        IsHitTestVisible = false;   // hints are display-only
        ClipToBounds     = true;
        AddVisualChild(_visual);
        AddLogicalChild(_visual);

        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounce.Tick += OnDebounce;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Injects or clears the LSP client.</summary>
    public void SetLspClient(ILspClient? client)
    {
        _lspClient = client;
        if (client is null) ClearHints();
    }

    /// <summary>Sets the current file path and requests a refresh.</summary>
    public void SetContext(string? filePath, int firstLine, int lastLine,
                           double charWidth, double lineHeight,
                           double horizontalScrollOffset = 0.0)
    {
        bool viewportMoved = _firstVisibleLine != firstLine || _lastVisibleLine != lastLine;
        _filePath               = filePath;
        _firstVisibleLine       = firstLine;
        _lastVisibleLine        = lastLine;
        _charWidth              = charWidth;
        _lineHeight             = lineHeight;
        _horizontalScrollOffset = horizontalScrollOffset;

        // Clear stale items immediately when the viewport scrolls so ArrangeOverride
        // doesn't render old items at wrong positions during the debounce.
        if (viewportMoved && _hints.Count > 0)
            ClearHints();

        RequestRefresh();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Don't re-render here — RenderHints is called after debounce resolves fresh data.
        // Rendering stale _hints in ArrangeOverride causes wrong positions after scroll.
        return finalSize;
    }

    private void RequestRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private async void OnDebounce(object? sender, EventArgs e)
    {
        _debounce.Stop();

        if (_lspClient?.IsFullyLoaded != true || _filePath is null)
        {
            ClearHints();
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            var hints = await _lspClient.InlayHintsAsync(
                _filePath, _firstVisibleLine, _lastVisibleLine, _cts.Token)
                .ConfigureAwait(true);

            _hints = hints;
            RenderHints();
        }
        catch (OperationCanceledException) { }
        catch { ClearHints(); }
    }

    private void ClearHints()
    {
        _hints = Array.Empty<LspInlayHint>();
        using var dc = _visual.RenderOpen();
        // nothing to draw
    }

    private void RenderHints()
    {
        using var dc = _visual.RenderOpen();

        if (_hints.Count == 0 || _charWidth <= 0 || _lineHeight <= 0) return;

        // Resolve theme brushes.
        var fg = TryFindResource("CE_InlayHintForeground") as Brush
                 ?? new SolidColorBrush(Color.FromArgb(160, 130, 130, 130));
        var bg = TryFindResource("CE_InlayHintBackground") as Brush
                 ?? new SolidColorBrush(Color.FromArgb(30, 130, 130, 130));

        var typeface = new Typeface(
            new FontFamily("Consolas, Courier New"),
            FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);

        foreach (var hint in _hints)
        {
            if (hint.Line < _firstVisibleLine || hint.Line > _lastVisibleLine) continue;

            var text = hint.Kind == "parameter"
                ? $"{hint.Label}:"
                : hint.Label;

            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                8.0,
                fg,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Position at (column × charWidth - hScroll, (line - firstLine) × lineHeight)
            var x = hint.Column * _charWidth - _horizontalScrollOffset;
            var y = (hint.Line - _firstVisibleLine) * _lineHeight
                    + (_lineHeight - ft.Height) / 2;

            // Pill background.
            var rect = new Rect(x - 2, y, ft.Width + 4, ft.Height);
            dc.DrawRectangle(bg, null, rect);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
