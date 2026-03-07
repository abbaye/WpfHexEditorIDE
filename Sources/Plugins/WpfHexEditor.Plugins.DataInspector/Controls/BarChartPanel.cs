// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: BarChartPanel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Bar chart FrameworkElement displaying byte frequency distribution (0x00-0xFF).
//     Moved from WpfHexEditor.Panels.BinaryAnalysis.Controls to live entirely
//     within the DataInspector plugin. Managed directly by DataInspectorPanel
//     via UpdateData() / Clear() — not wired via HexEditor ByteDistributionPanel DP.
//
// Architecture Notes:
//     - DrawingContext-based custom rendering for maximum performance
//     - Zoom support via ViewStartByte/ViewEndByte (ZoomToRange / ZoomReset)
//     - Default range 0-255 is fully backward-compatible
//     - No external resource file dependency (strings inlined as constants)
//     - No dependency on WpfHexEditor.Core (plugin boundary respected)
// ==========================================================

using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Plugins.DataInspector.Controls
{
    /// <summary>
    /// Bar chart panel displaying byte frequency distribution (0x00-0xFF).
    /// Managed directly by <see cref="Views.DataInspectorPanel"/> — data is pushed
    /// via <see cref="UpdateData"/> and <see cref="Clear"/>; no HexEditor DP binding.
    ///
    /// Supports zoom via <see cref="ZoomToRange"/> — only the bytes in
    /// [<see cref="ViewStartByte"/>, <see cref="ViewEndByte"/>] are rendered.
    /// Default range is 0-255 (full distribution, backward-compatible).
    /// </summary>
    public class BarChartPanel : FrameworkElement
    {
        #region Inlined resource strings

        private const string Str_NoData      = "No data to display";
        private const string Str_Total       = "Total";
        private const string Str_Max         = "Max";
        private const string Str_Entropy     = "Entropy";
        private const string Str_BitsPerByte = "bits/byte";
        private const string Str_Bytes       = "bytes";

        #endregion

        #region Fields

        private long[] _byteFrequencies = new long[256];
        private long   _totalBytes      = 0;
        private long   _maxFrequency    = 0;

        private Brush _barBrush        = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
        private Brush _textBrush       = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42));
        private Brush _backgroundBrush = Brushes.White;
        private Brush _gridLineBrush   = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80));

        private Typeface _typeface;

        private Color _backgroundColor = Colors.White;
        private Color _textColor       = Color.FromRgb(0x42, 0x42, 0x42);
        private Color _gridLineColor   = Color.FromArgb(0x40, 0x80, 0x80, 0x80);

        private bool _showAxisLabels     = true;
        private bool _showGridLines      = false;
        private bool _showStatistics     = true;
        private int  _axisLabelFrequency = 16;

        // Zoom / view range — defaults to full 0-255 (backward-compatible)
        private int _viewStartByte = 0;
        private int _viewEndByte   = 255;

        #endregion

        #region Constructor

        public BarChartPanel()
        {
            _typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _barBrush.Freeze();
            _textBrush.Freeze();

            Width     = 800;
            Height    = 200;
            MinWidth  = 256;
            MinHeight = 100;
        }

        #endregion

        #region Public Properties — Colors & Display

        /// <summary>Gets or sets the bar fill color.</summary>
        public Color BarColor
        {
            get => (_barBrush as SolidColorBrush)?.Color ?? Colors.Blue;
            set
            {
                _barBrush = new SolidColorBrush(value);
                _barBrush.Freeze();
                InvalidateVisual();
            }
        }

        /// <summary>Gets or sets the chart background color.</summary>
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor   = value;
                _backgroundBrush   = new SolidColorBrush(value);
                _backgroundBrush.Freeze();
                InvalidateVisual();
            }
        }

        /// <summary>Gets or sets the label and stats text color.</summary>
        public Color TextColor
        {
            get => _textColor;
            set
            {
                _textColor   = value;
                _textBrush   = new SolidColorBrush(value);
                _textBrush.Freeze();
                InvalidateVisual();
            }
        }

        /// <summary>Gets or sets the horizontal grid line color.</summary>
        public Color GridLineColor
        {
            get => _gridLineColor;
            set
            {
                _gridLineColor   = value;
                _gridLineBrush   = new SolidColorBrush(value);
                _gridLineBrush.Freeze();
                InvalidateVisual();
            }
        }

        /// <summary>Gets or sets whether to render X-axis labels (00, 10, 20 ...).</summary>
        public bool ShowAxisLabels
        {
            get => _showAxisLabels;
            set { _showAxisLabels = value; InvalidateVisual(); }
        }

        /// <summary>Gets or sets whether to render horizontal grid lines.</summary>
        public bool ShowGridLines
        {
            get => _showGridLines;
            set { _showGridLines = value; InvalidateVisual(); }
        }

        /// <summary>Gets or sets whether to render the top-right statistics overlay.</summary>
        public bool ShowStatistics
        {
            get => _showStatistics;
            set { _showStatistics = value; InvalidateVisual(); }
        }

        /// <summary>
        /// Interval between X-axis labels (every N byte values).
        /// Default 16 → labels at 0x00, 0x10, 0x20 ... 0xF0.
        /// </summary>
        public int AxisLabelFrequency
        {
            get => _axisLabelFrequency;
            set { _axisLabelFrequency = Math.Max(1, Math.Min(value, 64)); InvalidateVisual(); }
        }

        /// <summary>Gets the total number of bytes in the last <see cref="UpdateData"/> call.</summary>
        public long TotalBytes => _totalBytes;

        /// <summary>Gets the highest per-byte frequency count in the current data.</summary>
        public long MaxFrequency => _maxFrequency;

        #endregion

        #region Public Properties — Zoom / View Range

        /// <summary>
        /// First byte value shown in the chart (0-255).
        /// Use <see cref="ZoomToRange"/> to change both boundaries atomically.
        /// Default: 0.
        /// </summary>
        public int ViewStartByte
        {
            get => _viewStartByte;
            set
            {
                _viewStartByte = Math.Max(0, Math.Min(255, value));
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Last byte value shown in the chart (0-255).
        /// Use <see cref="ZoomToRange"/> to change both boundaries atomically.
        /// Default: 255.
        /// </summary>
        public int ViewEndByte
        {
            get => _viewEndByte;
            set
            {
                _viewEndByte = Math.Max(0, Math.Min(255, value));
                InvalidateVisual();
            }
        }

        #endregion

        #region Public Methods — Data

        /// <summary>
        /// Loads new byte data and redraws the chart.
        /// Analyzes full-array frequency distribution; zoom range is preserved.
        /// </summary>
        public void UpdateData(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Clear();
                return;
            }

            Array.Clear(_byteFrequencies, 0, 256);
            _totalBytes = data.Length;

            foreach (byte b in data)
                _byteFrequencies[b]++;

            _maxFrequency = _byteFrequencies.Max();

            InvalidateVisual();
        }

        /// <summary>Clears all data and redraws an empty chart.</summary>
        public void Clear()
        {
            Array.Clear(_byteFrequencies, 0, 256);
            _totalBytes   = 0;
            _maxFrequency = 0;
            InvalidateVisual();
        }

        /// <summary>Returns the raw frequency count for a specific byte value.</summary>
        public long GetFrequency(byte value) => _byteFrequencies[value];

        /// <summary>Returns the percentage (0-100) for a specific byte value.</summary>
        public double GetPercentage(byte value)
        {
            if (_totalBytes == 0) return 0;
            return (_byteFrequencies[value] * 100.0) / _totalBytes;
        }

        /// <summary>
        /// Calculates the Shannon entropy of the full byte distribution.
        /// Returns 0-8 bits; ≥7.5 suggests encrypted/compressed data.
        /// </summary>
        public double CalculateEntropy()
        {
            if (_totalBytes == 0) return 0;

            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                double p = GetPercentage((byte)i) / 100.0;
                if (p > 0) entropy -= p * Math.Log(p, 2);
            }

            return entropy;
        }

        #endregion

        #region Public Methods — Zoom

        /// <summary>
        /// Restricts the rendered view to byte values in [<paramref name="startByte"/>, <paramref name="endByte"/>].
        /// Both bounds are clamped to 0-255 and swapped if out of order.
        /// </summary>
        public void ZoomToRange(int startByte, int endByte)
        {
            var lo = Math.Max(0, Math.Min(255, Math.Min(startByte, endByte)));
            var hi = Math.Max(0, Math.Min(255, Math.Max(startByte, endByte)));
            if (hi <= lo) hi = Math.Min(lo + 1, 255);

            _viewStartByte = lo;
            _viewEndByte   = hi;
            InvalidateVisual();
        }

        /// <summary>Resets zoom to show the full 0x00-0xFF distribution.</summary>
        public void ZoomReset()
        {
            _viewStartByte = 0;
            _viewEndByte   = 255;
            InvalidateVisual();
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            dc.DrawRectangle(_backgroundBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (_totalBytes == 0 || _maxFrequency == 0)
            {
                var ft = MakeText(Str_NoData, 14);
                dc.DrawText(ft, new Point((ActualWidth - ft.Width) / 2, (ActualHeight - ft.Height) / 2));
                return;
            }

            // Number of byte values in the current view range
            int viewCount  = _viewEndByte - _viewStartByte + 1;
            double barW    = ActualWidth / (double)viewCount;
            double maxBarH = ActualHeight - 30;   // space for X-axis labels

            // Horizontal grid lines
            if (_showGridLines)
            {
                var pen = new Pen(_gridLineBrush, 1);
                pen.Freeze();
                for (int i = 0; i <= 4; i++)
                {
                    double y = (ActualHeight - 30) - (maxBarH / 4) * i;
                    dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
                }
            }

            // Bars — only the bytes inside the view range
            for (int i = _viewStartByte; i <= _viewEndByte; i++)
            {
                long freq = _byteFrequencies[i];
                if (freq == 0) continue;

                double barH = (freq / (double)_maxFrequency) * maxBarH;
                double x    = (i - _viewStartByte) * barW;
                double y    = ActualHeight - barH - 20;

                var rect = new Rect(x, y, Math.Max(1, barW - 1), barH);
                dc.DrawRectangle(_barBrush, null, rect);
            }

            // X-axis labels — show real byte value of each labelled bar
            if (_showAxisLabels)
            {
                // Determine a sensible label interval for the current view count
                int labelInterval = _axisLabelFrequency;
                if (viewCount <= 32)       labelInterval = 4;
                else if (viewCount <= 64)  labelInterval = 8;
                else if (viewCount <= 128) labelInterval = 16;

                for (int i = _viewStartByte; i <= _viewEndByte; i++)
                {
                    if ((i - _viewStartByte) % labelInterval != 0) continue;

                    double x  = (i - _viewStartByte) * barW;
                    var label = MakeText($"{i:X2}", 10);
                    dc.DrawText(label, new Point(x, ActualHeight - 15));
                }
            }

            // Statistics overlay (top-right corner) — always over the full distribution
            if (_showStatistics)
            {
                double entropy    = CalculateEntropy();
                int mostCommonIdx = (int)Array.IndexOf(_byteFrequencies, _maxFrequency);
                var statsText     = $"{Str_Total}: {_totalBytes:N0} {Str_Bytes}  |  {Str_Max}: {_maxFrequency:N0} ({GetPercentage((byte)mostCommonIdx):F2}%)  |  {Str_Entropy}: {entropy:F2} {Str_BitsPerByte}";

                // Zoom range hint (when not full range)
                if (_viewStartByte != 0 || _viewEndByte != 255)
                    statsText += $"  |  View: 0x{_viewStartByte:X2}-0x{_viewEndByte:X2}";

                var stats = MakeText(statsText, 11);
                dc.DrawText(stats, new Point(ActualWidth - stats.Width - 5, 5));
            }
        }

        // Helper: creates a FormattedText with the current text brush and DPI.
        private FormattedText MakeText(string text, double size)
            => new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                size,
                _textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

        #endregion
    }
}
