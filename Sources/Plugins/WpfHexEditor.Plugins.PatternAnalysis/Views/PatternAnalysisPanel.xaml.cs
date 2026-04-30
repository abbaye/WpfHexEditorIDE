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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfHexEditor.Plugins.PatternAnalysis.Properties;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.PatternAnalysis.Views;

/// <summary>
/// Panel for analyzing byte patterns, entropy, and anomalies.
/// </summary>
public partial class PatternAnalysisPanel : UserControl
{
    private ToolbarOverflowManager _overflowManager = null!;

    public PatternAnalysisPanel()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            _overflowManager = new ToolbarOverflowManager(
                toolbarContainer:      ToolbarBorder,
                alwaysVisiblePanel:    ToolbarRightPanel,
                overflowButton:        ToolbarOverflowButton,
                overflowMenu:          OverflowContextMenu,
                groupsInCollapseOrder: new FrameworkElement[] { TbgPatternRefresh });
            Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
        };
    }

    // -- Public API -----------------------------------------------------------

    /// <summary>
    /// Analyzes the provided byte array asynchronously.
    /// Heavy computation (entropy, distributions, patterns) runs on a background thread
    /// so the UI dispatcher stays responsive during analysis.
    /// </summary>
    public async void AnalyzeAsync(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            ShowNoDataMessage();
            return;
        }

        StatusTextBlock.Text = $"Analyzing {data.Length:N0} bytes...";

        try
        {
            // All heavy computation runs off the UI thread.
            var result = await Task.Run(() =>
            {
                var dist = CalculateByteDistribution(data);
                return (
                    Entropy      : CalculateEntropy(data),
                    Distribution : dist,
                    Patterns     : DetectPatterns(data),
                    Anomalies    : DetectAnomalies(data, dist)
                );
            });

            // async/await resumes on the UI SynchronizationContext.
            UpdateEntropyCard(result.Entropy);
            UpdateDistributionCard(result.Distribution);
            UpdatePatternsCard(result.Patterns);
            UpdateAnomaliesCard(result.Anomalies);

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
            // FindRepeatedSequences uses uint keys to avoid allocating a byte[] per iteration.
            var repeats = FindRepeatedSequences(data, 4);
            if (repeats.Count > 0)
            {
                var top = repeats.First();
                // Reconstruct the 4-byte pattern from the packed uint for display only.
                var bytes = new byte[4];
                bytes[0] = (byte)(top.Key & 0xFF);
                bytes[1] = (byte)((top.Key >> 8)  & 0xFF);
                bytes[2] = (byte)((top.Key >> 16) & 0xFF);
                bytes[3] = (byte)((top.Key >> 24) & 0xFF);
                patterns.Add(new PatternInfo
                {
                    Icon        = "\U0001F501",
                    Pattern     = BitConverter.ToString(bytes).Replace("-", " "),
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

    /// <summary>
    /// Counts repeated 4-byte sequences using a uint key (bytes packed little-endian).
    /// Avoids allocating a byte[] per iteration — previously caused ~1 M small heap allocs
    /// per 1 MB file (GC pressure, visible memory sawtooth in Plugin Monitor).
    /// </summary>
    private static Dictionary<uint, int> FindRepeatedSequences(byte[] data, int seqLen)
    {
        var counts = new Dictionary<uint, int>();
        int limit  = data.Length - seqLen;
        for (int i = 0; i <= limit; i++)
        {
            // Pack 4 bytes into a uint (little-endian) — zero-alloc key.
            uint key = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
            counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
        }
        return counts.Where(kvp => kvp.Value > 2)
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
                Title       = PatternAnalysisResources.PatternAnalysis_ExtremelySkewed,
                Description = $"Byte 0x{dominant:X2} appears {maxFreq * 100.0 / data.Length:F1}% of the time"
            });
        }

        var entropy = CalculateEntropy(data);
        if (entropy > 7.5)
            anomalies.Add(new AnomalyInfo
            {
                Title       = PatternAnalysisResources.PatternAnalysis_Entropy_VeryHigh,
                Description = PatternAnalysisResources.PatternAnalysis_Entropy_VeryHigh_Desc
            });

        if (entropy < 2.0)
            anomalies.Add(new AnomalyInfo
            {
                Title       = PatternAnalysisResources.PatternAnalysis_Entropy_VeryLow,
                Description = PatternAnalysisResources.PatternAnalysis_Entropy_VeryLow_Desc
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
            EntropyInterpretation.Text  = PatternAnalysisResources.PatternAnalysis_Entropy_Low;
        }
        else if (entropy < 6.0)
        {
            EntropyBar.Background       = (SolidColorBrush)FindResource("MediumEntropyBrush");
            EntropyInterpretation.Text  = PatternAnalysisResources.PatternAnalysis_Entropy_Medium;
        }
        else
        {
            EntropyBar.Background       = (SolidColorBrush)FindResource("HighEntropyBrush");
            EntropyInterpretation.Text  = PatternAnalysisResources.PatternAnalysis_Entropy_High;
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
        StatusTextBlock.Text        = PatternAnalysisResources.PatternAnalysis_NoData;
        EntropyCard.Visibility      = Visibility.Collapsed;
        DistributionCard.Visibility = Visibility.Collapsed;
        PatternsCard.Visibility     = Visibility.Collapsed;
        AnomaliesCard.Visibility    = Visibility.Collapsed;
    }

    // -- Toolbar handlers -----------------------------------------------------

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        => AnalysisRequested?.Invoke(this, EventArgs.Empty);

    // ── Toolbar overflow ─────────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowContextMenu.PlacementTarget = ToolbarOverflowButton;
        OverflowContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        OverflowContextMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        _overflowManager?.SyncMenuVisibility();
    }
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

