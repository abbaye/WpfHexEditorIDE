// ==========================================================
// Project: WpfHexEditor.Plugins.FileStatistics
// File: FileStatisticsPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     File health dashboard panel migrated from Panels.BinaryAnalysis.
//     Displays file info, byte composition, health score, entropy, and anomalies.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.FileStatistics.Views;

/// <summary>
/// Dashboard for file health and statistics.
/// </summary>
public partial class FileStatisticsPanel : UserControl
{
    /// <summary>Raised when the user clicks the Refresh button.</summary>
    public event EventHandler? RefreshRequested;

    public FileStatisticsPanel()
    {
        InitializeComponent();
    }

    // -- Public API -----------------------------------------------------------

    /// <summary>
    /// Update all displayed statistics from the provided <see cref="FileStats"/> snapshot.
    /// </summary>
    public void UpdateStatistics(FileStats stats)
    {
        if (stats == null) return;

        FileNameText.Text     = string.IsNullOrEmpty(stats.FileName) ? "No file loaded" : stats.FileName;
        FilePathText.Text     = stats.FilePath ?? string.Empty;
        FileSizeText.Text     = FormatFileSize(stats.FileSize);
        FormatNameText.Text   = string.IsNullOrEmpty(stats.FormatName) ? "Unknown" : stats.FormatName;
        AnalysisDateText.Text = stats.AnalysisDate == default
            ? "\u2014"
            : stats.AnalysisDate.ToString("yyyy-MM-dd HH:mm:ss");

        var dtLabel = string.IsNullOrEmpty(stats.DataType) ? "Unknown" : stats.DataType;
        DataTypeText.Text     = dtLabel;
        DataTypeDescText.Text = GetDataTypeDescription(dtLabel);

        NullBar.Value      = stats.NullBytePercentage;
        NullPctText.Text   = $"{stats.NullBytePercentage:F1}%";
        AsciiBar.Value     = stats.PrintableAsciiPercentage;
        AsciiPctText.Text  = $"{stats.PrintableAsciiPercentage:F1}%";

        var otherPct       = Math.Max(0, 100.0 - stats.NullBytePercentage - stats.PrintableAsciiPercentage);
        BinaryBar.Value    = otherPct;
        BinaryPctText.Text = $"{otherPct:F1}%";

        MostCommonByteText.Text = $"0x{stats.MostCommonByte:X2} ({stats.MostCommonBytePct:F1}%)";
        UniqueCountText.Text    = $"{stats.UniqueBytesCount} / 256";

        HealthScoreBar.Value  = stats.HealthScore;
        HealthScoreText.Text  = $"{stats.HealthScore}/100";
        HealthStatusText.Text = stats.HealthMessage ?? string.Empty;

        EntropyBar.Value     = (stats.Entropy / 8.0) * 100;
        EntropyText.Text     = $"{stats.Entropy:F2}/8.0";
        EntropyDescText.Text = GetEntropyDescription(stats.Entropy);

        StructureIcon.Text   = stats.StructureValid ? "\u2705" : "\u26A0\uFE0F";
        StructureStatus.Text = stats.StructureValid ? "Valid" : "Invalid";
        ChecksumIcon.Text    = stats.ChecksumsPass  ? "\u2705" : "\u26A0\uFE0F";
        ChecksumStatus.Text  = stats.ChecksumStatus ?? "N/A";

        if (stats.Anomalies?.Count > 0)
        {
            AnomaliesList.ItemsSource  = stats.Anomalies;
            AnomaliesList.Visibility   = Visibility.Visible;
            NoAnomaliesText.Visibility = Visibility.Collapsed;
        }
        else
        {
            AnomaliesList.Visibility   = Visibility.Collapsed;
            NoAnomaliesText.Visibility = Visibility.Visible;
        }
    }

    // -- Event handlers -------------------------------------------------------

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
        => RefreshRequested?.Invoke(this, EventArgs.Empty);

    // -- Helpers --------------------------------------------------------------

    private static string FormatFileSize(long size)
    {
        if (size < 1024)                 return $"{size} bytes";
        if (size < 1024 * 1024)          return $"{size / 1024.0:F1} KB";
        if (size < 1024L * 1024 * 1024) return $"{size / (1024.0 * 1024):F1} MB";
        return                                  $"{size / (1024.0 * 1024 * 1024):F1} GB";
    }

    private static string GetEntropyDescription(double entropy)
    {
        if (entropy < 1.0) return "Highly structured / repetitive data";
        if (entropy < 3.0) return "Low entropy \u2014 sparse or repetitive content";
        if (entropy < 5.5) return "Medium entropy \u2014 mixed content";
        if (entropy < 7.0) return "High entropy \u2014 compressed or binary data";
        return                    "Very high entropy \u2014 likely compressed or encrypted";
    }

    private static string GetDataTypeDescription(string dataType) => dataType switch
    {
        "Text"       => "Mostly printable ASCII characters",
        "Binary"     => "General binary data",
        "Compressed" => "High entropy suggests compression (ZIP, GZIP, etc.)",
        "Encrypted"  => "Near-random byte distribution \u2014 likely encrypted",
        "Sparse"     => "Large proportion of null / zero bytes",
        "Image"      => "Byte patterns consistent with image data",
        "Executable" => "Executable file signature detected (PE, ELF, etc.)",
        _            => string.Empty
    };
}

// -- Data model ---------------------------------------------------------------

/// <summary>
/// Snapshot of file statistics passed to <see cref="FileStatisticsPanel.UpdateStatistics"/>.
/// </summary>
public class FileStats
{
    public string?          FileName              { get; set; }
    public string?          FilePath              { get; set; }
    public DateTime         AnalysisDate          { get; set; }
    public long             FileSize              { get; set; }
    public int              FieldCount            { get; set; }
    public string?          FormatName            { get; set; }
    public string?          DataType              { get; set; }
    public byte             MostCommonByte        { get; set; }
    public double           MostCommonBytePct     { get; set; }
    public int              UniqueBytesCount      { get; set; }
    public double           NullBytePercentage    { get; set; }
    public double           PrintableAsciiPercentage { get; set; }
    public double           Entropy               { get; set; }
    public int              HealthScore           { get; set; }
    public string?          HealthMessage         { get; set; }
    public bool             StructureValid        { get; set; }
    public bool             ChecksumsPass         { get; set; }
    public string?          ChecksumStatus        { get; set; }
    public double           CompressionRatio      { get; set; }
    public List<AnomalyInfo>? Anomalies           { get; set; }
}

public class AnomalyInfo
{
    public string? Title       { get; set; }
    public string? Description { get; set; }
}
