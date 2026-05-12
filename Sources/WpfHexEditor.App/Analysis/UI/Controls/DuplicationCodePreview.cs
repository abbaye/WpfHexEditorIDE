// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/Controls/DuplicationCodePreview.cs
// Description: Side-by-side preview of a clone group's two occurrences with
//              line numbers, monospace text, and a whitespace-only diff
//              highlight. Reads only the windowed range of each file and
//              caches the snippet keyed by (path, mtime, startLine, count).
// Architecture Notes:
//     - All brushes are static frozen — no per-render allocation
//     - File reads use File.ReadLines + Skip/Take so a 50k-line file with a
//       30-line clone window costs O(window), not O(file)
//     - Snippet cache evicts oldest when over MaxCacheEntries, scoped per
//       (path, mtime, startLine, count) so two clones in the same file
//       coexist without re-reading
// ==========================================================

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Analysis.UI.ViewModels;

namespace WpfHexEditor.App.Analysis.UI.Controls;

public sealed class DuplicationCodePreview : FrameworkElement
{
    public static readonly DependencyProperty GroupProperty =
        DependencyProperty.Register(nameof(Group), typeof(DuplicationGroupViewModel), typeof(DuplicationCodePreview),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public DuplicationGroupViewModel? Group
    {
        get => (DuplicationGroupViewModel?)GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    private const int    MaxLinesShown = 200;
    private const double LineHeight    = 16;
    private const double GutterWidth   = 56;
    private const double FontSize      = 11;

    private static readonly Typeface       MonoFace = new("Consolas");
    private static readonly SolidColorBrush GutterFg = Freeze(new SolidColorBrush(Color.FromArgb(0x99, 0x80, 0x80, 0x80)));
    private static readonly SolidColorBrush SameBg   = Freeze(new SolidColorBrush(Color.FromArgb(28, 60, 200, 100)));
    private static readonly SolidColorBrush WsBg     = Freeze(new SolidColorBrush(Color.FromArgb(28, 220, 180, 0)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    // ── Snippet cache (bounded LRU) ──────────────────────────────────────────

    private sealed record CacheKey(string Path, DateTime Mtime, long Length, int StartLine, int Count);
    private static readonly object _cacheLock = new();
    private static readonly Dictionary<CacheKey, string[]> _cache = new();
    private static readonly LinkedList<CacheKey>           _lru   = new();
    private const int MaxCacheEntries = 64;

    private static string[] ReadSnippetCached(string path, int startLine, int endLine)
    {
        if (string.IsNullOrEmpty(path) || startLine < 1) return [];
        FileInfo info;
        try { info = new FileInfo(path); if (!info.Exists) return []; }
        catch { return []; }

        int from  = startLine - 1;
        int count = Math.Max(0, endLine - startLine + 1);
        var key   = new CacheKey(path, info.LastWriteTimeUtc, info.Length, from, count);

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var hit))
            {
                _lru.Remove(key); _lru.AddFirst(key);
                return hit;
            }
        }

        string[] snippet;
        try { snippet = File.ReadLines(path).Skip(from).Take(count).ToArray(); }
        catch { return []; }

        lock (_cacheLock)
        {
            _cache[key] = snippet;
            _lru.AddFirst(key);
            while (_lru.Count > MaxCacheEntries)
            {
                var oldest = _lru.Last!.Value;
                _lru.RemoveLast();
                _cache.Remove(oldest);
            }
        }
        return snippet;
    }

    // ── Render ───────────────────────────────────────────────────────────────

    public DuplicationCodePreview()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding   = true;
        MinHeight = 120;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Group is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var occA = Group.OccurrenceA;
        if (occA is null) return;
        var occB = Group.OccurrenceB;

        double half = (ActualWidth - 8) / 2;
        var fg = TextElement.GetForeground(this) ?? SystemColors.ControlTextBrush;

        string[] snippetA = ReadSnippetCached(occA.FilePath, occA.StartLine, occA.EndLine);
        string[] snippetB = occB is null ? [] : ReadSnippetCached(occB.FilePath, occB.StartLine, occB.EndLine);

        DrawPane(dc, x: 0,         half, ActualHeight, snippetA, otherSnippet: snippetB, occA.StartLine, occA.EndLine, fg);
        if (occB is not null)
            DrawPane(dc, x: half + 8, half, ActualHeight, snippetB, otherSnippet: snippetA, occB.StartLine, occB.EndLine, fg);
    }

    private static void DrawPane(
        DrawingContext dc, double x, double width, double height,
        string[] snippet, string[] otherSnippet,
        int startLine, int endLine,
        Brush fg)
    {
        dc.PushClip(new RectangleGeometry(new Rect(x, 0, width, height)));

        int totalLines = Math.Max(0, endLine - startLine + 1);
        int shown      = Math.Min(snippet.Length, MaxLinesShown);
        double codeMaxWidth = Math.Max(20, width - GutterWidth - 8);
        double y = 4;

        for (int i = 0; i < shown; i++)
        {
            if (y + LineHeight > height - 4) { shown = i; break; }

            if (i < otherSnippet.Length)
            {
                var a = snippet[i];
                var b = otherSnippet[i];
                Brush? rowBg =
                    string.Equals(a, b, StringComparison.Ordinal)                       ? SameBg :
                    string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal) ? WsBg   :
                    null;
                if (rowBg is not null)
                    dc.DrawRectangle(rowBg, null, new Rect(x, y, width, LineHeight));
            }

            var gutterFt = new FormattedText((startLine + i).ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MonoFace, FontSize, GutterFg, 1.0)
                { TextAlignment = TextAlignment.Right, MaxTextWidth = GutterWidth - 8 };
            dc.DrawText(gutterFt, new Point(x + 4, y));

            string text = (snippet[i] ?? string.Empty).Replace("\t", "    ");
            var codeFt = new FormattedText(text, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, MonoFace, FontSize, fg, 1.0)
                { MaxTextWidth = codeMaxWidth };
            dc.DrawText(codeFt, new Point(x + GutterWidth, y));

            y += LineHeight;
        }

        int hidden = totalLines - shown;
        if (hidden > 0)
        {
            var footer = new FormattedText($"… +{hidden} lines",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                MonoFace, FontSize, GutterFg, 1.0);
            dc.DrawText(footer, new Point(x + GutterWidth, y + 4));
        }

        dc.Pop();
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        bool prevSpace = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace && sb.Length > 0) sb.Append(' ');
                prevSpace = true;
            }
            else { sb.Append(ch); prevSpace = false; }
        }
        return sb.ToString().Trim();
    }

    /// <summary>Drop all cached snippets — called when a fresh report arrives so stale entries don't survive analyses.</summary>
    internal static void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
            _lru.Clear();
        }
    }
}
