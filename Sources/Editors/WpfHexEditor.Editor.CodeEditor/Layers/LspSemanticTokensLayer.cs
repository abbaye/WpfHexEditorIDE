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
    private double        _horizontalScrollOffset;
    private double        _topMargin;

    // Maps logical line number → absolute Y inside parent (as computed by _lineYLookup).
    // Accounts for InlineHints zone height and scroll fraction. Snapshot passed each frame.
    private IReadOnlyDictionary<int, double>? _lineYLookup;

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

    /// <summary>
    /// Cancels any in-flight <c>textDocument/semanticTokens/full</c> request.
    /// Call this before dispatching time-sensitive LSP requests (e.g. hover, completion)
    /// so the semantic-tokens fetch does not block the LSP channel.
    /// </summary>
    public void CancelFetch() => _cts?.Cancel();

    /// <summary>Updates the visible range and metrics, then requests a refresh.</summary>
    /// <param name="filePath">Current document path, or <c>null</c> to clear.</param>
    /// <param name="firstLine">First visible logical line.</param>
    /// <param name="lastLine">Last visible logical line.</param>
    /// <param name="charWidth">Monospace character width in pixels.</param>
    /// <param name="lineHeight">Line height in pixels (code-text slot only, excluding InlineHints zone).</param>
    /// <param name="horizontalScrollOffset">Horizontal scroll offset in pixels.</param>
    /// <param name="lineYLookup">
    /// Per-line absolute Y positions (relative to parent top) that account for InlineHints zones
    /// and scroll fraction. Computed by <c>CodeEditor.ComputeVisibleLinePositions()</c>.
    /// Pass <c>null</c> to fall back to the uniform formula.
    /// </param>
    /// <param name="topMargin">Parent's top margin (pixels). The layer is arranged at this offset,
    /// so subtract it from lookup values to get layer-local Y.</param>
    public void SetContext(string? filePath,
                           int firstLine, int lastLine,
                           double charWidth, double lineHeight,
                           double horizontalScrollOffset = 0,
                           IReadOnlyDictionary<int, double>? lineYLookup = null,
                           double topMargin = 0)
    {
        _filePath                = filePath;
        _firstVisibleLine        = firstLine;
        _lastVisibleLine         = lastLine;
        _charWidth               = charWidth;
        _lineHeight              = lineHeight;
        _horizontalScrollOffset  = horizontalScrollOffset;
        _lineYLookup             = lineYLookup;
        _topMargin               = topMargin;

        // Immediately clear stale tokens when the document is closed so that
        // highlights don't persist across the 1 000ms debounce window.
        if (filePath is null)
        {
            _debounce.Stop();
            ClearTokens();
            return;
        }

        // Re-render already-fetched tokens immediately so highlights follow scroll/resize
        // without waiting for the LSP debounce. The debounce only governs the server fetch.
        if (_tokens.Count > 0)
            RenderTokens();

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

            var x = token.Column * _charWidth - _horizontalScrollOffset;

            // Use the lookup when available — it accounts for InlineHints zone height and
            // scroll fraction. The layer is arranged at topMargin, so subtract it to get
            // layer-local Y. Fall back to the uniform formula if the line is absent.
            double y = _lineYLookup is not null && _lineYLookup.TryGetValue(token.Line, out double absY)
                ? absY - _topMargin
                : (token.Line - _firstVisibleLine) * _lineHeight;

            var w = token.Length  * _charWidth;

            dc.DrawRectangle(brush, null, new Rect(x, y, w, _lineHeight));
        }
    }
}
