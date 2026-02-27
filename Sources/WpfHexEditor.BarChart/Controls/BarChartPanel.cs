using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfHexaEditor.Interfaces;
using static WpfHexEditor.BarChart.Properties.Resources;

namespace WpfHexEditor.BarChart.Controls
{
    /// <summary>
    /// Bar chart panel displaying byte frequency distribution (0x00-0xFF).
    /// Implements <see cref="IByteDistributionPanel"/> to connect to HexEditor via
    /// the <c>ByteDistributionPanel</c> dependency property.
    /// </summary>
    public class BarChartPanel : FrameworkElement, IByteDistributionPanel
    {
        #region Fields

        private long[] _byteFrequencies = new long[256]; // Frequency count for each byte value
        private long _totalBytes = 0;
        private long _maxFrequency = 0;
        private Brush _barBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)); // Blue bars
        private Brush _textBrush = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42)); // Dark gray text
        private Brush _backgroundBrush = Brushes.White;
        private Brush _gridLineBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)); // Semi-transparent gray
        private Typeface _typeface;

        // Configurable properties
        private Color _backgroundColor = Colors.White;
        private Color _textColor = Color.FromRgb(0x42, 0x42, 0x42);
        private Color _gridLineColor = Color.FromArgb(0x40, 0x80, 0x80, 0x80);
        private bool _showAxisLabels = true;
        private bool _showGridLines = false;
        private bool _showStatistics = true;
        private int _axisLabelFrequency = 16;

        #endregion

        #region Constructor

        public BarChartPanel()
        {
            _typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _barBrush.Freeze();
            _textBrush.Freeze();

            // Default size
            Width = 800;
            Height = 200;
            MinWidth = 256; // At least 1 pixel per byte value
            MinHeight = 100;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the bar color.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the background color of the chart panel.
        /// </summary>
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                _backgroundBrush = new SolidColorBrush(value);
                _backgroundBrush.Freeze();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets or sets the text color for labels and statistics.
        /// </summary>
        public Color TextColor
        {
            get => _textColor;
            set
            {
                _textColor = value;
                _textBrush = new SolidColorBrush(value);
                _textBrush.Freeze();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets or sets the grid line color.
        /// </summary>
        public Color GridLineColor
        {
            get => _gridLineColor;
            set
            {
                _gridLineColor = value;
                _gridLineBrush = new SolidColorBrush(value);
                _gridLineBrush.Freeze();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets or sets whether to show axis labels (00-FF).
        /// </summary>
        public bool ShowAxisLabels
        {
            get => _showAxisLabels;
            set
            {
                _showAxisLabels = value;
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets or sets whether to show horizontal grid lines.
        /// </summary>
        public bool ShowGridLines
        {
            get => _showGridLines;
            set
            {
                _showGridLines = value;
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets or sets whether to show statistics overlay.
        /// </summary>
        public bool ShowStatistics
        {
            get => _showStatistics;
            set
            {
                _showStatistics = value;
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets or sets the frequency of axis labels (every N bytes).
        /// Default is 16 (shows 00, 10, 20, ..., F0).
        /// </summary>
        public int AxisLabelFrequency
        {
            get => _axisLabelFrequency;
            set
            {
                _axisLabelFrequency = Math.Max(1, Math.Min(value, 64)); // Clamp between 1 and 64
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets the total number of bytes analyzed.
        /// </summary>
        public long TotalBytes => _totalBytes;

        /// <summary>
        /// Gets the maximum frequency (most common byte count).
        /// </summary>
        public long MaxFrequency => _maxFrequency;

        #endregion

        #region Public Methods

        /// <summary>
        /// Update the chart with byte data from the editor.
        /// Analyzes all bytes and calculates frequency distribution.
        /// </summary>
        public void UpdateData(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Clear();
                return;
            }

            // Reset frequencies
            Array.Clear(_byteFrequencies, 0, 256);
            _totalBytes = data.Length;

            // Count byte frequencies
            foreach (byte b in data)
            {
                _byteFrequencies[b]++;
            }

            // Find max frequency for scaling
            _maxFrequency = _byteFrequencies.Max();

            InvalidateVisual();
        }

        /// <summary>
        /// Clear the chart.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_byteFrequencies, 0, 256);
            _totalBytes = 0;
            _maxFrequency = 0;
            InvalidateVisual();
        }

        /// <summary>
        /// Get frequency count for a specific byte value.
        /// </summary>
        public long GetFrequency(byte value)
        {
            return _byteFrequencies[value];
        }

        /// <summary>
        /// Get percentage for a specific byte value.
        /// </summary>
        public double GetPercentage(byte value)
        {
            if (_totalBytes == 0) return 0;
            return (_byteFrequencies[value] * 100.0) / _totalBytes;
        }

        /// <summary>
        /// Calculate Shannon entropy of the byte distribution.
        /// Returns value between 0 (no entropy, all same byte) and 8 (maximum entropy, uniform distribution).
        /// High entropy (&gt;7.5) suggests encrypted/compressed data.
        /// Low entropy (&lt;5) suggests uncompressed data with patterns.
        /// </summary>
        public double CalculateEntropy()
        {
            if (_totalBytes == 0) return 0;

            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                double p = GetPercentage((byte)i) / 100.0;
                if (p > 0)
                {
                    entropy -= p * Math.Log(p, 2);
                }
            }

            return entropy;
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // Draw background with configurable color
            dc.DrawRectangle(_backgroundBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (_totalBytes == 0 || _maxFrequency == 0)
            {
                // Draw "No data" message (localized)
                var noDataText = BarChart_NoData ?? "No data to display";
                var formattedText = new FormattedText(
                    noDataText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    14,
                    _textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(formattedText, new Point(
                    (ActualWidth - formattedText.Width) / 2,
                    (ActualHeight - formattedText.Height) / 2));
                return;
            }

            // Calculate bar dimensions
            double barWidth = ActualWidth / 256.0;
            double maxBarHeight = ActualHeight - 30; // Leave space for labels

            // Draw grid lines (optional)
            if (_showGridLines)
            {
                var gridPen = new Pen(_gridLineBrush, 1);
                gridPen.Freeze();

                // Draw 5 horizontal grid lines for frequency reference
                for (int i = 0; i <= 4; i++)
                {
                    double y = (ActualHeight - 30) - (maxBarHeight / 4) * i;
                    dc.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));
                }
            }

            // Draw bars
            for (int i = 0; i < 256; i++)
            {
                long frequency = _byteFrequencies[i];
                if (frequency == 0) continue;

                // Calculate bar height (normalized to max)
                double barHeight = (frequency / (double)_maxFrequency) * maxBarHeight;
                double x = i * barWidth;
                double y = ActualHeight - barHeight - 20; // Leave space for X-axis labels

                // Draw bar
                var rect = new Rect(x, y, Math.Max(1, barWidth - 1), barHeight);
                dc.DrawRectangle(_barBrush, null, rect);
            }

            // Draw X-axis labels (optional, configurable frequency)
            if (_showAxisLabels)
            {
                for (int i = 0; i < 256; i += _axisLabelFrequency)
                {
                    double x = i * barWidth;
                    var label = new FormattedText(
                        $"{i:X2}",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        _typeface,
                        10,
                        _textBrush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    dc.DrawText(label, new Point(x, ActualHeight - 15));
                }
            }

            // Draw statistics in top-right corner (optional, localized)
            if (_showStatistics)
            {
                double entropy = CalculateEntropy();

                // Localized labels
                var totalLabel = BarChart_Total ?? "Total";
                var maxLabel = BarChart_Max ?? "Max";
                var entropyLabel = BarChart_Entropy ?? "Entropy";
                var bytesLabel = BarChart_Bytes ?? "bytes";
                var bitsPerByteLabel = BarChart_BitsPerByte ?? "bits/byte";

                var statsText = $"{totalLabel}: {_totalBytes:N0} {bytesLabel}  |  {maxLabel}: {_maxFrequency:N0} ({GetPercentage((byte)Array.IndexOf(_byteFrequencies, _maxFrequency)):F2}%)  |  {entropyLabel}: {entropy:F2} {bitsPerByteLabel}";

                var stats = new FormattedText(
                    statsText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    11,
                    _textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(stats, new Point(ActualWidth - stats.Width - 5, 5));
            }
        }

        #endregion
    }
}
