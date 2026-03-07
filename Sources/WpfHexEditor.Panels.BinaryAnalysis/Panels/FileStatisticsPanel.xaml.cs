//////////////////////////////////////////////
// Apache 2.0  - 2026
// File Statistics Dashboard
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Panels.BinaryAnalysis
{
    /// <summary>
    /// Dashboard for file health and statistics.
    /// </summary>
    public partial class FileStatisticsPanel : UserControl
    {
        /// <summary>Raised when the user clicks the Refresh button — host should re-run the analysis.</summary>
        public event EventHandler? RefreshRequested;

        public FileStatisticsPanel()
        {
            InitializeComponent();
        }

        // -- Public API ------------------------------------------------------------

        /// <summary>
        /// Update all displayed statistics from the provided <see cref="FileStats"/> snapshot.
        /// </summary>
        public void UpdateStatistics(FileStats stats)
        {
            if (stats == null) return;

            // -- File info --
            FileNameText.Text    = string.IsNullOrEmpty(stats.FileName) ? "No file loaded" : stats.FileName;
            FilePathText.Text    = stats.FilePath ?? string.Empty;
            FileSizeText.Text    = FormatFileSize(stats.FileSize);
            FormatNameText.Text  = string.IsNullOrEmpty(stats.FormatName) ? "Unknown" : stats.FormatName;
            AnalysisDateText.Text = stats.AnalysisDate == default
                ? "—"
                : stats.AnalysisDate.ToString("yyyy-MM-dd HH:mm:ss");

            // -- Data type badge --
            var dtLabel = string.IsNullOrEmpty(stats.DataType) ? "Unknown" : stats.DataType;
            DataTypeText.Text    = dtLabel;
            DataTypeDescText.Text = GetDataTypeDescription(dtLabel);

            // -- Byte composition --
            NullBar.Value       = stats.NullBytePercentage;
            NullPctText.Text    = $"{stats.NullBytePercentage:F1}%";

            AsciiBar.Value      = stats.PrintableAsciiPercentage;
            AsciiPctText.Text   = $"{stats.PrintableAsciiPercentage:F1}%";

            var otherPct = Math.Max(0, 100.0 - stats.NullBytePercentage - stats.PrintableAsciiPercentage);
            BinaryBar.Value     = otherPct;
            BinaryPctText.Text  = $"{otherPct:F1}%";

            MostCommonByteText.Text = $"0x{stats.MostCommonByte:X2} ({stats.MostCommonBytePct:F1}%)";
            UniqueCountText.Text    = $"{stats.UniqueBytesCount} / 256";

            // -- Health score --
            HealthScoreBar.Value  = stats.HealthScore;
            HealthScoreText.Text  = $"{stats.HealthScore}/100";
            HealthStatusText.Text = stats.HealthMessage ?? string.Empty;

            // -- Entropy --
            EntropyBar.Value  = (stats.Entropy / 8.0) * 100;
            EntropyText.Text  = $"{stats.Entropy:F2}/8.0";
            EntropyDescText.Text = GetEntropyDescription(stats.Entropy);

            // -- Validation --
            StructureIcon.Text    = stats.StructureValid ? "✅" : "⚠️";
            StructureStatus.Text  = stats.StructureValid ? "Valid" : "Invalid";
            ChecksumIcon.Text     = stats.ChecksumsPass  ? "✅" : "⚠️";
            ChecksumStatus.Text   = stats.ChecksumStatus ?? "N/A";

            // -- Anomalies --
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

        // -- Event handlers --------------------------------------------------------

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
            => RefreshRequested?.Invoke(this, EventArgs.Empty);

        // -- Helpers ---------------------------------------------------------------

        private static string FormatFileSize(long size)
        {
            if (size < 1024)                   return $"{size} bytes";
            if (size < 1024 * 1024)            return $"{size / 1024.0:F1} KB";
            if (size < 1024L * 1024 * 1024)   return $"{size / (1024.0 * 1024):F1} MB";
            return                                    $"{size / (1024.0 * 1024 * 1024):F1} GB";
        }

        private static string GetEntropyDescription(double entropy)
        {
            if (entropy < 1.0)  return "Highly structured / repetitive data";
            if (entropy < 3.0)  return "Low entropy — sparse or repetitive content";
            if (entropy < 5.5)  return "Medium entropy — mixed content";
            if (entropy < 7.0)  return "High entropy — compressed or binary data";
            return                     "Very high entropy — likely compressed or encrypted";
        }

        private static string GetDataTypeDescription(string dataType) => dataType switch
        {
            "Text"        => "Mostly printable ASCII characters",
            "Binary"      => "General binary data",
            "Compressed"  => "High entropy suggests compression (ZIP, GZIP, etc.)",
            "Encrypted"   => "Near-random byte distribution — likely encrypted",
            "Sparse"      => "Large proportion of null / zero bytes",
            "Image"       => "Byte patterns consistent with image data",
            "Executable"  => "Executable file signature detected (PE, ELF, etc.)",
            _             => string.Empty
        };
    }

    // -- Data model ----------------------------------------------------------------

    /// <summary>
    /// Snapshot of file statistics passed to <see cref="FileStatisticsPanel.UpdateStatistics"/>.
    /// </summary>
    public class FileStats
    {
        // File identity
        public string?   FileName   { get; set; }
        public string?   FilePath   { get; set; }
        public DateTime  AnalysisDate { get; set; }

        // Size / format
        public long      FileSize   { get; set; }
        public int       FieldCount { get; set; }
        public string?   FormatName { get; set; }

        // Data type
        public string?   DataType   { get; set; }

        // Byte composition
        public byte      MostCommonByte        { get; set; }
        public double    MostCommonBytePct     { get; set; }
        public int       UniqueBytesCount      { get; set; }
        public double    NullBytePercentage    { get; set; }
        public double    PrintableAsciiPercentage { get; set; }

        // Entropy
        public double    Entropy    { get; set; }

        // Health
        public int       HealthScore   { get; set; }
        public string?   HealthMessage { get; set; }

        // Validation
        public bool      StructureValid  { get; set; }
        public bool      ChecksumsPass   { get; set; }
        public string?   ChecksumStatus  { get; set; }

        // Compression (kept for compatibility)
        public double    CompressionRatio { get; set; }

        // Anomalies
        public List<AnomalyInfo>? Anomalies { get; set; }
    }

    public class AnomalyInfo
    {
        public string? Title       { get; set; }
        public string? Description { get; set; }
    }
}
