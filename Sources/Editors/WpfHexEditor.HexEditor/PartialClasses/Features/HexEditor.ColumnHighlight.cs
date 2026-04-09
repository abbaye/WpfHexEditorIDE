// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: PartialClasses/Features/HexEditor.ColumnHighlight.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Updated: 2026-03-29 — Row highlight + ASCII stripe + height clamping + options DPs
// Description:
//     Partial class wiring ColumnHighlightOverlay to cursor position changes.
//     Renders up to three highlight elements:
//       • Vertical column stripe in the hex panel (active byte column)
//       • Vertical column stripe in the ASCII panel (same column, when ShowAscii=true)
//       • Horizontal row stripe (line the cursor is on)
//     All stripes are clamped to the visible content height so they never bleed
//     into the empty space below the last rendered line.
//
// Architecture Notes:
//     ShowColumnHighlight / ShowRowHighlight / ShowAsciiColumnHighlight are DPs
//     wired from EditorSettingsService → HexEditorDefaultSettings.
//     Subscribes to SelectionStartChanged + ZoomScaleChanged.
//     Geometry is read from HexViewport public API.
// ==========================================================

using System;
using System.Windows;
using WpfHexEditor.Core;
using WpfHexEditor.HexEditor.Controls;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        // ── Dependency Properties ─────────────────────────────────────────────

        /// <summary>Shows or hides the active column highlight overlay (hex panel stripe).</summary>
        public static readonly DependencyProperty ShowColumnHighlightProperty =
            DependencyProperty.Register(
                nameof(ShowColumnHighlight),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true, OnColumnHighlightOptionChanged));

        public bool ShowColumnHighlight
        {
            get => (bool)GetValue(ShowColumnHighlightProperty);
            set => SetValue(ShowColumnHighlightProperty, value);
        }

        /// <summary>Shows or hides the horizontal row highlight behind the active line.</summary>
        public static readonly DependencyProperty ShowRowHighlightProperty =
            DependencyProperty.Register(
                nameof(ShowRowHighlight),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true, OnColumnHighlightOptionChanged));

        public bool ShowRowHighlight
        {
            get => (bool)GetValue(ShowRowHighlightProperty);
            set => SetValue(ShowRowHighlightProperty, value);
        }

        /// <summary>Shows or hides the column stripe in the ASCII panel.</summary>
        public static readonly DependencyProperty ShowAsciiColumnHighlightProperty =
            DependencyProperty.Register(
                nameof(ShowAsciiColumnHighlight),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true, OnColumnHighlightOptionChanged));

        public bool ShowAsciiColumnHighlight
        {
            get => (bool)GetValue(ShowAsciiColumnHighlightProperty);
            set => SetValue(ShowAsciiColumnHighlightProperty, value);
        }

        private static void OnColumnHighlightOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditor editor) return;
            // Hide immediately if all highlights are disabled; otherwise refresh position.
            if (!editor.ShowColumnHighlight && !editor.ShowRowHighlight && !editor.ShowAsciiColumnHighlight)
                editor._columnHighlight?.Hide();
            else
                editor.UpdateColumnHighlight();
        }

        // ── State ─────────────────────────────────────────────────────────────

        private ColumnHighlightOverlay? _columnHighlight;

        // ── Initialisation ────────────────────────────────────────────────────

        private void InitializeColumnHighlight()
        {
            _columnHighlight = this.FindName("ColHighlightOverlay") as ColumnHighlightOverlay;
            if (_columnHighlight is null) return;

            SelectionStartChanged += OnColumnHighlightSelectionChanged;
            ZoomScaleChanged     += OnColumnHighlightSelectionChanged;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnColumnHighlightSelectionChanged(object? sender, EventArgs e)
            => UpdateColumnHighlight();

        private void UpdateColumnHighlight()
        {
            if (_columnHighlight is null) return;

            // All highlights disabled → hide everything.
            if (!ShowColumnHighlight && !ShowRowHighlight && !ShowAsciiColumnHighlight)
            {
                _columnHighlight.Hide();
                return;
            }

            if (!_viewModel.SelectionStart.IsValid)
            {
                _columnHighlight.Hide();
                return;
            }

            int bytesPerLine = HexViewport.BytesPerLine;
            if (bytesPerLine <= 0) { _columnHighlight.Hide(); return; }

            long   offset    = _viewModel.SelectionStart.Value;
            int    colIdx    = (int)(offset % bytesPerLine);
            double zoom      = ZoomScale;
            double cellWidth = HexViewport.CalculateCellWidthForByteCount(1) * zoom;
            double hexStart  = HexViewport.HexPanelStartX * zoom;

            // Byte-group spacer offset (only for the hex panel stripe).
            double spacerOffset = 0;
            int groupSize = (int)HexViewport.ByteGrouping;
            if (bytesPerLine >= groupSize &&
                (HexViewport.ByteSpacerPositioning == ByteSpacerPosition.Both ||
                 HexViewport.ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel))
            {
                int spacerCount = colIdx / groupSize;
                spacerOffset = spacerCount * (int)HexViewport.ByteSpacerWidthTickness * zoom;
            }

            // Fetch visible lines once — used for both row and ASCII column calculations.
            var visibleLinesList = HexViewport.GetVisibleLinesForHighlight();

            // Visible content height — clamps all stripes so they don't bleed
            // into the empty space below the last rendered line.
            double lineHeight = HexViewport.LineHeight * zoom;
            double visibleH   = visibleLinesList.Count > 0 ? visibleLinesList.Count * lineHeight : 0;

            // Column stripe: shown only in the currently active panel.
            bool hexActive   = HexViewport.ActivePanel == Controls.ActivePanelType.Hex;
            bool asciiActive = HexViewport.ActivePanel == Controls.ActivePanelType.Ascii;

            // Hex column stripe: only when hex panel is active.
            // Re-use colIdx = -1 to tell the overlay to skip the hex stripe.
            int effectiveColIdx = (ShowColumnHighlight && hexActive) ? colIdx : -1;

            // ASCII stripe: read pixel position from the pre-computed AsciiRect and convert
            // to overlay coordinate space via TranslatePoint (handles LayoutTransform + scroll).
            double asciiX  = -1;
            double asciiCW = 0;
            if (ShowAsciiColumnHighlight && asciiActive && HexViewport.ShowAscii)
            {
                foreach (var line in visibleLinesList)
                {
                    if (colIdx < line.Bytes.Count && line.Bytes[colIdx].AsciiRect.HasValue)
                    {
                        var rect = line.Bytes[colIdx].AsciiRect.Value;
                        var originInOverlay = HexViewport.TranslatePoint(new System.Windows.Point(rect.X, 0), _columnHighlight);
                        if (!double.IsNaN(originInOverlay.X))
                        {
                            asciiX  = originInOverlay.X;
                            asciiCW = rect.Width * zoom;   // width scales with zoom
                        }
                        break;
                    }
                }
            }

            // Row highlight position: which line number is the cursor on?
            double rowY      = -1;
            double rowHeight = lineHeight;
            if (ShowRowHighlight && visibleLinesList.Count > 0 && lineHeight > 0)
            {
                if (visibleLinesList.Count > 0)
                {
                    long firstOffset = visibleLinesList[0].Bytes[0].VirtualPos;
                    long lineIndex   = (offset - firstOffset) / bytesPerLine;
                    if (lineIndex >= 0 && lineIndex < visibleLinesList.Count)
                        rowY = lineIndex * lineHeight;
                }
            }

            _columnHighlight.SetColumn(
                columnIndex:          effectiveColIdx,
                hexPanelStartX:       hexStart,
                cellWidth:            cellWidth,
                spacerOffset:         spacerOffset,
                visibleContentHeight: visibleH,
                asciiPanelStartX:     asciiX,
                asciiCharWidth:       asciiCW,
                rowY:                 rowY,
                rowHeight:            rowHeight,
                showRow:              ShowRowHighlight && rowY >= 0);
        }
    }
}
