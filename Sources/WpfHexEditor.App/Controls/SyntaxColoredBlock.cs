// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// Reusable WPF control that renders syntax-highlighted source code.
/// Lives in the App layer and is exposed to plugins via <see cref="IUIControlFactory"/>.
/// Plugins never reference this class directly — they receive a <see cref="FrameworkElement"/>.
/// </summary>
internal sealed class SyntaxColoredBlock : StackPanel
{
    // ── Service ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Per-instance coloring service. Set by <see cref="Services.UIControlFactory"/>.
    /// Takes priority over <see cref="ColoringService"/> (static fallback).
    /// </summary>
    internal ISyntaxColoringService? ColoringServiceInstance { get; init; }

    private ISyntaxColoringService? ResolvedService => ColoringServiceInstance ?? ColoringService;

    /// <summary>
    /// Static fallback — set once at app startup for controls created outside the factory.
    /// </summary>
    public static ISyntaxColoringService? ColoringService { get; set; }

    // Allow derived classes in other assemblies to access the static service
    protected static ISyntaxColoringService? GetColoringService() => ColoringService;

    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty FilePathProperty =
        DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(SyntaxColoredBlock),
            new FrameworkPropertyMetadata(null, (d, _) => ((SyntaxColoredBlock)d).ScheduleRebuild()));

    public static readonly DependencyProperty LineProperty =
        DependencyProperty.Register(nameof(Line), typeof(int), typeof(SyntaxColoredBlock),
            new FrameworkPropertyMetadata(0, (d, _) => ((SyntaxColoredBlock)d).ScheduleRebuild()));

    public static readonly DependencyProperty ContextLinesProperty =
        DependencyProperty.Register(nameof(ContextLines), typeof(int), typeof(SyntaxColoredBlock),
            new FrameworkPropertyMetadata(2, (d, _) => ((SyntaxColoredBlock)d).ScheduleRebuild()));

    public static readonly DependencyProperty SourceTextProperty =
        DependencyProperty.Register(nameof(SourceText), typeof(string), typeof(SyntaxColoredBlock),
            new FrameworkPropertyMetadata(null, (d, _) => ((SyntaxColoredBlock)d).ScheduleRebuild()));

    public static readonly DependencyProperty LanguageIdProperty =
        DependencyProperty.Register(nameof(LanguageId), typeof(string), typeof(SyntaxColoredBlock),
            new FrameworkPropertyMetadata(null, (d, _) => ((SyntaxColoredBlock)d).ScheduleRebuild()));

    public static readonly DependencyProperty FontSizeCodeProperty =
        DependencyProperty.Register(nameof(FontSizeCode), typeof(double), typeof(SyntaxColoredBlock),
            new FrameworkPropertyMetadata(12.0, (d, _) => ((SyntaxColoredBlock)d).ScheduleRebuild()));

    public static readonly DependencyProperty HighlightBreakLineProperty =
        DependencyProperty.Register(nameof(HighlightBreakLine), typeof(bool), typeof(SyntaxColoredBlock),
            new FrameworkPropertyMetadata(true, (d, _) => ((SyntaxColoredBlock)d).ScheduleRebuild()));

    /// <summary>
    /// 0-based index of the focus line within <see cref="SourceText"/>.
    /// Used when <see cref="SourceText"/> is set directly (no file I/O).
    /// -1 means no highlight.
    /// </summary>
    public static readonly DependencyProperty FocusLineIndexProperty =
        DependencyProperty.Register(nameof(FocusLineIndex), typeof(int), typeof(SyntaxColoredBlock),
            new FrameworkPropertyMetadata(-1, (d, _) => ((SyntaxColoredBlock)d).ScheduleRebuild()));

    public int FocusLineIndex
    {
        get => (int)GetValue(FocusLineIndexProperty);
        set => SetValue(FocusLineIndexProperty, value);
    }

    /// <summary>Full path to the source file. Used together with <see cref="Line"/>.</summary>
    public string? FilePath
    {
        get => (string?)GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    /// <summary>1-based line number for the breakpoint / focus line.</summary>
    public int Line
    {
        get => (int)GetValue(LineProperty);
        set => SetValue(LineProperty, value);
    }

    /// <summary>Number of lines before and after <see cref="Line"/> to show (default: 2).</summary>
    public int ContextLines
    {
        get => (int)GetValue(ContextLinesProperty);
        set => SetValue(ContextLinesProperty, value);
    }

    /// <summary>
    /// Raw source text to colorise directly.
    /// When set, <see cref="FilePath"/>/<see cref="Line"/> are ignored.
    /// Requires <see cref="LanguageId"/> to be set for coloring.
    /// </summary>
    public string? SourceText
    {
        get => (string?)GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    /// <summary>Language identifier (e.g. "csharp", "json"). Used with <see cref="SourceText"/>.</summary>
    public string? LanguageId
    {
        get => (string?)GetValue(LanguageIdProperty);
        set => SetValue(LanguageIdProperty, value);
    }

    /// <summary>Font size for the rendered code lines (default: 12).</summary>
    public double FontSizeCode
    {
        get => (double)GetValue(FontSizeCodeProperty);
        set => SetValue(FontSizeCodeProperty, value);
    }

    /// <summary>When true, the focus line is highlighted with a yellow background (default: true).</summary>
    public bool HighlightBreakLine
    {
        get => (bool)GetValue(HighlightBreakLineProperty);
        set => SetValue(HighlightBreakLineProperty, value);
    }

    // ── Visual tree ───────────────────────────────────────────────────────────

    public SyntaxColoredBlock()
    {
        Orientation = Orientation.Vertical;
    }

    // ── Rebuild logic ─────────────────────────────────────────────────────────

    private void ScheduleRebuild() => Rebuild();

    private void Rebuild()
    {
        Children.Clear();

        // ── Mode 1: FilePath + Line ──────────────────────────────────────────
        string[]? sourceLines = null;
        int       focusIndex  = -1;      // index inside sourceLines of the focus line

        var fp   = FilePath;
        var line = Line;
        if (!string.IsNullOrEmpty(fp) && line > 0 && File.Exists(fp))
        {
            string[] allLines;
            try   { allLines = File.ReadAllLines(fp); }
            catch { allLines = []; }

            if (allLines.Length > 0 && line <= allLines.Length)
            {
                int ctx   = Math.Max(0, ContextLines);
                int start = Math.Max(0, line - 1 - ctx);
                int end   = Math.Clamp(line - 1 + ctx, start, allLines.Length - 1);
                sourceLines = allLines[start..(end + 1)];
                focusIndex  = line - 1 - start;
            }

            // Resolve language from extension when not explicitly set
            var langId = LanguageId
                      ?? ResolvedService?.ResolveLanguageId(Path.GetExtension(fp));
            RenderLines(sourceLines ?? [], langId, focusIndex);
            return;
        }

        // ── Mode 2: SourceText + LanguageId ─────────────────────────────────
        var src = SourceText;
        if (!string.IsNullOrEmpty(src))
        {
            sourceLines = src.Split('\n');
            RenderLines(sourceLines, LanguageId, focusIndex: FocusLineIndex);
        }
    }

    private void RenderLines(string[] lines, string? languageId, int focusIndex)
    {
        if (lines.Length == 0) return;

        // Strip common leading whitespace
        int commonIndent = 0;
        var nonEmpty = lines.Where(l => l.Trim().Length > 0).ToList();
        if (nonEmpty.Count > 0)
            commonIndent = nonEmpty.Min(l => l.Length - l.TrimStart().Length);

        // Colorize
        IReadOnlyList<IReadOnlyList<ColoredSpan>>? colorized = null;
        if (!string.IsNullOrEmpty(languageId) && ResolvedService is not null)
        {
            var trimmed = lines.Select(l => l.TrimEnd('\r')).ToArray();
            try   { colorized = ResolvedService.ColorizeLines(trimmed, languageId); }
            catch { }
        }

        // Brushes — Application.Current is always available; TryFindResource works once in tree
        var defaultFg = (IsLoaded ? TryFindResource("DockMenuForegroundBrush") : null) as Brush
                     ?? Application.Current?.TryFindResource("DockMenuForegroundBrush") as Brush
                     ?? Brushes.Gainsboro;
        var highlightBg = new SolidColorBrush(Color.FromArgb(50, 255, 210, 0));
        var fontFamily  = new FontFamily("Consolas");

        for (int i = 0; i < lines.Length; i++)
        {
            var  rawLine    = lines[i].TrimEnd('\r');
            var  stripped   = rawLine.Length >= commonIndent ? rawLine[commonIndent..] : rawLine;
            bool isFocusLine = HighlightBreakLine && i == focusIndex;

            var tb = new TextBlock
            {
                FontFamily   = fontFamily,
                FontSize     = FontSizeCode,
                TextWrapping = TextWrapping.NoWrap,
                Foreground   = defaultFg,
                Background   = isFocusLine ? highlightBg : Brushes.Transparent,
                Padding      = new Thickness(4, 1, 4, 1),
            };

            var spans = colorized is not null && i < colorized.Count ? colorized[i] : null;

            if (spans is not null && spans.Count > 0)
            {
                BuildColoredInlines(tb, rawLine, stripped, commonIndent, spans, defaultFg);
            }
            else
            {
                tb.Text = stripped.Length > 0 ? stripped : "\u00a0";
            }

            Children.Add(tb);
        }
    }

    private static void BuildColoredInlines(
        TextBlock               tb,
        string                  rawLine,
        string                  stripped,
        int                     commonIndent,
        IReadOnlyList<ColoredSpan> spans,
        Brush                   defaultFg)
    {
        int pos = 0;
        foreach (var span in spans)
        {
            // Clamp against rawLine (spans are indexed into the full raw line)
            int s = Math.Clamp(span.Start,              pos,            rawLine.Length);
            int e = Math.Clamp(span.Start + span.Length, s,             rawLine.Length);

            // Gap before this span (subtract commonIndent)
            if (s > pos)
            {
                int gapS = Math.Max(pos,  commonIndent);
                int gapE = Math.Max(s,    commonIndent);
                if (gapE > gapS)
                    tb.Inlines.Add(new Run(rawLine[gapS..gapE]) { Foreground = defaultFg });
            }

            // The span itself
            if (e > s)
            {
                int skip    = Math.Max(0, commonIndent - s);
                var runText = skip < (e - s) ? rawLine[(s + skip)..e] : string.Empty;
                if (runText.Length > 0)
                {
                    var run = new Run(runText) { Foreground = span.Foreground };
                    if (span.IsBold)   run.FontWeight = FontWeights.Bold;
                    if (span.IsItalic) run.FontStyle  = FontStyles.Italic;
                    tb.Inlines.Add(run);
                }
            }

            pos = e;
        }

        // Trailing text after last span
        int trailS = Math.Max(pos, commonIndent);
        if (trailS < rawLine.Length)
            tb.Inlines.Add(new Run(rawLine[trailS..]) { Foreground = defaultFg });

        // Fallback if nothing was added
        if (tb.Inlines.Count == 0)
            tb.Text = stripped.Length > 0 ? stripped : "\u00a0";
    }
}

