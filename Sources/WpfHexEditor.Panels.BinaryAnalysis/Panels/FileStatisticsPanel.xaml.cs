//////////////////////////////////////////////
// Apache 2.0  - 2026
// File Statistics Dashboard
// Author : Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Panels.BinaryAnalysis
{
    /// <summary>
    /// Dashboard for file health and statistics
    /// </summary>
    public partial class FileStatisticsPanel : UserControl
    {
        public FileStatisticsPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Update statistics
        /// </summary>
        public void UpdateStatistics(FileStats stats)
        {
            if (stats == null) return;

            HealthScoreBar.Value = stats.HealthScore;
            HealthScoreText.Text = $"{stats.HealthScore}/100";
            HealthStatusText.Text = stats.HealthMessage;

            StructureStatus.Text = stats.StructureValid ? "Valid" : "Invalid";
            ChecksumStatus.Text = stats.ChecksumStatus;
            ChecksumIcon.Text = stats.ChecksumsPass ? "✅" : "⚠️";

            CompressionBar.Value = stats.CompressionRatio;
            CompressionText.Text = $"{stats.CompressionRatio}%";

            EntropyBar.Value = (stats.Entropy / 8.0) * 100;
            EntropyText.Text = $"{stats.Entropy:F1}/8.0";

            FileSizeText.Text = FormatFileSize(stats.FileSize);
            FieldCountText.Text = stats.FieldCount.ToString();
            FormatNameText.Text = stats.FormatName ?? "Unknown";

            if (stats.Anomalies?.Count > 0)
            {
                AnomaliesList.ItemsSource = stats.Anomalies;
                AnomaliesList.Visibility = Visibility.Visible;
                NoAnomaliesText.Visibility = Visibility.Collapsed;
            }
            else
            {
                AnomaliesList.Visibility = Visibility.Collapsed;
                NoAnomaliesText.Visibility = Visibility.Visible;
            }
        }

        private string FormatFileSize(long size)
        {
            if (size < 1024) return $"{size} bytes";
            if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
            if (size < 1024 * 1024 * 1024) return $"{size / (1024.0 * 1024):F1} MB";
            return $"{size / (1024.0 * 1024 * 1024):F1} GB";
        }
    }

    /// <summary>
    /// File statistics data
    /// </summary>
    public class FileStats
    {
        public int HealthScore { get; set; }
        public string HealthMessage { get; set; }
        public bool StructureValid { get; set; }
        public bool ChecksumsPass { get; set; }
        public string ChecksumStatus { get; set; }
        public double CompressionRatio { get; set; }
        public double Entropy { get; set; }
        public long FileSize { get; set; }
        public int FieldCount { get; set; }
        public string FormatName { get; set; }
        public List<AnomalyInfo> Anomalies { get; set; }
    }

    public class AnomalyInfo
    {
        public string Title { get; set; }
        public string Description { get; set; }
    }
}
