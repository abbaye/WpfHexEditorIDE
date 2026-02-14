using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace WpfHexaEditor.Controls
{
    /// <summary>
    /// Bar chart panel displaying byte frequency distribution (0x00-0xFF).
    /// V1 compatible feature - visualizes which byte values appear most frequently in the file.
    /// Phase 7.4 - Complete implementation.
    /// </summary>
    public class BarChartPanel : FrameworkElement
    {
        #region Fields

        private long[] _byteFrequencies = new long[256]; // Frequency count for each byte value
        private long _totalBytes = 0;
        private long _maxFrequency = 0;
        private Brush _barBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)); // Blue bars
        private Brush _textBrush = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42)); // Dark gray text
        private Typeface _typeface;

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
        /// Update the chart with data from ViewModel (efficient for large files).
        /// </summary>
        public void UpdateDataFromViewModel(ViewModels.HexEditorViewModel viewModel)
        {
            if (viewModel == null || viewModel.VirtualLength == 0)
            {
                Clear();
                return;
            }

            // Reset frequencies
            Array.Clear(_byteFrequencies, 0, 256);
            _totalBytes = viewModel.VirtualLength;

            // Sample large files (analyze first 1MB for performance)
            long bytesToAnalyze = Math.Min(_totalBytes, 1024 * 1024);

            for (long i = 0; i < bytesToAnalyze; i++)
            {
                try
                {
                    byte b = viewModel.GetByteAt(new Models.VirtualPosition(i));
                    _byteFrequencies[b]++;
                }
                catch
                {
                    // Skip invalid positions
                }
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

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // Draw white background
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (_totalBytes == 0 || _maxFrequency == 0)
            {
                // Draw "No data" message
                var formattedText = new FormattedText(
                    "No data to display",
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

            // Draw X-axis labels (show every 16th byte value: 00, 10, 20, ..., F0)
            for (int i = 0; i < 256; i += 16)
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

            // Draw statistics in top-right corner
            var stats = new FormattedText(
                $"Total: {_totalBytes:N0} bytes  |  Max: {_maxFrequency:N0} ({GetPercentage((byte)Array.IndexOf(_byteFrequencies, _maxFrequency)):F2}%)",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                11,
                _textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(stats, new Point(ActualWidth - stats.Width - 5, 5));
        }

        #endregion
    }
}
