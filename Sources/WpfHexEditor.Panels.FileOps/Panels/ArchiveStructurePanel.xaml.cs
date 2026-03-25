//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Archive Structure Tree View
// Author : Claude Sonnet 4.5
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Panels.FileOps
{
    public partial class ArchiveStructurePanel : UserControl
    {
        public ArchiveStructurePanel()
        {
            InitializeComponent();
        }

        public void LoadArchive(ArchiveNode root)
        {
            if (root == null)
            {
                ArchiveInfoText.Text = "No archive loaded";
                StructureTreeView.ItemsSource = null;
                return;
            }

            var items = new ObservableCollection<ArchiveNode> { root };
            StructureTreeView.ItemsSource = items;

            var stats = CalculateStats(root);
            ArchiveInfoText.Text = $"{root.Name} ({stats.TotalFiles} files, {FormatSize(stats.TotalSize)})";
            FileCountText.Text = $"{stats.TotalFiles} files";
            FolderCountText.Text = $"{stats.TotalFolders} folders";
            TotalSizeText.Text = FormatSize(stats.TotalSize);

            if (stats.CompressedSize > 0 && stats.TotalSize > 0)
            {
                var ratio = 100.0 * stats.CompressedSize / stats.TotalSize;
                CompressionRatioText.Text = $"Ratio: {ratio:F1}%";
            }
        }

        private ArchiveStats CalculateStats(ArchiveNode node)
        {
            var stats = new ArchiveStats();
            CalculateStatsRecursive(node, stats);
            return stats;
        }

        private void CalculateStatsRecursive(ArchiveNode node, ArchiveStats stats)
        {
            if (node.IsFolder)
            {
                stats.TotalFolders++;
                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                        CalculateStatsRecursive(child, stats);
                }
            }
            else
            {
                stats.TotalFiles++;
                stats.TotalSize += node.Size;
                stats.CompressedSize += node.CompressedSize;
            }
        }

        private string FormatSize(long size)
        {
            if (size < 1024) return $"{size} B";
            if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
            if (size < 1024 * 1024 * 1024) return $"{size / (1024.0 * 1024):F1} MB";
            return $"{size / (1024.0 * 1024 * 1024):F1} GB";
        }

        private void StructureTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Handle selection
        }

        private void Extract_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Extract functionality - to be implemented with archive library",
                "Extract", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (StructureTreeView.SelectedItem is ArchiveNode node)
            {
                var details = $"Name: {node.Name}\n" +
                             $"Type: {(node.IsFolder ? "Folder" : "File")}\n" +
                             $"Size: {FormatSize(node.Size)}\n" +
                             $"Compressed: {FormatSize(node.CompressedSize)}\n" +
                             $"CRC: {node.Crc}\n" +
                             $"Method: {node.CompressionMethod}";
                MessageBox.Show(details, "Details", MessageBoxButton.OK);
            }
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            ExpandCollapseAll(true);
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            ExpandCollapseAll(false);
        }

        private void ExpandCollapseAll(bool expand)
        {
            if (StructureTreeView.ItemsSource is ObservableCollection<ArchiveNode> items)
            {
                foreach (var item in items)
                    SetExpandedRecursive(item, expand);
            }
        }

        private void SetExpandedRecursive(ArchiveNode node, bool expanded)
        {
            node.IsExpanded = expanded;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                    SetExpandedRecursive(child, expanded);
            }
        }
    }

    public class ArchiveNode : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public string Name { get; set; }
        public bool IsFolder { get; set; }
        public long Size { get; set; }
        public long CompressedSize { get; set; }
        public string Crc { get; set; }
        public string CompressionMethod { get; set; }
        public ObservableCollection<ArchiveNode> Children { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public string Icon => IsFolder ? "ðŸ“" : GetFileIcon();
        public string SizeInfo => IsFolder ? "" : $"({FormatSize(Size)})";
        public string StatusIcon => Crc != null && Crc.Contains("FAIL") ? "âš ï¸" : "";

        private string GetFileIcon()
        {
            var ext = System.IO.Path.GetExtension(Name)?.ToLowerInvariant();
            return ext switch
            {
                ".txt" or ".md" => "ðŸ“„",
                ".jpg" or ".png" or ".gif" => "ðŸ–¼ï¸",
                ".zip" or ".rar" or ".7z" => "ðŸ“¦",
                ".exe" or ".dll" => "âš™ï¸",
                ".mp3" or ".wav" => "ðŸŽµ",
                ".mp4" or ".avi" => "ðŸŽ¬",
                _ => "ðŸ“„"
            };
        }

        private string FormatSize(long size)
        {
            if (size < 1024) return $"{size} B";
            if (size < 1024 * 1024) return $"{size / 1024.0:F0} KB";
            return $"{size / (1024.0 * 1024):F1} MB";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ArchiveStats
    {
        public int TotalFiles { get; set; }
        public int TotalFolders { get; set; }
        public long TotalSize { get; set; }
        public long CompressedSize { get; set; }
    }
}
