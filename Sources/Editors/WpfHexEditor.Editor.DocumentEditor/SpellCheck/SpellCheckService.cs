// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: SpellCheck/SpellCheckService.cs
// Description:
//     Coordinates ISpellChecker with the DocumentCanvasRenderer.
//     Debounces analysis 800ms after BlocksUpdated, caches results per block
//     (BlockId + text version), maps CharOffset→canvas coordinates via GlyphLines,
//     and feeds SpellCheckLayer with canvas-space SpellCheckError markers.
// Architecture:
//     All analysis runs on a ThreadPool thread; UI updates are Dispatcher-marshalled.
//     Cache invalidation: if block text changes, the entry is evicted.
//     Word tokenizer excludes URLs, numbers, CamelCase segments, and common code tokens.
// ==========================================================

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.Core.SpellCheck;
using WpfHexEditor.Editor.DocumentEditor.Controls;
using WpfHexEditor.Editor.DocumentEditor.Layers;
using WpfHexEditor.Editor.DocumentEditor.Rendering;

namespace WpfHexEditor.Editor.DocumentEditor.SpellCheck;

internal sealed class SpellCheckService : IDisposable
{
    private readonly ISpellChecker       _checker;
    private readonly SpellCheckLayer     _layer;
    private readonly DispatcherTimer     _debounce;
    private DocumentCanvasRenderer?      _renderer;
    private CancellationTokenSource      _cts = new();

    // Cache: blockId → (text snapshot, errors)
    private readonly Dictionary<string, (string Text, List<SpellCheckError> Errors)> _cache = [];

    private readonly HashSet<string> _ignoredWords = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex WordRx = new(
        @"\b[^\W\d_]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SkipRx = new(
        @"https?://|[A-Z][a-z]+[A-Z]|\w+\.\w+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public SpellCheckService(ISpellChecker checker, SpellCheckLayer layer)
    {
        _checker = checker;
        _layer   = layer;

        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            TriggerAnalysis();
        };

        _checker.DictionaryChanged += (_, _) =>
        {
            _cache.Clear();
            ScheduleAnalysis();
        };
    }

    public void Attach(DocumentCanvasRenderer renderer)
    {
        if (_renderer is not null)
            _renderer.BlocksUpdated -= OnBlocksUpdated;

        _renderer = renderer;
        _renderer.BlocksUpdated += OnBlocksUpdated;

        renderer.AddSpellCheckLayer(_layer);

        // Auto-load all installed dictionaries for multi-language support
        _ = _checker.LoadAllInstalledAsync();
    }

    public void Detach()
    {
        if (_renderer is null) return;
        _renderer.BlocksUpdated -= OnBlocksUpdated;
        _renderer = null;
        _layer.Clear();
    }

    public ISpellChecker Checker => _checker;

    public void IgnoreWord(string word) => _ignoredWords.Add(word);

    public void InvalidateAll()
    {
        _cache.Clear();
        ScheduleAnalysis();
    }

    /// <summary>
    /// Clears visible squiggles immediately (no stale markers during zoom/resize),
    /// then schedules re-analysis via the normal debounce.
    /// </summary>
    public void ClearAndSchedule()
    {
        _layer.Clear();
        _cache.Clear();
        ScheduleAnalysis();
    }

    /// <summary>Returns the misspelled word at canvas point <paramref name="pt"/>, or null.</summary>
    public SpellCheckError? HitTest(Point pt)
    {
        foreach (var entry in _cache.Values)
        foreach (var err in entry.Errors)
        {
            var r = new Rect(err.CanvasX, err.CanvasY, err.CanvasWidth, err.LineHeight);
            if (r.Contains(pt)) return err;
        }
        return null;
    }

    private void OnBlocksUpdated(object? sender, EventArgs e) => ScheduleAnalysis();

    private void ScheduleAnalysis()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void TriggerAnalysis()
    {
        if (_renderer is null || !_checker.IsLoaded) return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var blocks      = _renderer.LayoutBlocks;
        var contentOriX = _renderer.ContentOriginX;
        var zoom        = _renderer.ZoomFactor;

        Task.Run(() => Analyse(blocks, contentOriX, zoom, ct), ct);
    }

    private void Analyse(
        IReadOnlyList<RenderBlock> blocks,
        double contentOriX,
        double zoom,
        CancellationToken ct)
    {
        var allErrors = new List<SpellCheckError>();

        foreach (var rb in blocks)
        {
            ct.ThrowIfCancellationRequested();

            // Only check paragraph and list-item text
            if (rb.Block.Kind is not ("paragraph" or "list-item" or "heading")) continue;

            var text = rb.Block.Text ?? string.Empty;
            if (text.Length == 0) continue;
            if (text.Length > 5000) text = text[..5000];

            var blockId = rb.Y.ToString("F0");
            if (_cache.TryGetValue(blockId, out var cached) && cached.Text == text)
            {
                allErrors.AddRange(cached.Errors);
                continue;
            }

            var blockErrors = new List<SpellCheckError>();
            var matches     = WordRx.Matches(text);

            foreach (Match m in matches)
            {
                ct.ThrowIfCancellationRequested();
                var word = m.Value;
                if (_ignoredWords.Contains(word)) continue;
                if (SkipRx.IsMatch(word)) continue;
                if (_checker.CheckWord(word)) continue;

                var (cx, cy, cw, ch) = MapToCanvas(rb, m.Index, m.Length, contentOriX, zoom);
                if (cw <= 0) continue;

                blockErrors.Add(new SpellCheckError(
                    new SpellCheckResult(m.Index, m.Length, word),
                    cx, cy, cw, ch));
            }

            _cache[blockId] = (text, blockErrors);
            allErrors.AddRange(blockErrors);
        }

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!ct.IsCancellationRequested)
                _layer.SetErrors(allErrors);
        });
    }

    private static (double X, double Y, double W, double H) MapToCanvas(
        RenderBlock rb, int charStart, int charLen,
        double contentOriX, double zoom)
    {
        if (rb.GlyphLines is { Count: > 0 })
        {
            int    charEnd = charStart + charLen;
            double x0      = -1, x1 = -1;
            double lineY   = rb.Y, lineH = 0;
            double lineTop = rb.Y;

            foreach (var line in rb.GlyphLines)
            {
                foreach (var seg in line.Segments)
                {
                    double segX = contentOriX + rb.IndentLeft + seg.OffsetX;
                    double accW = 0;
                    for (int i = 0; i < seg.AdvanceWidths.Count; i++)
                    {
                        int    globalChar = seg.CharStart + i;
                        double gx         = segX + accW;

                        if (globalChar == charStart)
                        {
                            x0    = gx;
                            lineY = lineTop;
                            lineH = line.LineHeight;
                        }
                        if (globalChar == charEnd) { x1 = gx; break; }

                        accW += seg.AdvanceWidths[i];
                    }
                    if (x1 > 0) break;
                }
                if (x1 > 0) break;
                lineTop += line.LineHeight;
            }
            if (x0 >= 0 && x1 > x0)
                return (x0, lineY, x1 - x0, lineH > 0 ? lineH : 16);
        }

        if (rb.FormattedLines is { Count: > 0 })
        {
            var ft = rb.FormattedLines[0];
            if (ft.Text.Length > 0)
            {
                double ratio = (double)charLen / ft.Text.Length;
                double approxW = ft.Width * ratio;
                double approxX = contentOriX + rb.IndentLeft + ft.Width * ((double)charStart / ft.Text.Length);
                return (approxX, rb.Y, approxW, ft.Height / Math.Max(1, rb.FormattedLines.Count));
            }
        }

        return (0, 0, 0, 0);
    }

    public void Dispose()
    {
        _debounce.Stop();
        _cts.Cancel();
        _cts.Dispose();
    }
}
