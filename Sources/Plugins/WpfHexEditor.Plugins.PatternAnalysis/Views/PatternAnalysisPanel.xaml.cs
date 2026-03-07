// ==========================================================
// Project: WpfHexEditor.Plugins.PatternAnalysis
// File: PatternAnalysisPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Pattern analysis panel migrated from Panels.BinaryAnalysis.
//     Analyzes entropy, byte distribution, patterns, and anomalies.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfHexEditor.Plugins.PatternAnalysis.Views;

/// <summary>
/// Panel for analyzing byte patterns, entropy, and anomalies.
/// </summary>
public partial class PatternAnalysisPanel : UserControl
{
    private byte[]? _analysisData;

    public PatternAnalysisPanel()
    {
        InitializeComponent();
    }

    // -- Public API -----------------------------------------------------------

    /// <summary>Analyzes the provided byte array and updates the UI.</summary>
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
            var entropy      = CalculateEntropy(data);
            var distribution = CalculateByteDistribution(data);
            var patterns     = DetectPatterns(data);
            var anomalies    = DetectAnomalies(data, distribution);

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

    /// <summary>Raised when the user requests a new analysis.</summary>
    public event EventHandler? AnalysisRequested;

    // -- Analysis algorithms --------------------------------------------------

    private static double CalculateEntropy(byte[] data)
    {
        if (data.Length == 0) return 0;

        var frequency = new int[256];
        foreach (var b in data) frequency[b]++;

        double entropy = 0;
        foreach (var count in frequency)
        {
            if (count == 0) continue;
            double p = (double)count / data.Length;
            entropy -= p * Math.Log(p, 2);
        }
        return entropy;
    }

    private static int[] CalculateByteDistribution(byte[] data)
    {
        var dist = new int[256];
        foreach (var b in data) dist[b]++;
        return dist;
    }

    private List<PatternInfo> DetectPatterns(byte[] data)
    {
        var patterns = new List<PatternInfo>();

        int nullCount = data.Count(b => b == 0x00);
        if (nullCount > data.Length * 0.3)
            patterns.Add(new PatternInfo
            {
                Icon        = "\U0001F532",
                Pattern     = "NULL bytes",
                Description = $"{nullCount:N0} null bytes ({nullCount * 100.0 / data.Length:F1}% of data)"
            });

        if (data.Length > 4)
        {
            var repeats = FindRepeatedSequences(data, 4);
            if (repeats.Count > 0)
            {
                var top = repeats.First();
                patterns.Add(new PatternInfo
                {
                    Icon        = "\U0001F501",
                    Pattern     = BitConverter.ToString(top.Key).Replace("-", " "),
                    Description = $"Repeated {top.Value} times"
                });
            }
        }

        int asciiCount = data.Count(b => b >= 0x20 && b < 0x7F);
        if (asciiCount > data.Length * 0.7)
            patterns.Add(new PatternInfo
            {
                Icon        = "\U0001F4DD",
                Pattern     = "ASCII text",
                Description = $"{asciiCount * 100.0 / data.Length:F1}% printable ASCII characters"
            });

        if (data.Length % 4 == 0 && data.Length >= 16)
            patterns.Add(new PatternInfo
            {
                Icon        = "\U0001F4D0",
                Pattern     = "4-byte aligned",
                Description = "Data is aligned to 4-byte boundaries (typical for structured data)"
            });

        return patterns;
    }

    private static Dictionary<byte[], int> FindRepeatedSequences(byte[] data, int seqLen)
    {
        var sequences = new Dictionary<byte[], int>(new ByteArrayComparer());
        for (int i = 0; i <= data.Length - seqLen; i++)
        {
            var seq = new byte[seqLen];
            Array.Copy(data, i, seq, 0, seqLen);
            sequences[seq] = sequences.TryGetValue(seq, out int c) ? c + 1 : 1;
        }
        return sequences.Where(kvp => kvp.Value > 2)
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(5)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private List<AnomalyInfo> DetectAnomalies(byte[] data, int[] distribution)
    {
        var anomalies = new List<AnomalyInfo>();

        var maxFreq = distribution.Max();
        if (maxFreq > data.Length * 0.9)
        {
            var dominant = Array.IndexOf(distribution, maxFreq);
            anomalies.Add(new AnomalyInfo
            {
                Title       = "Extremely skewed distribution",
                Description = $"Byte 0x{dominant:X2} appears {maxFreq * 100.0 / data.Length:F1}% of the time"
            });
        }

        var entropy = CalculateEntropy(data);
        if (entropy > 7.5)
            anomalies.Add(new AnomalyInfo
            {
                Title       = "Very high entropy detected",
                Description = "Data may be encrypted or compressed (entropy > 7.5 bits/byte)"
            });

        if (entropy < 2.0)
            anomalies.Add(new AnomalyInfo
            {
                Title       = "Very low entropy detected",
                Description = "Data contains mostly repetitive or zero bytes (entropy < 2.0 bits/byte)"
            });

        return anomalies;
    }

    // -- UI updates -----------------------------------------------------------

    private void UpdateEntropyCard(double entropy)
    {
        EntropyValueText.Text = entropy.ToString("F2");

        var pct = entropy / 8.0;
        EntropyBar.Width = ActualWidth > 0 ? (ActualWidth - 48) * pct : 100 * pct;

        if (entropy < 3.0)
        {
            EntropyBar.Background       = (SolidColorBrush)FindResource("LowEntropyBrush");
            EntropyInterpretation.Text  = "Low randomness \u2014 data is repetitive or structured";
        }
        else if (entropy < 6.0)
        {
            EntropyBar.Background       = (SolidColorBrush)FindResource("MediumEntropyBrush");
            EntropyInterpretation.Text  = "Medium randomness \u2014 typical for mixed or compressed data";
        }
        else
        {
            EntropyBar.Background       = (SolidColorBrush)FindResource("HighEntropyBrush");
            EntropyInterpretation.Text  = "High randomness \u2014 data may be encrypted or highly compressed";
        }

        EntropyCard.Visibility = Visibility.Visible;
    }

    private void UpdateDistributionCard(int[] distribution)
    {
        HistogramCanvas.Children.Clear();

        if (HistogramCanvas.ActualWidth == 0 || HistogramCanvas.ActualHeight == 0)
            HistogramCanvas.Loaded += (_, _) => DrawHistogram(distribution);
        else
            DrawHistogram(distribution);

        var maxFreq   = distribution.Max();
        var maxByte   = Array.IndexOf(distribution, maxFreq);
        MostFrequentByteText.Text = $"0x{maxByte:X2} ({maxFreq:N0} times)";

        var unique    = distribution.Count(c => c > 0);
        UniqueBytesText.Text = $"{unique} / 256";

        DistributionCard.Visibility = Visibility.Visible;
    }

    private void DrawHistogram(int[] distribution)
    {
        if (distribution == null || HistogramCanvas.ActualWidth == 0) return;

        HistogramCanvas.Children.Clear();

        var maxFreq = distribution.Max();
        if (maxFreq == 0) return;

        var w        = HistogramCanvas.ActualWidth;
        var h        = HistogramCanvas.ActualHeight;
        var barWidth = w / 256.0;

        for (int i = 0; i < 256; i++)
        {
            if (distribution[i] == 0) continue;

            var barH = (distribution[i] / (double)maxFreq) * h;
            var rect = new Rectangle
            {
                Width   = Math.Max(barWidth, 1),
                Height  = barH,
                Fill    = new SolidColorBrush(Color.FromRgb(74, 144, 226)),
                ToolTip = $"0x{i:X2}: {distribution[i]:N0} bytes"
            };

            Canvas.SetLeft(rect, i * barWidth);
            Canvas.SetBottom(rect, 0);
            HistogramCanvas.Children.Add(rect);
        }
    }

    private void UpdatePatternsCard(List<PatternInfo> patterns)
    {
        if (patterns.Count > 0)
        {
            PatternsListBox.ItemsSource = patterns;
            NoPatternsText.Visibility   = Visibility.Collapsed;
        }
        else
        {
            PatternsListBox.ItemsSource = null;
            NoPatternsText.Visibility   = Visibility.Visible;
        }
        PatternsCard.Visibility = Visibility.Visible;
    }

    private void UpdateAnomaliesCard(List<AnomalyInfo> anomalies)
    {
        if (anomalies.Count > 0)
        {
            AnomaliesListBox.ItemsSource = anomalies;
            NoAnomaliesText.Visibility   = Visibility.Collapsed;
        }
        else
        {
            AnomaliesListBox.ItemsSource = null;
            NoAnomaliesText.Visibility   = Visibility.Visible;
        }
        AnomaliesCard.Visibility = Visibility.Visible;
    }

    private void ShowAllCards()
    {
        EntropyCard.Visibility     = Visibility.Visible;
        DistributionCard.Visibility = Visibility.Visible;
        PatternsCard.Visibility    = Visibility.Visible;
        AnomaliesCard.Visibility   = Visibility.Visible;
    }

    private void ShowNoDataMessage()
    {
        StatusTextBlock.Text        = "No data to analyze. Select a range in the hex editor.";
        EntropyCard.Visibility      = Visibility.Collapsed;
        DistributionCard.Visibility = Visibility.Collapsed;
        PatternsCard.Visibility     = Visibility.Collapsed;
        AnomaliesCard.Visibility    = Visibility.Collapsed;
    }

    // -- Toolbar handlers -----------------------------------------------------

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        => AnalysisRequested?.Invoke(this, EventArgs.Empty);
}

// -- Supporting data types ----------------------------------------------------

public class PatternInfo
{
    public string? Icon        { get; set; }
    public string? Pattern     { get; set; }
    public string? Description { get; set; }
}

public class AnomalyInfo
{
    public string? Title       { get; set; }
    public string? Description { get; set; }
}

/// <summary>Byte array equality comparer for dictionary keys.</summary>
public class ByteArrayComparer : IEqualityComparer<byte[]>
{
    public bool Equals(byte[]? x, byte[]? y)
    {
        if (x == null || y == null) return x == y;
        if (x.Length != y.Length)   return false;
        for (int i = 0; i < x.Length; i++)
            if (x[i] != y[i]) return false;
        return true;
    }

    public int GetHashCode(byte[] obj)
    {
        if (obj == null) return 0;
        int hash = 17;
        foreach (var b in obj) hash = hash * 31 + b;
        return hash;
    }
}
