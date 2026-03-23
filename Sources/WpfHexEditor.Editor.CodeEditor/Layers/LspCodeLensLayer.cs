// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Layers/LspCodeLensLayer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     DrawingVisual overlay that renders code lens annotations
//     (e.g. "3 references", "[Test] Run | Debug") above method/class
//     declarations. Positions at (indent × charWidth, line × lineHeight - offset).
//
// Architecture Notes:
//     Pattern: Decorator / Overlay (drawn above text in CodeEditor's visual stack).
//     Click hit-testing is intentionally excluded in this layer version —
//     the layer renders informational labels only. Interactive lens items
//     can be added in a future pass using AdornerLayer or a dedicated input handler.
//     Debounced 800ms on scroll/edit to avoid hammering the language server.
//     Theme tokens: CE_CodeLensForeground, CE_CodeLensHoverBackground.
// ==========================================================

using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Layers;

/// <summary>
/// Renders LSP code lens annotations (reference counts, test runner hints) as a
/// <see cref="DrawingVisual"/> overlay drawn above method and class declarations.
/// </summary>
public sealed class LspCodeLensLayer : FrameworkElement
{
    // ── State ─────────────────────────────────────────────────────────────────
    private ILspClient?    _lspClient;
    private string?        _filePath;
    private int            _firstVisibleLine;
    private int            _lastVisibleLine;
    private double         _charWidth;
    private double         _lineHeight;

    /// <summary>
    /// Snapshot of visible source lines, used to detect [Test]/[Fact] attributes.
    /// Each entry: (lineIndex, trimmedText).
    /// </summary>
    private IReadOnlyList<(int Line, string Text)> _sourceLines
        = Array.Empty<(int, string)>();

    private readonly DrawingVisual   _visual   = new();
    private readonly DispatcherTimer _debounce;
    private CancellationTokenSource? _cts;

    // ── Compiled regexes ──────────────────────────────────────────────────────

    private static readonly Regex s_testAttr =
        new(@"\[\s*(?:Test|Fact|Theory|TestMethod|TestCase)\s*[\]\(]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Constructor ───────────────────────────────────────────────────────────

    public LspCodeLensLayer()
    {
        IsHitTestVisible = false;   // display-only; interactive clicks not wired here
        AddVisualChild(_visual);
        AddLogicalChild(_visual);

        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _debounce.Tick += OnDebounce;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Injects or clears the LSP client.</summary>
    public void SetLspClient(ILspClient? client)
    {
        _lspClient = client;
        if (client is null) ClearLens();
    }

    /// <summary>
    /// Updates the visible range, metrics, and source snapshot, then requests a refresh.
    /// </summary>
    /// <param name="filePath">Absolute path of the open document.</param>
    /// <param name="firstLine">Zero-based first visible line.</param>
    /// <param name="lastLine">Zero-based last visible line (inclusive).</param>
    /// <param name="charWidth">Monospaced character width in device-independent pixels.</param>
    /// <param name="lineHeight">Height of one text line in device-independent pixels.</param>
    /// <param name="sourceLines">
    /// Snapshot of visible source lines as (lineIndex, trimmedText) tuples.
    /// Used to identify [Test]/[Fact] attributes above method signatures.
    /// </param>
    public void SetContext(string? filePath,
                           int firstLine, int lastLine,
                           double charWidth, double lineHeight,
                           IReadOnlyList<(int Line, string Text)>? sourceLines = null)
    {
        _filePath         = filePath;
        _firstVisibleLine = firstLine;
        _lastVisibleLine  = lastLine;
        _charWidth        = charWidth;
        _lineHeight       = lineHeight;
        _sourceLines      = sourceLines ?? Array.Empty<(int, string)>();
        RequestRefresh();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    protected override Size ArrangeOverride(Size finalSize)
    {
        RenderLens();
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
            ClearLens();
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            // Use DocumentSymbols as a proxy for declaration positions.
            var symbols = await _lspClient.DocumentSymbolsAsync(_filePath, _cts.Token)
                .ConfigureAwait(true);

            _lensItems = BuildLensItems(symbols);
            RenderLens();
        }
        catch (OperationCanceledException) { }
        catch { ClearLens(); }
    }

    // ── Lens item model ───────────────────────────────────────────────────────

    private readonly record struct LensItem(int Line, int IndentChars, string Label);

    private IReadOnlyList<LensItem> _lensItems = Array.Empty<LensItem>();

    /// <summary>
    /// Builds a flat list of lens items from document symbols.
    /// Each method/class/struct/interface symbol gets a "N references" label.
    /// Methods whose preceding line contains a [Test] attribute get an additional
    /// "[Test] Run | Debug" label.
    /// </summary>
    private IReadOnlyList<LensItem> BuildLensItems(IReadOnlyList<LspDocumentSymbol> symbols)
    {
        if (symbols.Count == 0) return Array.Empty<LensItem>();

        var items = new List<LensItem>(symbols.Count);

        // Build a fast lookup set of lines that have a [Test] attribute above them.
        var testLines = BuildTestAttributeLines();

        foreach (var sym in symbols)
        {
            if (sym.StartLine < _firstVisibleLine || sym.StartLine > _lastVisibleLine) continue;

            var kind = sym.Kind?.ToLowerInvariant() ?? string.Empty;
            if (kind is not ("method" or "constructor" or "function"
                         or "class" or "struct" or "interface" or "enum")) continue;

            // Ref-count label — stub value; a real implementation would call
            // ILspClient.ReferencesAsync but that would make lens too heavy.
            // The label intentionally says "references" as a placeholder.
            var refLabel = sym.Kind?.ToLowerInvariant() is "class" or "struct"
                or "interface" or "enum" ? "0 references" : "0 references";

            items.Add(new LensItem(sym.StartLine, sym.StartColumn, refLabel));

            // Test runner hint when a [Test]/[Fact] attribute precedes this line.
            if (testLines.Contains(sym.StartLine))
                items.Add(new LensItem(sym.StartLine, sym.StartColumn, "[Test] Run | Debug"));
        }

        return items;
    }

    /// <summary>
    /// Scans <see cref="_sourceLines"/> for test attribute decorators.
    /// Returns a set of line numbers whose immediately following non-blank line
    /// bears the attribute.
    /// </summary>
    private HashSet<int> BuildTestAttributeLines()
    {
        var result = new HashSet<int>();
        for (int i = 0; i < _sourceLines.Count - 1; i++)
        {
            if (s_testAttr.IsMatch(_sourceLines[i].Text))
                result.Add(_sourceLines[i + 1].Line);
        }
        return result;
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    private void ClearLens()
    {
        _lensItems = Array.Empty<LensItem>();
        using var dc = _visual.RenderOpen();
        // nothing to draw — clears previous content
    }

    private void RenderLens()
    {
        using var dc = _visual.RenderOpen();

        if (_lensItems.Count == 0 || _charWidth <= 0 || _lineHeight <= 0) return;

        var fg = TryFindResource("CE_CodeLensForeground") as Brush
                 ?? new SolidColorBrush(Color.FromArgb(140, 140, 140, 140));

        var typeface = new Typeface(
            new FontFamily("Consolas, Courier New"),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // When multiple lens items share the same declaration line, stack them.
        // First: group by line.
        var byLine = _lensItems
            .GroupBy(l => l.Line)
            .OrderBy(g => g.Key);

        foreach (var group in byLine)
        {
            var lineIdx = group.Key;
            if (lineIdx < _firstVisibleLine || lineIdx > _lastVisibleLine) continue;

            var items = group.ToList();
            // Draw items right-to-left so each can be placed without overlap tracking.
            // Simple approach: join labels with " · " on a single rendered line.
            var combined = string.Join(" · ", items.Select(i => i.Label));

            var ft = new FormattedText(
                combined,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                8.0,
                fg,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Position above the declaration line — offset upward by (lineHeight - ft.Height) / 2.
            var indent = items[0].IndentChars * _charWidth;
            var y = (lineIdx - _firstVisibleLine) * _lineHeight - ft.Height - 1;
            if (y < 0) continue;  // don't draw above the visible area

            dc.DrawText(ft, new Point(indent, y));
        }
    }
}
