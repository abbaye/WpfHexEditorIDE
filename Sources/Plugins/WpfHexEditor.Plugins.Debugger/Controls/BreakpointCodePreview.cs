// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Controls/BreakpointCodePreview.cs
// Description:
//     GlyphRun-based syntax-highlighted code preview FrameworkElement.
//     Resolves language from file extension via LanguageRegistry,
//     tokenizes using LanguageDefinition.SyntaxRules, renders with
//     DrawingContext GlyphRun for zero-allocation per-frame rendering.
// Architecture:
//     Plugin-local — depends only on WpfHexEditor.Core.ProjectSystem
//     (available via SDK transitive re-export). No CodeEditor/TextEditor dep.
//     GlyphRun pattern mirrors DiffGlyphHelper in FileComparison plugin.
// ==========================================================

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Plugins.Debugger.Controls;

internal sealed class BreakpointCodePreview : FrameworkElement
{
    // ── Dependency Properties ──────────────────────────────────────────────

    public static readonly DependencyProperty SourceTextProperty =
        DependencyProperty.Register(nameof(SourceText), typeof(string), typeof(BreakpointCodePreview),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FilePathProperty =
        DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(BreakpointCodePreview),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d, _) => ((BreakpointCodePreview)d).OnFilePathChanged()));

    public string SourceText
    {
        get => (string)GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    public string FilePath
    {
        get => (string)GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    // ── Font constants ─────────────────────────────────────────────────────

    private const double FontSize = 10.5;
    private static readonly Typeface _typeface = new("Consolas");

    // ── GlyphTypeface cache (flyweight, shared across instances) ───────────

    private static readonly Dictionary<Typeface, GlyphTypeface?> _gtCache = [];
    private static GlyphTypeface? _gt;
    private static double _charWidth;
    private static double _rowHeight;
    private static double _baseline;
    private static bool   _metricsReady;

    // ── Thread-static pooled GlyphRun lists ────────────────────────────────

    [ThreadStatic] private static List<ushort>? _glyphPool;
    [ThreadStatic] private static List<double>? _advancePool;

    // ── Per-instance tokenizer ─────────────────────────────────────────────

    private List<(Regex regex, Brush brush)>? _rules;
    private float _pixelsPerDip = 1f;

    // ── Constructor ────────────────────────────────────────────────────────

    public BreakpointCodePreview()
    {
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _pixelsPerDip = (float)VisualTreeHelper.GetDpi(this).PixelsPerDip;
        EnsureMetrics();
    }

    // ── Metric initialisation ──────────────────────────────────────────────

    private void EnsureMetrics()
    {
        if (_metricsReady) return;

        if (!_gtCache.TryGetValue(_typeface, out _gt))
        {
            _gt = _typeface.TryGetGlyphTypeface(out var resolved) ? resolved : null;
            _gtCache[_typeface] = _gt;
        }

        if (_gt is not null)
        {
            _gt.CharacterToGlyphMap.TryGetValue('M', out ushort gi);
            _charWidth = _gt.AdvanceWidths[gi] * FontSize;
            _rowHeight  = _gt.Height           * FontSize;
            _baseline   = _gt.Baseline         * FontSize;
        }
        else
        {
            var ft = new FormattedText("M", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _typeface, FontSize, Brushes.Black, _pixelsPerDip);
            _charWidth = ft.Width;
            _rowHeight  = ft.Height;
            _baseline   = ft.Height * 0.8;
        }

        _metricsReady = true;
    }

    // ── DP callbacks ───────────────────────────────────────────────────────

    private void OnFilePathChanged()
    {
        _rules = null;
        BuildRules();
        InvalidateVisual();
    }

    // ── Tokenizer build ────────────────────────────────────────────────────

    private void BuildRules()
    {
        var fp = FilePath;
        if (string.IsNullOrEmpty(fp)) return;

        var lang = LanguageRegistry.Instance.GetLanguageForFile(fp);
        if (lang is null) return;

        _rules = [];
        foreach (var rule in lang.SyntaxRules)
        {
            if (string.IsNullOrEmpty(rule.Pattern)) continue;
            try
            {
                var regex = new Regex(rule.Pattern,
                    RegexOptions.Compiled | RegexOptions.ExplicitCapture,
                    TimeSpan.FromMilliseconds(50));
                var brush = ResolveBrush(rule.Kind);
                _rules.Add((regex, brush));
            }
            catch { /* invalid pattern — skip */ }
        }
    }

    private static Brush ResolveBrush(SyntaxTokenKind kind)
    {
        var key = kind switch
        {
            SyntaxTokenKind.Keyword     => "CE_Keyword",
            SyntaxTokenKind.ControlFlow => "CE_ControlFlow",
            SyntaxTokenKind.String      => "CE_String",
            SyntaxTokenKind.Number      => "CE_Number",
            SyntaxTokenKind.Comment     => "CE_Comment",
            SyntaxTokenKind.Type        => "CE_Type",
            SyntaxTokenKind.Identifier  => "CE_Identifier",
            SyntaxTokenKind.Operator    => "CE_Operator",
            SyntaxTokenKind.Bracket     => "CE_Bracket",
            SyntaxTokenKind.Attribute   => "CE_Attribute",
            _                           => null,
        };
        return key is not null && System.Windows.Application.Current?.TryFindResource(key) is Brush b
            ? b
            : SystemColors.ControlTextBrush;
    }

    // ── Per-line tokenisation ──────────────────────────────────────────────

    private List<(int start, int length, Brush brush)> TokenizeLine(string line)
    {
        var result = new List<(int start, int length, Brush brush)>();
        if (_rules is null || _rules.Count == 0 || line.Length == 0)
            return result;

        var covered = new bool[line.Length];
        foreach (var (regex, brush) in _rules)
        {
            try
            {
                foreach (Match m in regex.Matches(line))
                {
                    if (!m.Success || m.Length == 0) continue;
                    var g = m.Groups.Count > 1 && m.Groups[1].Success ? m.Groups[1] : m.Groups[0];
                    int s = g.Index, e = s + g.Length;
                    if (s >= line.Length || e > line.Length) continue;
                    bool overlaps = false;
                    for (int i = s; i < e; i++) if (covered[i]) { overlaps = true; break; }
                    if (overlaps) continue;
                    for (int i = s; i < e; i++) covered[i] = true;
                    result.Add((s, g.Length, brush));
                }
            }
            catch (RegexMatchTimeoutException) { /* pathological input — skip rule */ }
        }

        result.Sort(static (a, b) => a.start.CompareTo(b.start));
        return result;
    }

    // ── Layout ─────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureMetrics();
        var text = SourceText;
        if (string.IsNullOrEmpty(text) || _rowHeight == 0)
            return new Size(0, 0);

        var lines = text.Split('\n');
        double maxW = 0;
        foreach (var l in lines)
            maxW = Math.Max(maxW, l.TrimEnd('\r').Length * _charWidth);

        return new Size(maxW, lines.Length * _rowHeight);
    }

    // ── Rendering ──────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var text = SourceText;
        if (string.IsNullOrEmpty(text)) return;

        EnsureMetrics();
        if (_rowHeight == 0) return;
        if (_rules is null) BuildRules();

        var defaultFg =
            System.Windows.Application.Current?.TryFindResource("DockMenuForegroundBrush") as Brush
            ?? Brushes.White;

        var lines = text.Split('\n');
        for (int li = 0; li < lines.Length; li++)
        {
            var line = lines[li].TrimEnd('\r');
            if (line.Length == 0) continue;

            double topY  = li * _rowHeight;
            var    tokens = TokenizeLine(line);

            int pos = 0;
            foreach (var (start, length, brush) in tokens)
            {
                if (start > pos)
                    DrawSegment(dc, line, pos, start - pos, topY, defaultFg);
                DrawSegment(dc, line, start, length, topY, brush);
                pos = start + length;
            }
            if (pos < line.Length)
                DrawSegment(dc, line, pos, line.Length - pos, topY, defaultFg);
        }
    }

    private void DrawSegment(DrawingContext dc, string line, int start, int length,
                              double topY, Brush brush)
    {
        if (length <= 0) return;
        var  text      = line.Substring(start, length);
        double x       = start * _charWidth;
        double baseline = topY + _baseline;

        if (_gt is not null)
        {
            var glyphs   = _glyphPool   ??= new List<ushort>(256);
            var advances = _advancePool ??= new List<double>(256);
            glyphs.Clear();
            advances.Clear();

            var map = _gt.CharacterToGlyphMap;
            foreach (char ch in text)
            {
                if (ch == '\t')
                {
                    map.TryGetValue(' ', out ushort sp);
                    glyphs.Add(sp);
                    advances.Add(_gt.AdvanceWidths[sp] * FontSize * 4);
                    continue;
                }
                if (!map.TryGetValue(ch, out ushort gi))
                    map.TryGetValue('\uFFFD', out gi);
                glyphs.Add(gi);
                advances.Add(_gt.AdvanceWidths[gi] * FontSize);
            }
            if (glyphs.Count == 0) return;

            var run = new GlyphRun(
                _gt, 0, false, FontSize, _pixelsPerDip,
                glyphs.ToArray(),
                new Point(x, baseline),
                advances.ToArray(),
                null, null, null, null, null, null);

            dc.DrawGlyphRun(brush, run);
        }
        else
        {
            var ft = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _typeface, FontSize, brush, _pixelsPerDip);
            dc.DrawText(ft, new Point(x, topY));
        }
    }
}
