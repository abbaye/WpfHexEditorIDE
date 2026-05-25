//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.Editor.DiffViewer
// File: Controls/DiffViewer.TextMode.cs
// Description: Text-diff mode — side-by-side line rendering with intra-line
//              word-level highlighting via TextDiffResult.WordEdits.
//              Export to unified .patch format via DiffExportService.
//////////////////////////////////////////////

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using WpfHexEditor.Core.Diff.Algorithms;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Core.Diff.Services;
using WpfHexEditor.Editor.DiffViewer.Properties;

namespace WpfHexEditor.Editor.DiffViewer.Controls;

public sealed partial class DiffViewer
{
    // ── Fields ───────────────────────────────────────────────────────────────

    private TextDiffResult? _textResult;
    // Parallel left/right line lists (null slot = blank placeholder for unpaired side)
    private List<TextDiffLine?> _textLeftLines  = [];
    private List<TextDiffLine?> _textRightLines = [];
    private bool _syncingScroll;

    // ── Brushes (frozen, allocated once) ─────────────────────────────────────

    private static readonly Brush BrushTextModifiedBg  = Freeze(Color.FromArgb(60, 200, 180, 0));
    private static readonly Brush BrushTextDeletedBg   = Freeze(Color.FromArgb(60, 180, 40,  40));
    private static readonly Brush BrushTextInsertedBg  = Freeze(Color.FromArgb(60, 40,  160, 40));
    private static readonly Brush BrushWordHighlight   = Freeze(Color.FromArgb(140, 255, 200, 0));
    private static readonly Brush BrushWordDeleted     = Freeze(Color.FromArgb(140, 220, 60,  60));
    private static readonly Brush BrushWordInserted    = Freeze(Color.FromArgb(140, 60,  200, 60));
    private static readonly Brush BrushLineNumber      = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));

    private static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    // ── Tab switch ───────────────────────────────────────────────────────────

    private void OnViewTabChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isTextTab = ViewTabControl.SelectedIndex == 1;
        BtnExportPatch.IsEnabled = isTextTab && _textResult is not null;

        if (isTextTab && _textResult is null && _left is not null && _right is not null)
            _ = ComputeTextDiffAsync();

        UpdateMergeToolbarVisibility();
    }

    // ── Text diff computation ─────────────────────────────────────────────────

    private async Task ComputeTextDiffAsync()
    {
        if (_left is null || _right is null) return;

        TextLeftHeader.Text  = System.IO.Path.GetFileName(_leftPath);
        TextRightHeader.Text = System.IO.Path.GetFileName(_rightPath);

        var leftBytes  = _left;
        var rightBytes = _right;

        _textResult = await Task.Run(() =>
        {
            if (!IsLikelyText(leftBytes) || !IsLikelyText(rightBytes))
                return null;

            var leftLines  = DecodeLines(leftBytes);
            var rightLines = DecodeLines(rightBytes);

            return new MyersDiffAlgorithm().ComputeLines(leftLines, rightLines);
        });

        if (_textResult is null)
        {
            TextLeftList.ItemsSource  = null;
            TextRightList.ItemsSource = null;
            StatusText.Text = DiffViewerResources.DiffViewer_NotTextFile;
            return;
        }

        BuildSideBySideLists(_textResult);
        RenderTextLists();
        BtnExportPatch.IsEnabled = true;
    }

    // ── Build parallel left/right line lists ──────────────────────────────────

    private void BuildSideBySideLists(TextDiffResult result)
    {
        _textLeftLines  = [];
        _textRightLines = [];

        int i = 0;
        var lines = result.Lines;
        while (i < lines.Count)
        {
            var line = lines[i];
            switch (line.Kind)
            {
                case TextLineKind.Equal:
                    _textLeftLines.Add(line);
                    _textRightLines.Add(line);
                    i++;
                    break;

                case TextLineKind.Modified:
                    // Modified pairs: del line followed by ins line
                    var delLine = line;
                    var insLine = i + 1 < lines.Count && lines[i + 1].Kind == TextLineKind.Modified
                        ? lines[i + 1] : null;
                    _textLeftLines.Add(delLine);
                    _textRightLines.Add(insLine);
                    i += insLine is not null ? 2 : 1;
                    break;

                case TextLineKind.DeletedLeft:
                    _textLeftLines.Add(line);
                    _textRightLines.Add(null);
                    i++;
                    break;

                case TextLineKind.InsertedRight:
                    _textLeftLines.Add(null);
                    _textRightLines.Add(line);
                    i++;
                    break;

                default:
                    i++;
                    break;
            }
        }
    }

    // ── Render ────────────────────────────────────────────────────────────────

    private void RenderTextLists()
    {
        TextLeftList.ItemsSource  = _textLeftLines .Select((l, i) => BuildLineElement(l, i, isLeft: true) ).ToList();
        TextRightList.ItemsSource = _textRightLines.Select((l, i) => BuildLineElement(l, i, isLeft: false)).ToList();
    }

    private UIElement BuildLineElement(TextDiffLine? line, int rowIndex, bool isLeft)
    {
        var grid = new Grid { Height = 18 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Background
        Brush? bg = line?.Kind switch
        {
            TextLineKind.Modified      => BrushTextModifiedBg,
            TextLineKind.DeletedLeft   => BrushTextDeletedBg,
            TextLineKind.InsertedRight => BrushTextInsertedBg,
            _                          => null
        };
        if (bg is not null)
            grid.Background = bg;

        // Line number
        var lineNum = new TextBlock
        {
            Text              = line?.LeftLineNumber?.ToString() ?? line?.RightLineNumber?.ToString() ?? string.Empty,
            Foreground        = BrushLineNumber,
            FontFamily        = new FontFamily("Consolas"),
            FontSize          = 11,
            Padding           = new Thickness(4, 0, 4, 0),
            TextAlignment     = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(lineNum, 0);
        grid.Children.Add(lineNum);

        // Content — inline with word-level highlights when WordEdits available
        FrameworkElement content = BuildContentElement(line, isLeft);
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);

        return grid;
    }

    private FrameworkElement BuildContentElement(TextDiffLine? line, bool isLeft)
    {
        if (line is null)
        {
            return new TextBlock
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
                Height     = 18
            };
        }

        // Use TextBlock with Run inlines when word edits exist (Modified lines only)
        if (line.Kind == TextLineKind.Modified && line.WordEdits.Count > 0)
            return BuildInlineTextBlock(line, isLeft);

        var tb = new TextBlock
        {
            Text              = line.Content,
            FontFamily        = new FontFamily("Consolas"),
            FontSize          = 11,
            Foreground        = SystemColors.ControlTextBrush,
            Padding           = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis,
        };
        return tb;
    }

    private TextBlock BuildInlineTextBlock(TextDiffLine line, bool isLeft)
    {
        var tb = new TextBlock
        {
            FontFamily        = new FontFamily("Consolas"),
            FontSize          = 11,
            Foreground        = SystemColors.ControlTextBrush,
            Padding           = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis,
        };

        string text   = line.Content;
        int    cursor = 0;

        foreach (var edit in line.WordEdits)
        {
            int start = isLeft ? edit.LeftStart  : edit.RightStart;
            int end   = isLeft ? edit.LeftEnd    : edit.RightEnd;

            if (start < 0 || end <= start) continue;

            // Text before this edit
            if (start > cursor && cursor < text.Length)
                tb.Inlines.Add(new Run(text[cursor..Math.Min(start, text.Length)]));

            if (start >= text.Length) { cursor = end; continue; }

            end = Math.Min(end, text.Length);
            Brush hlBrush = edit.Kind switch
            {
                EditKind.Delete => BrushWordDeleted,
                EditKind.Insert => BrushWordInserted,
                _               => BrushWordHighlight,
            };

            tb.Inlines.Add(new Run(text[start..end]) { Background = hlBrush });
            cursor = end;
        }

        if (cursor < text.Length)
            tb.Inlines.Add(new Run(text[cursor..]));

        return tb;
    }

    // ── Synchronized scroll ───────────────────────────────────────────────────

    private void OnTextScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingScroll) return;
        _syncingScroll = true;
        try
        {
            var other = ReferenceEquals(sender, TextLeftScroll) ? TextRightScroll : TextLeftScroll;
            other.ScrollToVerticalOffset(e.VerticalOffset);
        }
        finally { _syncingScroll = false; }
    }

    // ── Export .patch ─────────────────────────────────────────────────────────

    private void BtnExportPatch_Click(object sender, RoutedEventArgs e)
    {
        if (_textResult is null) return;

        var dlg = new SaveFileDialog
        {
            Title      = DiffViewerResources.DiffViewer_ExportPatch,
            Filter     = "Patch files (*.patch)|*.patch|All files (*.*)|*.*",
            DefaultExt = ".patch",
            FileName   = System.IO.Path.GetFileNameWithoutExtension(_leftPath) + ".patch",
        };
        if (dlg.ShowDialog() != true) return;

        var patch = new DiffExportService().ExportUnifiedPatch(_textResult, _leftPath, _rightPath);
        File.WriteAllText(dlg.FileName, patch, Encoding.UTF8);
        StatusText.Text = string.Format(DiffViewerResources.DiffViewer_PatchSaved, dlg.FileName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string[] DecodeLines(byte[] data)
        => DetectEncoding(data).GetString(data).ReplaceLineEndings("\n").Split('\n');

    private static bool IsLikelyText(byte[] data)
    {
        if (data.Length == 0) return true;
        // BOM check
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return true;
        if (data.Length >= 2 && ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))) return true;
        // Null-byte heuristic: more than 1 null in first 512 bytes → likely binary
        int sample = Math.Min(data.Length, 512);
        int nulls  = 0;
        for (int i = 0; i < sample; i++)
            if (data[i] == 0) nulls++;
        return nulls <= 1;
    }

    private static Encoding DetectEncoding(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return Encoding.Unicode;
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return Encoding.BigEndianUnicode;
        return new UTF8Encoding(false);
    }
}
