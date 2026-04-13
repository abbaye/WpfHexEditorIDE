// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Layers/LspSemanticTokensLayer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     DrawingVisual overlay that applies LSP semantic token colorization on top
//     of the syntactic highlight layer. Draws semi-transparent color rectangles
//     for each semantic token in the visible range.
//
// Architecture Notes:
//     Pattern: Decorator / Overlay (drawn between text and caret layers).
//     Calls ILspClient.SemanticTokensAsync on scroll/edit (1 000ms debounce).
//     LSP token array is delta-encoded (line+char deltas) — decoded here.
//     Each token type maps to a theme resource (SE_TypeColor, SE_MethodColor, …).
//     Only visible-range tokens are rendered; off-screen tokens are skipped.
//     Theme tokens: SE_TypeColor, SE_MethodColor, SE_VariableColor,
//                   SE_ParameterColor, SE_NamespaceColor, SE_KeywordColor,
//                   SE_StringColor, SE_NumberColor.
// ==========================================================

using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Layers;

/// <summary>
/// Renders LSP semantic token colorization as semi-transparent highlight rectangles
/// drawn above the text layer.
/// </summary>
public sealed class LspSemanticTokensLayer : FrameworkElement
{
    // ── State ─────────────────────────────────────────────────────────────────
    private ILspClient?   _lspClient;
    private string?       _filePath;
    private int           _firstVisibleLine;
    private int           _lastVisibleLine;
    private double        _charWidth;
    private double        _lineHeight;

    private IReadOnlyList<LspSemanticToken> _tokens = Array.Empty<LspSemanticToken>();

    private readonly DrawingVisual   _visual   = new();
    private readonly DispatcherTimer _debounce;
    private CancellationTokenSource? _cts;

    // ── Token type → theme resource key ───────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, string> s_typeToResource =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"]        = "SE_TypeColor",
            ["class"]       = "SE_TypeColor",
            ["struct"]      = "SE_TypeColor",
            ["interface"]   = "SE_TypeColor",
            ["enum"]        = "SE_TypeColor",
            ["method"]      = "SE_MethodColor",
            ["function"]    = "SE_MethodColor",
            ["member"]      = "SE_MethodColor",
            ["variable"]    = "SE_VariableColor",
            ["local"]       = "SE_VariableColor",
            ["field"]       = "SE_VariableColor",
            ["parameter"]   = "SE_ParameterColor",
            ["namespace"]   = "SE_NamespaceColor",
            ["keyword"]     = "SE_KeywordColor",
            ["modifier"]    = "SE_KeywordColor",
            ["string"]      = "SE_StringColor",
            ["number"]      = "SE_NumberColor",
        };

    // ── Constructor ───────────────────────────────────────────────────────────

    public LspSemanticTokensLayer()
    {
        IsHitTestVisible = false;
        AddVisualChild(_visual);
        AddLogicalChild(_visual);

        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        _debounce.Tick += OnDebounce;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Injects or clears the LSP client.</summary>
    public void SetLspClient(ILspClient? client)
    {
        _lspClient = client;
        if (client is null) ClearTokens();
    }

    /// <summary>Updates the visible range and metrics, then requests a refresh.</summary>
    public void SetContext(string? filePath,
                           int firstLine, int lastLine,
                           double charWidth, double lineHeight)
    {
        _filePath         = filePath;
        _firstVisibleLine = firstLine;
        _lastVisibleLine  = lastLine;
        _charWidth        = charWidth;
        _lineHeight       = lineHeight;
        RequestRefresh();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Do NOT render here — RenderTokens is called after the debounce resolves fresh data.
        // Calling RenderTokens from ArrangeOverride causes TryFindResource traversals during
        // layout, which can trigger WPF composition updates that invalidate the QuickInfoPopup
        // placement and suppress hover tooltips.
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

        if (_lspClient?.IsInitialized != true || _filePath is null)
        {
            ClearTokens();
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            var result = await _lspClient.SemanticTokensAsync(_filePath, _cts.Token)
                .ConfigureAwait(true);

            _tokens = result?.Tokens ?? Array.Empty<LspSemanticToken>();
            RenderTokens();
        }
        catch (OperationCanceledException) { }
        catch { ClearTokens(); }
    }

    private void ClearTokens()
    {
        _tokens = Array.Empty<LspSemanticToken>();
        using var dc = _visual.RenderOpen();
        // clears previous content
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    private void RenderTokens()
    {
        using var dc = _visual.RenderOpen();

        if (_tokens.Count == 0 || _charWidth <= 0 || _lineHeight <= 0) return;

        // Cache resolved brushes per resource key to avoid repeated TryFindResource calls.
        var brushCache = new Dictionary<string, Brush?>(8);

        foreach (var token in _tokens)
        {
            if (token.Line < _firstVisibleLine || token.Line > _lastVisibleLine) continue;
            if (!s_typeToResource.TryGetValue(token.TokenType ?? string.Empty, out var resKey)) continue;

            if (!brushCache.TryGetValue(resKey, out var brush))
            {
                brush = TryFindResource(resKey) as Brush;
                brushCache[resKey] = brush;
            }

            if (brush is null) continue;

            var x = token.Column * _charWidth;
            var y = (token.Line - _firstVisibleLine) * _lineHeight;
            var w = token.Length  * _charWidth;

            dc.DrawRectangle(brush, null, new Rect(x, y, w, _lineHeight));
        }
    }
}
