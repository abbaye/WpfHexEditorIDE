// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/ReferencesPopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Inline "Find All References" popup anchored near the cursor.
//     Displays results grouped by file with VS Code–style collapsible
//     sections: folder path (dimmed) + bold filename + (count),
//     reference rows with icon + line number + highlighted snippet.
//
// Architecture Notes:
//     - Popup-derived (StaysOpen=true — explicit dismiss via Escape or
//       CodeEditor.OnMouseDown closing it on any click)
//     - AllowsTransparency=true for drop-shadow effect
//     - Tree rendering delegated to ReferencesTreeBuilder (shared with
//       FindReferencesPanel)
//     - Fires NavigationRequested, RefreshRequested, PinRequested
//     - Group sections are independently collapsible (chevron toggle)
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WpfHexEditor.Editor.CodeEditor.Controls
{
    // ── Public data model ──────────────────────────────────────────────────────

    /// <summary>A single reference occurrence within a file.</summary>
    public sealed class ReferenceItem
    {
        /// <summary>0-based line index.</summary>
        public int Line { get; init; }

        /// <summary>0-based column of the symbol start in the original (non-trimmed) line.</summary>
        public int Column { get; init; }

        /// <summary>Source line text (may be TrimStart'd); max 200 chars.</summary>
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
        public int    Line     { get; init; }
        public int    Column   { get; init; }
    }

    /// <summary>Event args for the pin-to-dock request.</summary>
    public sealed class FindAllReferencesDockEventArgs : EventArgs
    {
        public IReadOnlyList<ReferenceGroup> Groups     { get; init; } = Array.Empty<ReferenceGroup>();
        public string                        SymbolName { get; init; } = string.Empty;
    }

    // ── ReferencesPopup control ────────────────────────────────────────────────

    /// <summary>
    /// Floating popup listing "Find All References" results grouped by file.
    /// </summary>
    internal sealed class ReferencesPopup : Popup
    {
        #region Fields

        private ScrollViewer _scroll       = null!;
        private TextBlock    _collapseLink = null!;
        private Point        _anchor;
        private string       _symbolName   = string.Empty;
        private bool         _allCollapsed;

        private List<(StackPanel ItemsPanel, TextBlock Chevron)> _groups = new();

        #endregion

        #region Events

        /// <summary>Fired when the user clicks a reference row.</summary>
        public event EventHandler<ReferencesNavigationEventArgs>? NavigationRequested;

        /// <summary>Fired when "Actualiser" is clicked — caller should re-run the search.</summary>
        public event EventHandler? RefreshRequested;

        /// <summary>Fired when the pin button is clicked — caller should dock results.</summary>
        public event EventHandler? PinRequested;

        #endregion

        #region Constructor

        internal ReferencesPopup()
        {
            StaysOpen          = true;
            AllowsTransparency = true;
            BuildUI();
            PreviewKeyDown += OnPreviewKeyDown;

            // Close when the application loses focus (user switches to another window).
            if (Application.Current is not null)
                Application.Current.Deactivated += OnApplicationDeactivated;
        }

        private void OnApplicationDeactivated(object? sender, EventArgs e)
            => Dispatcher.BeginInvoke(
                   System.Windows.Threading.DispatcherPriority.Background,
                   new Action(() => IsOpen = false));

        #endregion

        #region Public API

        internal void Show(
            CodeEditor                    owner,
            IReadOnlyList<ReferenceGroup> groups,
            string                        symbolName,
            Point                         anchor)
        {
            _anchor     = anchor;
            _symbolName = symbolName ?? string.Empty;
            _allCollapsed = false;

            PlacementTarget              = owner;
            Placement                    = PlacementMode.Custom;
            CustomPopupPlacementCallback = CalculatePlacement;

            PopulateContent(groups);
            IsOpen = true;
        }

        internal new void Close()
        {
            IsOpen = false;
            _groups.Clear();
            if (_scroll.Content is StackPanel old)
                old.Children.Clear();
        }

        internal void Dispose()
        {
            if (Application.Current is not null)
                Application.Current.Deactivated -= OnApplicationDeactivated;
        }

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            // ── Outer border: shadow + rounded corners ─────────────────────────
            var outerBorder = new Border
            {
                MinWidth        = 480,
                MaxWidth        = 700,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Effect          = new DropShadowEffect
                {
                    Color       = Colors.Black,
                    Opacity     = 0.40,
                    BlurRadius  = 10,
                    ShadowDepth = 3
                }
            };
            outerBorder.SetResourceReference(Border.BackgroundProperty,  "TE_Background");
            outerBorder.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");

            // ── Root grid: body (row 0) + footer (row 1) ──────────────────────
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── Scrollable body ────────────────────────────────────────────────
            _scroll = new ScrollViewer
            {
                MaxHeight                     = 400,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _scroll.SetResourceReference(ScrollViewer.BackgroundProperty, "TE_Background");
            Grid.SetRow(_scroll, 0);

            // ── Footer ────────────────────────────────────────────────────────
            var footer = new Border
            {
                Padding         = new Thickness(10, 5, 10, 5),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            footer.SetResourceReference(Border.BackgroundProperty,  "TE_Background");
            footer.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");
            Grid.SetRow(footer, 1);

            // Footer content: links left, pin right
            var footerRow = new DockPanel { LastChildFill = false };

            // Pin button — right-aligned
            var pinBtn = new TextBlock
            {
                Text              = "\uE718",    // Segoe MDL2 — pin (thumbtack); \uE840 renders invisible
                FontFamily        = new FontFamily("Segoe MDL2 Assets"),
                FontSize          = 13,
                Background        = Brushes.Transparent,   // ensures full bounding-box hit-test
                Padding           = new Thickness(6, 0, 0, 0),
                Cursor            = Cursors.Hand,
                ToolTip           = "Ancrer la fenêtre contextuelle",
                VerticalAlignment = VerticalAlignment.Center
            };
            pinBtn.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");
            pinBtn.MouseEnter += (_, _) => pinBtn.SetResourceReference(
                TextBlock.ForegroundProperty, "CE_Keyword");
            pinBtn.MouseLeave += (_, _) => pinBtn.SetResourceReference(
                TextBlock.ForegroundProperty, "TE_Foreground");
            pinBtn.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;    // stop bubbling to CodeEditor.OnMouseDown
                PinRequested?.Invoke(this, EventArgs.Empty);
            };
            DockPanel.SetDock(pinBtn, Dock.Right);

            // "Tout réduire" — TextBlock link (no WPF Button blue hover artefact)
            _collapseLink = new TextBlock
            {
                Text              = "Tout réduire",
                FontSize          = 11,
                Background        = Brushes.Transparent,   // full bounding-box hit-test
                Cursor            = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            _collapseLink.SetResourceReference(TextBlock.ForegroundProperty, "CE_Keyword");
            _collapseLink.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;    // stop bubbling to CodeEditor.OnMouseDown
                OnCollapseAllClicked();
            };

            // "Actualiser" — TextBlock link
            var refreshLink = new TextBlock
            {
                Text              = "Actualiser",
                FontSize          = 11,
                Background        = Brushes.Transparent,   // full bounding-box hit-test
                Cursor            = Cursors.Hand,
                Margin            = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            refreshLink.SetResourceReference(TextBlock.ForegroundProperty, "CE_Keyword");
            refreshLink.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;    // stop bubbling to CodeEditor.OnMouseDown
                RefreshRequested?.Invoke(this, EventArgs.Empty);
            };

            footerRow.Children.Add(pinBtn);
            footerRow.Children.Add(_collapseLink);
            footerRow.Children.Add(refreshLink);
            footer.Child = footerRow;

            root.Children.Add(_scroll);
            root.Children.Add(footer);

            outerBorder.Child = root;
            Child = outerBorder;
        }

        #endregion

        #region Content Population

        private void PopulateContent(IReadOnlyList<ReferenceGroup> groups)
        {
            _allCollapsed         = false;
            _collapseLink.Text    = "Tout réduire";

            var panel = ReferencesTreeBuilder.BuildGroupsPanel(
                groups,
                _symbolName,
                e => NavigationRequested?.Invoke(this, e),
                out _groups);

            _scroll.Content = panel;
        }

        #endregion

        #region Collapse / Expand

        private void OnCollapseAllClicked()
        {
            _allCollapsed = !_allCollapsed;
            foreach (var (panel, chevron) in _groups)
            {
                panel.Visibility = _allCollapsed ? Visibility.Collapsed : Visibility.Visible;
                chevron.Text     = _allCollapsed ? "▶" : "▼";
            }
            _collapseLink.Text = _allCollapsed ? "Tout développer" : "Tout réduire";
        }

        #endregion

        #region Popup Placement

        private CustomPopupPlacement[] CalculatePlacement(
            Size popupSize, Size targetSize, Point offset)
        {
            double x = Math.Min(_anchor.X, Math.Max(0, targetSize.Width  - popupSize.Width  - 8));
            double y = _anchor.Y;
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
