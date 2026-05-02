// ==========================================================
// Project: WpfHexEditor.Editor.MarkdownEditor
// File: Panels/MarkdownOutlinePanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-22
// Description:
//     Dockable panel that displays an H1–H6 heading outline for the
//     active Markdown document. Clicking a heading navigates the
//     source editor to the corresponding line number.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to MarkdownEditorHost.ContentChanged.
//     Parsing is done off the UI thread (Task.Run) with 400ms debounce.
//     Uses SE_* theme tokens to stay consistent with Solution Explorer.
// ==========================================================

using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.MarkdownEditor.Controls;
using WpfHexEditor.Editor.MarkdownEditor.Properties;

namespace WpfHexEditor.Editor.MarkdownEditor.Panels;

// ── View-model ─────────────────────────────────────────────────────────────────

/// <summary>Represents a single Markdown heading for display in the outline.</summary>
public sealed class MdHeadingNode
{
    /// <summary>Heading level 1–6.</summary>
    public int Level { get; init; }

    /// <summary>Heading text (without the # prefix).</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>1-based line number in the source.</summary>
    public int Line { get; init; }

    /// <summary>Indentation padding for visual nesting (12 px per level beyond H1).</summary>
    public Thickness LeftPadding => new(4 + (Level - 1) * 12, 2, 4, 2);

    /// <summary>Segoe MDL2 icon that reflects the heading level.</summary>
    public string LevelIcon => Level switch
    {
        1 => "\uE8C9", // Header1-like: full paragraph
        2 => "\uE8CA",
        _ => "\uE8CB",
    };

    /// <summary>Subtle accent colour per heading depth.</summary>
    public Brush LevelBrush => Level switch
    {
        1 => new SolidColorBrush(Color.FromRgb(0x4F, 0xC1, 0xFF)),  // blue  H1
        2 => new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)),  // light H2
        3 => new SolidColorBrush(Color.FromRgb(0xB8, 0xD7, 0xA3)),  // green H3
        _ => new SolidColorBrush(Color.FromRgb(0x9B, 0x9B, 0x9B)),  // grey  H4+
    };
}

// ── Panel ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Dockable outline panel for the active Markdown document.
/// Call <see cref="SetEditor"/> whenever the active document changes.
/// </summary>
public sealed partial class MarkdownOutlinePanel : UserControl
{
    // ── State ─────────────────────────────────────────────────────────────────

    private MarkdownEditorHost? _editor;
    private readonly ObservableCollection<MdHeadingNode> _headings = [];
    private readonly DispatcherTimer _debounce;

    // Heading regex: matches ATX headings (# … ######) at the start of a line.
    // Captures: group 1 = hashes, group 2 = heading text.
    private static readonly Regex HeadingRegex =
        new(@"^(#{1,6})\s+(.+?)(?:\s+#+)?\s*$",
            RegexOptions.Multiline | RegexOptions.Compiled);

    // ── Constructor ───────────────────────────────────────────────────────────

    public MarkdownOutlinePanel()
    {
        InitializeComponent();

        HeadingList.ItemsSource = _headings;

        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        _debounce.Tick += OnDebounceTick;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects the panel to a Markdown editor. Pass <see langword="null"/> to
    /// show the empty-state overlay (no active Markdown file).
    /// </summary>
    public void SetEditor(MarkdownEditorHost? editor)
    {
        if (_editor is not null)
            _editor.ContentChanged -= OnContentChanged;

        _editor = editor;

        if (_editor is not null)
            _editor.ContentChanged += OnContentChanged;

        ScheduleRefresh();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnContentChanged(object? sender, EventArgs e) => ScheduleRefresh();

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _debounce.Stop();
        _ = RefreshOutlineAsync();
    }

    private void OnHeadingClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is MdHeadingNode node)
            _editor?.NavigateTo(node.Line, 0);
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        _ = RefreshOutlineAsync();
    }

    // ── Outline building ──────────────────────────────────────────────────────

    private void ScheduleRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private async Task RefreshOutlineAsync()
    {
        if (_editor is null)
        {
            _headings.Clear();
            EmptyLabel.Visibility  = Visibility.Visible;
            HeadingList.Visibility = Visibility.Collapsed;
            StatusLabel.Text       = string.Empty;
            return;
        }

        var text = _editor.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            _headings.Clear();
            EmptyLabel.Visibility  = Visibility.Collapsed;
            HeadingList.Visibility = Visibility.Visible;
            StatusLabel.Text       = MarkdownEditorResources.Md_NoHeadingsFound;
            return;
        }

        // Parse off the UI thread — may be called on large files.
        var nodes = await Task.Run(() => ParseHeadings(text));

        // Update collection on UI thread.
        _headings.Clear();
        foreach (var n in nodes)
            _headings.Add(n);

        bool hasItems = _headings.Count > 0;
        EmptyLabel.Visibility  = hasItems ? Visibility.Collapsed : Visibility.Visible;
        HeadingList.Visibility = hasItems ? Visibility.Visible   : Visibility.Collapsed;
        StatusLabel.Text       = hasItems
            ? $"{_headings.Count} heading{(_headings.Count == 1 ? "" : "s")}"
            : "No headings found.";
    }

    private static readonly Regex _yamlFrontmatterRegex =
        new(@"^---\r?\n(.*?)\r?\n---\r?\n", RegexOptions.Singleline | RegexOptions.Compiled);

    private static List<MdHeadingNode> ParseHeadings(string text)
    {
        var result = new List<MdHeadingNode>();
        var lines  = text.Split('\n');

        // Determine how many lines are occupied by YAML front-matter (skip them)
        int yamlEndLine = 0;
        var yamlMatch = _yamlFrontmatterRegex.Match(text);
        if (yamlMatch.Success)
            yamlEndLine = yamlMatch.Value.Split('\n').Length - 1;

        bool inFenceBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            // Skip YAML front-matter lines
            if (i < yamlEndLine) continue;

            var raw = lines[i].TrimEnd('\r');

            // Skip content inside fenced code blocks (```).
            if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFenceBlock = !inFenceBlock;
                continue;
            }
            if (inFenceBlock) continue;

            var m = HeadingRegex.Match(raw);
            if (!m.Success) continue;

            result.Add(new MdHeadingNode
            {
                Level = m.Groups[1].Length,
                Text  = m.Groups[2].Value.Trim(),
                Line  = i + 1,  // 1-based
            });
        }

        return result;
    }
}
