//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.Editor.DiffViewer
// File: Controls/DiffViewer.MergeMode.cs
// Description: 3-way merge mode — side-by-side ours/base/theirs with
//              conflict resolution (accept ours / theirs / both) and
//              save-merged-output to file.
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Core.Diff.Services;
using WpfHexEditor.Editor.Core.Dialogs;
using WpfHexEditor.Editor.DiffViewer.Properties;

namespace WpfHexEditor.Editor.DiffViewer.Controls;

public sealed partial class DiffViewer
{
    // ── Fields ───────────────────────────────────────────────────────────────

    private byte[]?              _base;
    private string               _basePath      = string.Empty;
    private string[]?            _baseLinesCache;
    private ThreeWayMergeResult? _mergeResult;
    private int                  _currentConflictIndex = -1;
    private bool                 _syncingMergeScroll;

    // ── Brushes (frozen) ──────────────────────────────────────────────────────

    private static readonly Brush BrushMergeConflictOursBg   = Freeze(Color.FromArgb(60, 180, 40,  40));
    private static readonly Brush BrushMergeConflictTheirsBg = Freeze(Color.FromArgb(60, 40,  100, 200));
    private static readonly Brush BrushMergeAcceptedOursBg   = Freeze(Color.FromArgb(40, 40,  160, 40));
    private static readonly Brush BrushMergeAcceptedTheirsBg = Freeze(Color.FromArgb(40, 40,  100, 200));
    private static readonly Brush BrushMergeResolvedBg       = Freeze(Color.FromArgb(40, 160, 220, 80));

    // ── Tab visibility ────────────────────────────────────────────────────────

    private void UpdateMergeToolbarVisibility()
    {
        bool isMergeTab = ViewTabControl.SelectedIndex == 2;
        var visibility  = isMergeTab ? Visibility.Visible : Visibility.Collapsed;
        MergeToolbarSep.Visibility = BtnOpenBase.Visibility     = visibility;
        BtnPrevConflict.Visibility = BtnNextConflict.Visibility = visibility;
        ChipConflicts.Visibility   = BtnSaveMerged.Visibility   = visibility;

        if (isMergeTab && _mergeResult is null && _left is not null && _right is not null && _base is not null)
            ComputeMerge();
    }

    // ── Open base file ────────────────────────────────────────────────────────

    private void BtnOpenBase_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = DiffViewerResources.DiffViewer_MergeOpenBase };
        if (dlg.ShowDialog() != true) return;

        _basePath        = dlg.FileName;
        _base            = File.ReadAllBytes(_basePath);
        _baseLinesCache  = null;
        _mergeResult     = null;

        MergeBaseHeader.Text = System.IO.Path.GetFileName(_basePath);
        ComputeMerge();
    }

    // ── Merge computation ─────────────────────────────────────────────────────

    private void ComputeMerge()
    {
        if (_left is null || _right is null || _base is null) return;

        MergeOursHeader.Text   = System.IO.Path.GetFileName(_leftPath);
        MergeTheirsHeader.Text = System.IO.Path.GetFileName(_rightPath);

        if (!IsLikelyText(_left) || !IsLikelyText(_right) || !IsLikelyText(_base))
        {
            StatusText.Text = DiffViewerResources.DiffViewer_NotTextFile;
            return;
        }

        var baseLines   = _baseLinesCache = DecodeLines(_base);
        var oursLines   = DecodeLines(_left);
        var theirsLines = DecodeLines(_right);

        _mergeResult          = new ThreeWayMergeEngine().Merge(baseLines, oursLines, theirsLines);
        _currentConflictIndex = -1;

        RenderMergeLists();
        UpdateConflictChip();
        UpdateMergeButtons();

        StatusText.Text = _mergeResult.Conflicts.Count == 0
            ? DiffViewerResources.DiffViewer_MergeNoConflicts
            : string.Format(DiffViewerResources.DiffViewer_MergeConflicts, _mergeResult.Conflicts.Count);
    }

    // ── Render ────────────────────────────────────────────────────────────────

    private void RenderMergeLists()
    {
        if (_mergeResult is null) return;

        var oursItems   = new List<UIElement>();
        var baseItems   = new List<UIElement>();
        var theirsItems = new List<UIElement>();

        var baseLines = _baseLinesCache;

        foreach (var line in _mergeResult.Lines)
        {
            Brush? oursBg = line.Kind switch
            {
                MergeLineKind.ConflictOurs   => BrushMergeConflictOursBg,
                MergeLineKind.AcceptedOurs   => BrushMergeAcceptedOursBg,
                MergeLineKind.Resolved       => BrushMergeResolvedBg,
                _ => null
            };
            Brush? theirsBg = line.Kind switch
            {
                MergeLineKind.ConflictTheirs  => BrushMergeConflictTheirsBg,
                MergeLineKind.AcceptedTheirs  => BrushMergeAcceptedTheirsBg,
                MergeLineKind.Resolved        => BrushMergeResolvedBg,
                _ => null
            };

            string oursContent   = line.Kind is MergeLineKind.ConflictOurs or MergeLineKind.AcceptedOurs
                                               or MergeLineKind.Equal or MergeLineKind.Resolved
                ? line.Content : string.Empty;
            string theirsContent = line.Kind is MergeLineKind.ConflictTheirs or MergeLineKind.AcceptedTheirs
                                               or MergeLineKind.Equal or MergeLineKind.Resolved
                ? line.Content : string.Empty;
            string baseContent   = baseLines is not null && line.BaseLineNumber is { } bn && bn <= baseLines.Length
                ? baseLines[bn - 1] : string.Empty;

            oursItems.Add(BuildMergeLineElement(oursContent, oursBg, line.OursLineNumber));
            theirsItems.Add(BuildMergeLineElement(theirsContent, theirsBg, line.TheirsLineNumber));
            baseItems.Add(BuildMergeLineElement(baseContent, null, line.BaseLineNumber));

            if (line.Kind == MergeLineKind.ConflictOurs && line.ConflictIndex >= 0)
                AddConflictButtons(oursItems, line.ConflictIndex);
        }

        MergeOursList.ItemsSource   = oursItems;
        MergeBaseList.ItemsSource   = baseItems;
        MergeTheirsList.ItemsSource = theirsItems;
    }

    private static UIElement BuildMergeLineElement(string content, Brush? bg, int? lineNum)
    {
        var grid = new Grid { Height = 18 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (bg is not null) grid.Background = bg;

        var lineNumTb = new TextBlock
        {
            Text              = lineNum?.ToString() ?? string.Empty,
            Foreground        = BrushLineNumber,
            FontFamily        = new FontFamily("Consolas"),
            FontSize          = 11,
            Padding           = new Thickness(4, 0, 4, 0),
            TextAlignment     = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(lineNumTb, 0);
        grid.Children.Add(lineNumTb);

        FrameworkElement contentEl = string.IsNullOrEmpty(content)
            ? new TextBlock { Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)), Height = 18 }
            : new TextBlock
            {
                Text              = content,
                FontFamily        = new FontFamily("Consolas"),
                FontSize          = 11,
                Foreground        = SystemColors.ControlTextBrush,
                Padding           = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming      = TextTrimming.CharacterEllipsis,
            };
        Grid.SetColumn(contentEl, 1);
        grid.Children.Add(contentEl);
        return grid;
    }

    private void AddConflictButtons(List<UIElement> target, int conflictIndex)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(44, 2, 0, 2) };

        var btnOurs = new Button { Content = DiffViewerResources.DiffViewer_MergeAcceptOurs, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0), FontSize = 11 };
        btnOurs.Click += (_, _) => ResolveConflict(conflictIndex, ConflictResolution.AcceptOurs);

        var btnTheirs = new Button { Content = DiffViewerResources.DiffViewer_MergeAcceptTheirs, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0), FontSize = 11 };
        btnTheirs.Click += (_, _) => ResolveConflict(conflictIndex, ConflictResolution.AcceptTheirs);

        var btnBoth = new Button { Content = DiffViewerResources.DiffViewer_MergeAcceptBoth, Padding = new Thickness(8, 2, 8, 2), FontSize = 11 };
        btnBoth.Click += (_, _) => ResolveConflict(conflictIndex, ConflictResolution.AcceptBoth);

        panel.Children.Add(btnOurs);
        panel.Children.Add(btnTheirs);
        panel.Children.Add(btnBoth);
        target.Add(panel);
    }

    // ── Conflict resolution ───────────────────────────────────────────────────

    private void ResolveConflict(int index, ConflictResolution resolution)
    {
        if (_mergeResult is null || index < 0 || index >= _mergeResult.Conflicts.Count) return;
        _mergeResult.Conflicts[index].Resolution = resolution;
        _currentConflictIndex = Math.Min(_currentConflictIndex, _mergeResult.UnresolvedCount - 1);
        RenderMergeLists();
        UpdateConflictChip();
        UpdateMergeButtons();
    }

    private void UpdateConflictChip()
    {
        if (_mergeResult is null) return;
        ConflictsText.Text       = string.Format(DiffViewerResources.DiffViewer_MergeConflicts, _mergeResult.UnresolvedCount);
        ChipConflicts.Visibility = _mergeResult.Conflicts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateMergeButtons()
    {
        bool hasConflicts         = _mergeResult?.Conflicts.Count > 0;
        BtnPrevConflict.IsEnabled = hasConflicts;
        BtnNextConflict.IsEnabled = hasConflicts;
        BtnSaveMerged.IsEnabled   = _mergeResult is not null;
    }

    // ── Conflict navigation ───────────────────────────────────────────────────

    private void BtnPrevConflict_Click(object sender, RoutedEventArgs e) => NavigateConflict(-1);
    private void BtnNextConflict_Click(object sender, RoutedEventArgs e) => NavigateConflict(+1);

    private void NavigateConflict(int direction)
    {
        if (_mergeResult is null || _mergeResult.Conflicts.Count == 0) return;
        _currentConflictIndex = (_currentConflictIndex + direction + _mergeResult.Conflicts.Count)
                                % _mergeResult.Conflicts.Count;
        ScrollToConflict(_currentConflictIndex);
    }

    private void ScrollToConflict(int conflictIndex)
    {
        if (_mergeResult is null) return;
        double offset = _mergeResult.Conflicts[conflictIndex].Index * 18.0;
        MergeOursScroll.ScrollToVerticalOffset(offset);
        MergeBaseScroll.ScrollToVerticalOffset(offset);
        MergeTheirsScroll.ScrollToVerticalOffset(offset);
    }

    // ── Synchronized scroll ───────────────────────────────────────────────────

    private void OnMergeScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingMergeScroll) return;
        _syncingMergeScroll = true;
        try
        {
            var scrollers = new[] { MergeOursScroll, MergeBaseScroll, MergeTheirsScroll };
            foreach (var s in scrollers)
                if (!ReferenceEquals(s, sender))
                    s.ScrollToVerticalOffset(e.VerticalOffset);
        }
        finally { _syncingMergeScroll = false; }
    }

    // ── Save merged output ────────────────────────────────────────────────────

    private void BtnSaveMerged_Click(object sender, RoutedEventArgs e)
    {
        if (_mergeResult is null) return;

        if (!_mergeResult.IsFullyResolved)
        {
            var msg = string.Format(DiffViewerResources.DiffViewer_MergeUnresolvedWarning, _mergeResult.UnresolvedCount);
            if (IdeMessageBox.Show(msg, DiffViewerResources.DiffViewer_MergeSave,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
        }

        var dlg = new SaveFileDialog
        {
            Title    = DiffViewerResources.DiffViewer_MergeSave,
            Filter   = "All files (*.*)|*.*",
            FileName = System.IO.Path.GetFileNameWithoutExtension(_leftPath) + ".merged" +
                       System.IO.Path.GetExtension(_leftPath),
        };
        if (dlg.ShowDialog() != true) return;

        File.WriteAllText(dlg.FileName, _mergeResult.BuildOutput(), DetectEncoding(_left ?? []));
        StatusText.Text = string.Format(DiffViewerResources.DiffViewer_MergeSaved, dlg.FileName);
    }
}
