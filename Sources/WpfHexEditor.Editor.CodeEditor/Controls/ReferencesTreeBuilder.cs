// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/ReferencesTreeBuilder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Shared, stateless tree-building helpers for ReferencesPopup and
//     FindReferencesPanel.  Produces VS Code–style collapsible reference
//     groups with highlighted symbol snippets.
//
// Architecture Notes:
//     Static utility — no state. Navigation callback injected as Action<>.
//     WPF Theme — all brushes resolved via SetResourceReference so the tree
//     respects the active IDE theme (CE_*, TE_*, Panel_*, PFP_* tokens).
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

internal static class ReferencesTreeBuilder
{
    // ── Frozen brushes ────────────────────────────────────────────────────────

    // VS Code method-purple used for the ◆ reference glyph.
    private static readonly Brush s_glyphBrush =
        MakeFrozenBrush(Color.FromRgb(0xC5, 0x86, 0xC0));

    // Semi-transparent blue for the highlighted symbol span inside a snippet.
    private static readonly Brush s_symbolHighlightBg =
        MakeFrozenBrush(Color.FromArgb(80, 0x20, 0x56, 0xA0));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="StackPanel"/> containing all group sections.
    /// Populates <paramref name="groupHandles"/> so callers can collapse/expand
    /// individual groups or all at once.
    /// </summary>
    internal static StackPanel BuildGroupsPanel(
        IReadOnlyList<ReferenceGroup>                         groups,
        string                                                symbolName,
        Action<ReferencesNavigationEventArgs>                 onNavigate,
        out List<(StackPanel ItemsPanel, TextBlock Chevron)>  groupHandles)
    {
        groupHandles = new List<(StackPanel, TextBlock)>(groups.Count);

        var panel = new StackPanel();
        panel.SetResourceReference(StackPanel.BackgroundProperty, "TE_Background");

        foreach (var group in groups)
        {
            var (groupPanel, handle) = BuildGroupPanel(group, symbolName, onNavigate);
            groupHandles.Add(handle);
            panel.Children.Add(groupPanel);
        }

        return panel;
    }

    /// <summary>
    /// Collapses or expands a group: toggles <paramref name="itemsPanel"/> visibility
    /// and swaps the <paramref name="chevron"/> glyph.
    /// </summary>
    internal static void ToggleGroup(StackPanel itemsPanel, TextBlock chevron)
    {
        bool visible          = itemsPanel.Visibility == Visibility.Visible;
        itemsPanel.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        chevron.Text          = visible ? "▶" : "▼";
    }

    // ── Group panel ───────────────────────────────────────────────────────────

    private static (UIElement Panel, (StackPanel ItemsPanel, TextBlock Chevron) Handle)
        BuildGroupPanel(
            ReferenceGroup                        group,
            string                                symbolName,
            Action<ReferencesNavigationEventArgs> onNavigate)
    {
        var container = new StackPanel();

        string fileName      = Path.GetFileName(group.FilePath);
        if (string.IsNullOrEmpty(fileName)) fileName = group.DisplayLabel;
        string folderDisplay = BuildCompactFolderPath(
            Path.GetDirectoryName(group.FilePath) ?? string.Empty);

        // ── Group header: left accent stripe + path + count ───────────────────
        var groupHeader = new Border
        {
            Padding         = new Thickness(10, 5, 10, 5),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Cursor          = Cursors.Hand
        };
        groupHeader.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
        groupHeader.SetResourceReference(Border.BorderBrushProperty, "CE_Keyword");

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal };

        var chevron = new TextBlock
        {
            Text              = "▼",
            FontSize          = 9,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 6, 0)
        };
        chevron.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");

        var pathBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip           = group.FilePath,
            TextTrimming      = TextTrimming.CharacterEllipsis,
            MaxWidth          = 580
        };
        if (!string.IsNullOrEmpty(folderDisplay))
        {
            var folderRun = new Run(folderDisplay);
            folderRun.SetResourceReference(Run.ForegroundProperty, "PFP_SubTextBrush");
            pathBlock.Inlines.Add(folderRun);
        }
        var fileRun = new Run(fileName) { FontWeight = FontWeights.Bold, FontSize = 12 };
        fileRun.SetResourceReference(Run.ForegroundProperty, "TE_Foreground");
        pathBlock.Inlines.Add(fileRun);

        var countTb = new TextBlock
        {
            Text              = $"  ({group.Items.Count})",
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        countTb.SetResourceReference(TextBlock.ForegroundProperty, "PFP_SubTextBrush");

        headerRow.Children.Add(chevron);
        headerRow.Children.Add(pathBlock);
        headerRow.Children.Add(countTb);
        groupHeader.Child = headerRow;

        // ── Reference rows (collapsible) ──────────────────────────────────────
        var itemsPanel = new StackPanel();
        foreach (var item in group.Items)
            itemsPanel.Children.Add(BuildReferenceRow(group.FilePath, item, symbolName, onNavigate));

        groupHeader.MouseLeftButtonDown += (_, _) => ToggleGroup(itemsPanel, chevron);

        var sep = new Border { Height = 1 };
        sep.SetResourceReference(Border.BackgroundProperty, "Panel_ToolbarBorderBrush");

        container.Children.Add(groupHeader);
        container.Children.Add(itemsPanel);
        container.Children.Add(sep);

        return (container, (itemsPanel, chevron));
    }

    // ── Reference row ─────────────────────────────────────────────────────────

    private static UIElement BuildReferenceRow(
        string                                filePath,
        ReferenceItem                         item,
        string                                symbolName,
        Action<ReferencesNavigationEventArgs> onNavigate)
    {
        var row = new Border
        {
            Padding = new Thickness(12, 3, 8, 3),
            Cursor  = Cursors.Hand
        };
        row.SetResourceReference(Border.BackgroundProperty, "TE_Background");
        row.MouseEnter += (_, _) => row.SetResourceReference(
            Border.BackgroundProperty, "Panel_ToolbarButtonHoverBrush");
        row.MouseLeave += (_, _) => row.SetResourceReference(
            Border.BackgroundProperty, "TE_Background");

        var rowContent = new DockPanel { LastChildFill = true };

        var icon = new TextBlock
        {
            Text              = "◆",
            FontSize          = 9,
            Margin            = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground        = s_glyphBrush
        };
        DockPanel.SetDock(icon, Dock.Left);

        var lineNumTb = new TextBlock
        {
            Text              = $"{item.Line + 1} : ",
            FontFamily        = new FontFamily("Consolas"),
            FontSize          = 11,
            MinWidth          = 52,
            TextAlignment     = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 4, 0)
        };
        lineNumTb.SetResourceReference(TextBlock.ForegroundProperty, "PFP_SubTextBrush");
        DockPanel.SetDock(lineNumTb, Dock.Left);

        var snippetTb              = BuildSnippetTextBlock(item.Snippet, symbolName);
        snippetTb.FontFamily       = new FontFamily("Consolas");
        snippetTb.FontSize         = 11;
        snippetTb.VerticalAlignment = VerticalAlignment.Center;
        snippetTb.TextTrimming     = TextTrimming.CharacterEllipsis;

        rowContent.Children.Add(icon);
        rowContent.Children.Add(lineNumTb);
        rowContent.Children.Add(snippetTb);
        row.Child = rowContent;

        row.MouseLeftButtonDown += (_, _) =>
            onNavigate(new ReferencesNavigationEventArgs
            {
                FilePath = filePath,
                Line     = item.Line,
                Column   = item.Column
            });

        return row;
    }

    // ── Snippet TextBlock with highlighted symbol ─────────────────────────────

    private static TextBlock BuildSnippetTextBlock(string snippet, string symbol)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.NoWrap };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");

        if (string.IsNullOrEmpty(snippet) || string.IsNullOrEmpty(symbol))
        {
            tb.Text = snippet;
            return tb;
        }

        int idx = snippet.IndexOf(symbol, StringComparison.Ordinal);
        if (idx < 0)
            idx = snippet.IndexOf(symbol, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) { tb.Text = snippet; return tb; }

        if (idx > 0)
        {
            var pre = new Run(snippet[..idx]);
            pre.SetResourceReference(Run.ForegroundProperty, "TE_Foreground");
            tb.Inlines.Add(pre);
        }

        var match = new Run(snippet.Substring(idx, symbol.Length))
        {
            FontWeight = FontWeights.Bold,
            Background = s_symbolHighlightBg
        };
        match.SetResourceReference(Run.ForegroundProperty, "CE_Keyword");
        tb.Inlines.Add(match);

        if (idx + symbol.Length < snippet.Length)
        {
            var post = new Run(snippet[(idx + symbol.Length)..]);
            post.SetResourceReference(Run.ForegroundProperty, "TE_Foreground");
            tb.Inlines.Add(post);
        }

        return tb;
    }

    // ── Path helper ───────────────────────────────────────────────────────────

    private static string BuildCompactFolderPath(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return string.Empty;
        folder = folder.Replace('/', '\\').TrimEnd('\\');
        var parts = folder.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty
             : parts.Length > 3  ? $"…\\{string.Join("\\", parts[^3..])} \\"
             : folder + "\\";
    }

    private static Brush MakeFrozenBrush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }
}
