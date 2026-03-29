// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: PartialClasses/Features/HexEditor.ColumnHighlight.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Partial class wiring ColumnHighlightOverlay to cursor position changes.
//     The overlay renders a semi-transparent vertical stripe on the byte column
//     matching the current cursor position.
//
// Architecture Notes:
//     Subscribes to SelectionStartChanged to recompute the active column.
//     Column index = (cursorOffset % BytesPerLine).
//     Cell width and hex panel start are read from HexViewport public API.
//     ShowColumnHighlight DP controls visibility.
// ==========================================================

using System;
using System.Windows;
using WpfHexEditor.Core;
using WpfHexEditor.HexEditor.Controls;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        // ── Dependency Property ───────────────────────────────────────────────

        /// <summary>Shows or hides the active column highlight overlay.</summary>
        public static readonly DependencyProperty ShowColumnHighlightProperty =
            DependencyProperty.Register(
                nameof(ShowColumnHighlight),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true, OnShowColumnHighlightChanged));

        public bool ShowColumnHighlight
        {
            get => (bool)GetValue(ShowColumnHighlightProperty);
            set => SetValue(ShowColumnHighlightProperty, value);
        }

        private static void OnShowColumnHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditor editor) return;
            if (!(bool)e.NewValue)
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
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnColumnHighlightSelectionChanged(object? sender, EventArgs e)
            => UpdateColumnHighlight();

        private void UpdateColumnHighlight()
        {
            if (_columnHighlight is null || !ShowColumnHighlight) return;

            if (!_viewModel.SelectionStart.IsValid)
            {
                _columnHighlight.Hide();
                return;
            }

            int bytesPerLine = HexViewport.BytesPerLine;
            if (bytesPerLine <= 0) { _columnHighlight.Hide(); return; }

            long   offset    = _viewModel.SelectionStart.Value;
            int    colIdx    = (int)(offset % bytesPerLine);
            double cellWidth = HexViewport.CalculateCellWidthForByteCount(1) + 2; // +2 for HexByteSpacing
            double hexStart  = HexViewport.HexPanelStartX;

            // Account for byte-group spacers inserted by the renderer at every group boundary.
            // e.g. with ByteGrouping=4 and colIdx=6: 1 spacer before col 4 → +spacerWidth
            double spacerOffset = 0;
            int groupSize = (int)HexViewport.ByteGrouping;
            if (bytesPerLine >= groupSize &&
                (HexViewport.ByteSpacerPositioning == ByteSpacerPosition.Both ||
                 HexViewport.ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel))
            {
                int spacerCount = colIdx / groupSize;
                spacerOffset = spacerCount * (int)HexViewport.ByteSpacerWidthTickness;
            }

            _columnHighlight.SetColumn(colIdx, hexStart, cellWidth, spacerOffset);
        }
    }
}
