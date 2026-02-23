//////////////////////////////////////////////
// Apache 2.0  - 2026
// Pattern Analysis Panel
// Author : Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfHexaEditor.Views.Panels
{
    /// <summary>
    /// Panel for analyzing byte patterns, entropy, and anomalies
    /// </summary>
    public partial class PatternAnalysisPanel : UserControl
    {
        private byte[] _analysisData;

        public PatternAnalysisPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Analyze the provided byte array
        /// </summary>
        public void Analyze(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                ShowNoDataMessage();
                return;
            }

            _analysisData = data;
            StatusTextBlock.Text = $"Analyzing {data.Length:N0} bytes...";

            try
            {
                // Perform analysis
                var entropy = CalculateEntropy(data);
                var distribution = CalculateByteDistribution(data);
                var patterns = DetectPatterns(data);
                var anomalies = DetectAnomalies(data, distribution);

                // Update UI
                UpdateEntropyCard(entropy);
                UpdateDistributionCard(distribution);
                UpdatePatternsCard(patterns);
                UpdateAnomaliesCard(anomalies);

                StatusTextBlock.Text = $"Analysis complete ({data.Length:N0} bytes)";
                ShowAllCards();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Analysis failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Calculate Shannon entropy (0-8 bits per byte)
        /// </summary>
        private double CalculateEntropy(byte[] data)
        {
            if (data.Length == 0) return 0;

            var frequency = new int[256];
            foreach (var b in data)
                frequency[b]++;

            double entropy = 0;
            foreach (var count in frequency)
            {
                if (count == 0) continue;
                double probability = (double)count / data.Length;
                entropy -= probability * Math.Log(probability, 2);
            }

            return entropy;
        }

        /// <summary>
        /// Calculate byte distribution
        /// </summary>
        private int[] CalculateByteDistribution(byte[] data)
        {
            var distribution = new int[256];
            foreach (var b in data)
                distribution[b]++;
            return distribution;
        }

        /// <summary>
        /// Detect common patterns in data
        /// </summary>
        private List<PatternInfo> DetectPatterns(byte[] data)
        {
            var patterns = new List<PatternInfo>();

            // Check for null bytes
            int nullCount = data.Count(b => b == 0x00);
            if (nullCount > data.Length * 0.3)
            {
                patterns.Add(new PatternInfo
                {
                    Icon = "🔲",
                    Pattern = "NULL bytes",
                    Description = $"{nullCount:N0} null bytes ({(nullCount * 100.0 / data.Length):F1}% of data)"
                });
            }

            // Check for repeated bytes
            if (data.Length > 4)
            {
                var repeats = FindRepeatedSequences(data, 4);
                if (repeats.Count > 0)
                {
                    var topRepeat = repeats.First();
                    patterns.Add(new PatternInfo
                    {
                        Icon = "🔁",
                        Pattern = BitConverter.ToString(topRepeat.Key).Replace("-", " "),
                        Description = $"Repeated {topRepeat.Value} times"
                    });
                }
            }

            // Check for ASCII text
            int asciiCount = data.Count(b => b >= 0x20 && b < 0x7F);
            if (asciiCount > data.Length * 0.7)
            {
                patterns.Add(new PatternInfo
                {
                    Icon = "📝",
                    Pattern = "ASCII text",
                    Description = $"{(asciiCount * 100.0 / data.Length):F1}% printable ASCII characters"
                });
            }

            // Check for aligned data (4-byte boundaries)
            if (data.Length % 4 == 0 && data.Length >= 16)
            {
                patterns.Add(new PatternInfo
                {
                    Icon = "📐",
                    Pattern = "4-byte aligned",
                    Description = "Data is aligned to 4-byte boundaries (typical for structured data)"
                });
            }

            return patterns;
        }

        /// <summary>
        /// Find repeated byte sequences
        /// </summary>
        private Dictionary<byte[], int> FindRepeatedSequences(byte[] data, int sequenceLength)
        {
            var sequences = new Dictionary<byte[], int>(new ByteArrayComparer());

            for (int i = 0; i <= data.Length - sequenceLength; i++)
            {
                var sequence = new byte[sequenceLength];
                Array.Copy(data, i, sequence, 0, sequenceLength);

                if (sequences.ContainsKey(sequence))
                    sequences[sequence]++;
                else
                    sequences[sequence] = 1;
            }

            return sequences.Where(kvp => kvp.Value > 2)
                           .OrderByDescending(kvp => kvp.Value)
                           .Take(5)
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Detect anomalies in data
        /// </summary>
        private List<AnomalyInfo> DetectAnomalies(byte[] data, int[] distribution)
        {
            var anomalies = new List<AnomalyInfo>();

            // Check for extremely skewed distribution
            var maxFrequency = distribution.Max();
            if (maxFrequency > data.Length * 0.9)
            {
                var dominantByte = Array.IndexOf(distribution, maxFrequency);
                anomalies.Add(new AnomalyInfo
                {
                    Title = "Extremely skewed distribution",
                    Description = $"Byte 0x{dominantByte:X2} appears {(maxFrequency * 100.0 / data.Length):F1}% of the time"
                });
            }

            // Check for suspiciously high entropy (possible encryption/compression)
            var entropy = CalculateEntropy(data);
            if (entropy > 7.5)
            {
                anomalies.Add(new AnomalyInfo
                {
                    Title = "Very high entropy detected",
                    Description = "Data may be encrypted or compressed (entropy > 7.5 bits/byte)"
                });
            }

            // Check for suspiciously low entropy (padding/zeros)
            if (entropy < 2.0)
            {
                anomalies.Add(new AnomalyInfo
                {
                    Title = "Very low entropy detected",
                    Description = "Data contains mostly repetitive or zero bytes (entropy < 2.0 bits/byte)"
                });
            }

            return anomalies;
        }

        /// <summary>
        /// Update entropy card UI
        /// </summary>
        private void UpdateEntropyCard(double entropy)
        {
            EntropyValueText.Text = entropy.ToString("F2");

            // Update bar width (percentage of 8.0)
            var percentage = entropy / 8.0;
            EntropyBar.Width = ActualWidth > 0 ? (ActualWidth - 48) * percentage : 100 * percentage;

            // Update color based on entropy level
            if (entropy < 3.0)
            {
                EntropyBar.Background = (SolidColorBrush)FindResource("LowEntropyBrush");
                EntropyInterpretation.Text = "Low randomness - data is repetitive or structured";
            }
            else if (entropy < 6.0)
            {
                EntropyBar.Background = (SolidColorBrush)FindResource("MediumEntropyBrush");
                EntropyInterpretation.Text = "Medium randomness - typical for mixed or compressed data";
            }
            else
            {
                EntropyBar.Background = (SolidColorBrush)FindResource("HighEntropyBrush");
                EntropyInterpretation.Text = "High randomness - data may be encrypted or highly compressed";
            }

            EntropyCard.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Update distribution card UI with histogram
        /// </summary>
        private void UpdateDistributionCard(int[] distribution)
        {
            // Clear previous histogram
            HistogramCanvas.Children.Clear();

            if (HistogramCanvas.ActualWidth == 0 || HistogramCanvas.ActualHeight == 0)
            {
                HistogramCanvas.Loaded += (s, e) => DrawHistogram(distribution);
            }
            else
            {
                DrawHistogram(distribution);
            }

            // Update stats
            var maxFreq = distribution.Max();
            var maxByte = Array.IndexOf(distribution, maxFreq);
            MostFrequentByteText.Text = $"0x{maxByte:X2} ({maxFreq:N0} times)";

            var uniqueBytes = distribution.Count(c => c > 0);
            UniqueBytesText.Text = $"{uniqueBytes} / 256";

            DistributionCard.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Draw histogram on canvas
        /// </summary>
        private void DrawHistogram(int[] distribution)
        {
            if (distribution == null || HistogramCanvas.ActualWidth == 0)
                return;

            HistogramCanvas.Children.Clear();

            var maxFreq = distribution.Max();
            if (maxFreq == 0) return;

            var width = HistogramCanvas.ActualWidth;
            var height = HistogramCanvas.ActualHeight;
            var barWidth = width / 256.0;

            for (int i = 0; i < 256; i++)
            {
                if (distribution[i] == 0) continue;

                var barHeight = (distribution[i] / (double)maxFreq) * height;
                var rect = new Rectangle
                {
                    Width = Math.Max(barWidth, 1),
                    Height = barHeight,
                    Fill = new SolidColorBrush(Color.FromRgb(74, 144, 226)),
                    ToolTip = $"0x{i:X2}: {distribution[i]:N0} bytes"
                };

                Canvas.SetLeft(rect, i * barWidth);
                Canvas.SetBottom(rect, 0);

                HistogramCanvas.Children.Add(rect);
            }
        }

        /// <summary>
        /// Update patterns card UI
        /// </summary>
        private void UpdatePatternsCard(List<PatternInfo> patterns)
        {
            if (patterns.Count > 0)
            {
                PatternsListBox.ItemsSource = patterns;
                NoPatternsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                PatternsListBox.ItemsSource = null;
                NoPatternsText.Visibility = Visibility.Visible;
            }

            PatternsCard.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Update anomalies card UI
        /// </summary>
        private void UpdateAnomaliesCard(List<AnomalyInfo> anomalies)
        {
            if (anomalies.Count > 0)
            {
                AnomaliesListBox.ItemsSource = anomalies;
                NoAnomaliesText.Visibility = Visibility.Collapsed;
            }
            else
            {
                AnomaliesListBox.ItemsSource = null;
                NoAnomaliesText.Visibility = Visibility.Visible;
            }

            AnomaliesCard.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Show all analysis cards
        /// </summary>
        private void ShowAllCards()
        {
            EntropyCard.Visibility = Visibility.Visible;
            DistributionCard.Visibility = Visibility.Visible;
            PatternsCard.Visibility = Visibility.Visible;
            AnomaliesCard.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Show no data message
        /// </summary>
        private void ShowNoDataMessage()
        {
            StatusTextBlock.Text = "No data to analyze. Select a range in the hex editor.";
            EntropyCard.Visibility = Visibility.Collapsed;
            DistributionCard.Visibility = Visibility.Collapsed;
            PatternsCard.Visibility = Visibility.Collapsed;
            AnomaliesCard.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Analyze button click handler
        /// </summary>
        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            // Trigger analysis request event
            AnalysisRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event raised when user requests analysis
        /// </summary>
        public event EventHandler AnalysisRequested;
    }

    /// <summary>
    /// Pattern information
    /// </summary>
    public class PatternInfo
    {
        public string Icon { get; set; }
        public string Pattern { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Byte array comparer for dictionary keys
    /// </summary>
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            if (x == null || y == null) return x == y;
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++)
                if (x[i] != y[i]) return false;
            return true;
        }

        public int GetHashCode(byte[] obj)
        {
            if (obj == null) return 0;
            int hash = 17;
            foreach (var b in obj)
                hash = hash * 31 + b;
            return hash;
        }
    }
}
