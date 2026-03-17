// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/ReferencesPopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Inline "Find All References" popup anchored near the cursor.
//     Displays LSP reference results grouped by file with line numbers
//     and highlighted code snippets — mimics the Visual Studio peek
//     references panel.
//
// Architecture Notes:
//     - Popup-derived (StaysOpen=false, AllowsTransparency=true)
//     - Mirrors IntelliSensePopup architecture pattern
//     - All colors via SetResourceReference / DynamicResource (CE_* / Panel_* tokens)
//     - Fires NavigationRequested; CodeEditor routes cross-file navigation
//       to the host through its own ReferenceNavigationRequested event
//     - Group sections are independently collapsible (VS-like chevron toggle)
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.CodeEditor.Controls
{
    // ── Public data model ──────────────────────────────────────────────────────

    /// <summary>A single reference occurrence within a file.</summary>
    public sealed class ReferenceItem
    {
        /// <summary>0-based line index.</summary>
        public int Line { get; init; }

        /// <summary>0-based column of the symbol start.</summary>
        public int Column { get; init; }

        /// <summary>Raw (untrimmed) source line text; max 200 chars.</summary>
        public string Snippet { get; init; } = string.Empty;
    }

    /// <summary>All references from a single file.</summary>
    public sealed class ReferenceGroup
    {
        /// <summary>Absolute file path.</summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>Short label shown in the header (file name or relative path).</summary>
        public string DisplayLabel { get; init; } = string.Empty;

        /// <summary>Ordered list of reference occurrences in this file.</summary>
        public IReadOnlyList<ReferenceItem> Items { get; init; } = Array.Empty<ReferenceItem>();
    }

    /// <summary>Event args fired when the user clicks a reference entry.</summary>
    public sealed class ReferencesNavigationEventArgs : EventArgs
    {
        public string FilePath { get; init; } = string.Empty;
        public int Line        { get; init; }
        public int Column      { get; init; }
    }

    // ── ReferencesPopup control ────────────────────────────────────────────────

    /// <summary>
    /// Inline popup that lists all LSP reference results grouped by file.
    /// Anchor it to the editor via <see cref="Show"/>.
    /// </summary>
    internal sealed class ReferencesPopup : Popup
    {
        #region Fields

        private StackPanel  _groupsPanel  = null!;
        private TextBlock   _headerLabel  = null!;
        private Button      _collapseAllBtn = null!;
        private Point       _anchor;
        private string      _symbolName   = string.Empty;
        private bool        _allCollapsed;
        private readonly List<(StackPanel itemsPanel, TextBlock chevron)> _groups = new();

        #endregion

        #region Events

        /// <summary>Fired when the user clicks a reference row. Handle to navigate.</summary>
        public event EventHandler<ReferencesNavigationEventArgs>? NavigationRequested;

        #endregion

        #region Constructor

        internal ReferencesPopup()
        {
            StaysOpen          = true;
            AllowsTransparency = true;

            BuildUI();

            PreviewKeyDown += OnPreviewKeyDown;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Populates and shows the popup anchored at <paramref name="anchor"/>
        /// (editor-relative coordinates, below the cursor).
        /// </summary>
        internal void Show(
            CodeEditor              owner,
            IReadOnlyList<ReferenceGroup> groups,
            string                  symbolName,
            Point                   anchor)
        {
            _anchor     = anchor;
            _symbolName = symbolName ?? string.Empty;
            _allCollapsed = false;

            PlacementTarget               = owner;
            Placement                     = PlacementMode.Custom;
            CustomPopupPlacementCallback  = CalculatePlacement;

            PopulateContent(groups);
            IsOpen = true;
        }

        /// <summary>Closes and clears the popup.</summary>
        internal new void Close()
        {
            IsOpen = false;
            _groups.Clear();
            _groupsPanel.Children.Clear();
        }

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            // ── Outer border (shadow + rounded corners) ────────────────────────
            var outerBorder = new Border
            {
                MinWidth        = 520,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Effect          = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = Colors.Black,
                    Opacity     = 0.35,
                    BlurRadius  = 8,
                    ShadowDepth = 3
                }
            };
            outerBorder.SetResourceReference(Border.BackgroundProperty,    "TE_Background");
            outerBorder.SetResourceReference(Border.BorderBrushProperty,   "Panel_ToolbarBorderBrush");

            // ── Root grid: header / body / footer ─────────────────────────────
            // All rows are Auto — star rows collapse to 0 in auto-sized containers.
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── Header row ────────────────────────────────────────────────────
            // BorderThickness bottom=1 draws the separator between header and body
            // without a separate Border element that would occupy the same grid row.
            var header = new Border
            {
                Padding         = new Thickness(10, 6, 8, 6),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            header.SetResourceReference(Border.BackgroundProperty,  "Panel_ToolbarBrush");
            header.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
            Grid.SetRow(header, 0);

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _headerLabel = new TextBlock
            {
                FontWeight  = FontWeights.SemiBold,
                FontSize    = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            _headerLabel.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");
            Grid.SetColumn(_headerLabel, 0);

            var closeBtn = new Button
            {
                Content         = "✕",
                Width           = 20,
                Height          = 20,
                Padding         = new Thickness(0),
                BorderThickness = new Thickness(0),
                FontSize        = 11,
                Cursor          = Cursors.Hand,
                ToolTip         = "Close (Esc)"
            };
            closeBtn.SetResourceReference(Button.BackgroundProperty,   "Panel_ToolbarBrush");
            closeBtn.SetResourceReference(Button.ForegroundProperty,   "TE_Foreground");
            closeBtn.Click += (_, _) => IsOpen = false;
            Grid.SetColumn(closeBtn, 1);

            headerGrid.Children.Add(_headerLabel);
            headerGrid.Children.Add(closeBtn);
            header.Child = headerGrid;

            // ── Body — scrollable groups list ─────────────────────────────────
            _groupsPanel = new StackPanel();
            _groupsPanel.SetResourceReference(StackPanel.BackgroundProperty, "TE_Background");

            var scroll = new ScrollViewer
            {
                MaxHeight                     = 360,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content                       = _groupsPanel
            };
            scroll.SetResourceReference(ScrollViewer.BackgroundProperty, "TE_Background");
            Grid.SetRow(scroll, 1);

            // ── Footer ────────────────────────────────────────────────────────
            var footer = new Border { Padding = new Thickness(8, 4, 8, 4) };
            footer.SetResourceReference(Border.BackgroundProperty,   "Panel_ToolbarBrush");
            footer.SetResourceReference(Border.BorderBrushProperty,  "Panel_ToolbarBorderBrush");
            footer.BorderThickness = new Thickness(0, 1, 0, 0);
            Grid.SetRow(footer, 2);

            _collapseAllBtn = new Button
            {
                Content         = "Réduire tout",
                Padding         = new Thickness(6, 2, 6, 2),
                FontSize        = 11,
                BorderThickness = new Thickness(1),
                Cursor          = Cursors.Hand
            };
            _collapseAllBtn.SetResourceReference(Button.ForegroundProperty,  "TE_Foreground");
            _collapseAllBtn.SetResourceReference(Button.BackgroundProperty,  "Panel_ToolbarBrush");
            _collapseAllBtn.SetResourceReference(Button.BorderBrushProperty, "Panel_ToolbarBorderBrush");
            _collapseAllBtn.Click += OnCollapseAllClicked;
            footer.Child = _collapseAllBtn;

            root.Children.Add(header);
            root.Children.Add(scroll);
            root.Children.Add(footer);

            outerBorder.Child = root;
            Child = outerBorder;
        }

        #endregion

        #region Content Population

        private void PopulateContent(IReadOnlyList<ReferenceGroup> groups)
        {
            _groups.Clear();
            _groupsPanel.Children.Clear();

            int total = 0;
            foreach (var g in groups) total += g.Items.Count;

            _headerLabel.Text = $"{total} référence{(total != 1 ? "s" : "")} — {_symbolName}";
            _allCollapsed     = false;
            _collapseAllBtn.Content = "Réduire tout";

            foreach (var group in groups)
                _groupsPanel.Children.Add(BuildGroupPanel(group));
        }

        private UIElement BuildGroupPanel(ReferenceGroup group)
        {
            var container = new StackPanel();

            // ── Group header: ▼ chevron + file name ───────────────────────────
            var groupHeader = new Border
            {
                Padding    = new Thickness(6, 4, 6, 4),
                Cursor     = Cursors.Hand
            };
            groupHeader.SetResourceReference(Border.BackgroundProperty, "Panel_ToolbarBrush");

            var headerRow = new StackPanel { Orientation = Orientation.Horizontal };

            var chevron = new TextBlock
            {
                Text              = "▼",
                FontSize          = 9,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 6, 0)
            };
            chevron.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");

            var fileLabel = new TextBlock
            {
                Text              = group.DisplayLabel,
                FontWeight        = FontWeights.SemiBold,
                FontSize          = 12,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip           = group.FilePath
            };
            fileLabel.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");

            var countLabel = new TextBlock
            {
                Text              = $" ({group.Items.Count})",
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            countLabel.SetResourceReference(TextBlock.ForegroundProperty, "PFP_SubTextBrush");

            headerRow.Children.Add(chevron);
            headerRow.Children.Add(fileLabel);
            headerRow.Children.Add(countLabel);
            groupHeader.Child = headerRow;

            // ── Items panel (collapsible) ─────────────────────────────────────
            var itemsPanel = new StackPanel();

            foreach (var item in group.Items)
                itemsPanel.Children.Add(BuildReferenceRow(group.FilePath, item));

            // Wire collapse toggle
            groupHeader.MouseLeftButtonDown += (_, _) =>
                ToggleGroup(itemsPanel, chevron);

            _groups.Add((itemsPanel, chevron));

            var sep = new Border { Height = 1 };
            sep.SetResourceReference(Border.BackgroundProperty, "Panel_ToolbarBorderBrush");

            container.Children.Add(groupHeader);
            container.Children.Add(itemsPanel);
            container.Children.Add(sep);

            return container;
        }

        private UIElement BuildReferenceRow(string filePath, ReferenceItem item)
        {
            var row = new Border
            {
                Padding    = new Thickness(26, 3, 8, 3),
                Cursor     = Cursors.Hand
            };
            row.SetResourceReference(Border.BackgroundProperty, "TE_Background");

            // Hover effect
            row.MouseEnter += (_, _) => row.SetResourceReference(
                Border.BackgroundProperty, "Panel_ToolbarBrush");
            row.MouseLeave += (_, _) => row.SetResourceReference(
                Border.BackgroundProperty, "TE_Background");

            var rowContent = new StackPanel { Orientation = Orientation.Horizontal };

            // Line-number badge
            var lineNum = new Border
            {
                Padding         = new Thickness(4, 1, 4, 1),
                Margin          = new Thickness(0, 0, 8, 0),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(2),
                MinWidth        = 32,
                VerticalAlignment = VerticalAlignment.Center
            };
            lineNum.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
            lineNum.SetResourceReference(Border.BackgroundProperty,   "Panel_ToolbarBrush");
            var lineNumText = new TextBlock
            {
                Text      = (item.Line + 1).ToString(),
                FontSize  = 10,
                TextAlignment = TextAlignment.Right
            };
            lineNumText.SetResourceReference(TextBlock.ForegroundProperty, "PFP_SubTextBrush");
            lineNum.Child = lineNumText;

            // Snippet with highlighted symbol
            var snippet = BuildSnippetTextBlock(item.Snippet, _symbolName);
            snippet.FontFamily  = new FontFamily("Consolas");
            snippet.FontSize    = 11;
            snippet.VerticalAlignment = VerticalAlignment.Center;
            snippet.TextTrimming = TextTrimming.CharacterEllipsis;

            rowContent.Children.Add(lineNum);
            rowContent.Children.Add(snippet);
            row.Child = rowContent;

            // Navigate on click
            row.MouseLeftButtonDown += (_, _) =>
                NavigationRequested?.Invoke(this, new ReferencesNavigationEventArgs
                {
                    FilePath = filePath,
                    Line     = item.Line,
                    Column   = item.Column
                });

            return row;
        }

        /// <summary>
        /// Builds a <see cref="TextBlock"/> with the first occurrence of
        /// <paramref name="symbol"/> highlighted in bold/accent colour.
        /// </summary>
        private TextBlock BuildSnippetTextBlock(string snippet, string symbol)
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
            {
                // Try case-insensitive fallback
                idx = snippet.IndexOf(symbol, StringComparison.OrdinalIgnoreCase);
            }

            if (idx < 0)
            {
                tb.Text = snippet;
                return tb;
            }

            // prefix
            if (idx > 0)
            {
                var pre = new Run(snippet[..idx]);
                pre.SetResourceReference(Run.ForegroundProperty, "TE_Foreground");
                tb.Inlines.Add(pre);
            }

            // highlighted symbol
            var match = new Run(snippet.Substring(idx, symbol.Length))
            {
                FontWeight = FontWeights.Bold
            };
            match.SetResourceReference(Run.ForegroundProperty, "CE_Keyword");
            tb.Inlines.Add(match);

            // suffix
            if (idx + symbol.Length < snippet.Length)
            {
                var post = new Run(snippet[(idx + symbol.Length)..]);
                post.SetResourceReference(Run.ForegroundProperty, "TE_Foreground");
                tb.Inlines.Add(post);
            }

            return tb;
        }

        #endregion

        #region Collapse/Expand

        private static void ToggleGroup(StackPanel itemsPanel, TextBlock chevron)
        {
            bool isVisible = itemsPanel.Visibility == Visibility.Visible;
            itemsPanel.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            chevron.Text          = isVisible ? "▶" : "▼";
        }

        private void OnCollapseAllClicked(object sender, RoutedEventArgs e)
        {
            _allCollapsed = !_allCollapsed;
            foreach (var (panel, chevron) in _groups)
            {
                panel.Visibility = _allCollapsed ? Visibility.Collapsed : Visibility.Visible;
                chevron.Text     = _allCollapsed ? "▶" : "▼";
            }
            _collapseAllBtn.Content = _allCollapsed ? "Développer tout" : "Réduire tout";
        }

        #endregion

        #region Popup Placement

        private CustomPopupPlacement[] CalculatePlacement(
            Size popupSize, Size targetSize, Point offset)
        {
            // Position below cursor; clamp to right/bottom edges.
            double x = Math.Min(_anchor.X, Math.Max(0, targetSize.Width  - popupSize.Width  - 8));
            double y = _anchor.Y;

            // If popup would overflow downward, show above the cursor line instead.
            if (y + popupSize.Height > targetSize.Height - 8)
                y = Math.Max(0, _anchor.Y - popupSize.Height - 4);

            return new[] { new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Vertical) };
        }

        #endregion

        #region Keyboard

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                IsOpen    = false;
                e.Handled = true;
            }
        }

        #endregion
    }
}
