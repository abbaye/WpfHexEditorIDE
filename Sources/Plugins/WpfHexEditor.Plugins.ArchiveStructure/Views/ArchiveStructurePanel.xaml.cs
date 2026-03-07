// ==========================================================
// Project: WpfHexEditor.Plugins.ArchiveStructure
// File: ArchiveStructurePanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Archive structure tree-view panel migrated from Panels.FileOps.
//     Displays hierarchical archive contents with stats.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.ArchiveStructure.Views;

/// <summary>
/// Panel displaying the hierarchical structure of an archive file.
/// </summary>
public partial class ArchiveStructurePanel : UserControl
{
    public ArchiveStructurePanel()
    {
        InitializeComponent();
    }

    // -- Public API -----------------------------------------------------------

    /// <summary>Loads and displays an archive root node.</summary>
    public void LoadArchive(ArchiveNode? root)
    {
        if (root is null)
        {
            ArchiveInfoText.Text        = "No archive loaded";
            StructureTreeView.ItemsSource = null;
            return;
        }

        StructureTreeView.ItemsSource = new ObservableCollection<ArchiveNode> { root };

        var stats = CalculateStats(root);
        ArchiveInfoText.Text     = $"{root.Name} ({stats.TotalFiles} files, {FormatSize(stats.TotalSize)})";
        FileCountText.Text       = $"{stats.TotalFiles} files";
        FolderCountText.Text     = $"{stats.TotalFolders} folders";
        TotalSizeText.Text       = FormatSize(stats.TotalSize);

        if (stats.CompressedSize > 0 && stats.TotalSize > 0)
            CompressionRatioText.Text = $"Ratio: {100.0 * stats.CompressedSize / stats.TotalSize:F1}%";
    }

    // -- Event handlers -------------------------------------------------------

    private void StructureTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { }

    private void Extract_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show("Extract functionality requires an archive library.",
            "Extract", MessageBoxButton.OK, MessageBoxImage.Information);

    private void ViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (StructureTreeView.SelectedItem is not ArchiveNode node) return;

        MessageBox.Show(
            $"Name: {node.Name}\nType: {(node.IsFolder ? "Folder" : "File")}\n" +
            $"Size: {FormatSize(node.Size)}\nCompressed: {FormatSize(node.CompressedSize)}\n" +
            $"CRC: {node.Crc}\nMethod: {node.CompressionMethod}",
            "Details", MessageBoxButton.OK);
    }

    private void ExpandAll_Click(object sender, RoutedEventArgs e)   => SetExpandedAll(true);
    private void CollapseAll_Click(object sender, RoutedEventArgs e) => SetExpandedAll(false);

    private void SetExpandedAll(bool expanded)
    {
        if (StructureTreeView.ItemsSource is not ObservableCollection<ArchiveNode> items) return;
        foreach (var item in items)
            SetExpandedRecursive(item, expanded);
    }

    // -- Private helpers ------------------------------------------------------

    private static ArchiveStats CalculateStats(ArchiveNode root)
    {
        var stats = new ArchiveStats();
        CalculateStatsRecursive(root, stats);
        return stats;
    }

    private static void CalculateStatsRecursive(ArchiveNode node, ArchiveStats stats)
    {
        if (node.IsFolder)
        {
            stats.TotalFolders++;
            if (node.Children != null)
                foreach (var child in node.Children)
                    CalculateStatsRecursive(child, stats);
        }
        else
        {
            stats.TotalFiles++;
            stats.TotalSize      += node.Size;
            stats.CompressedSize += node.CompressedSize;
        }
    }

    private static void SetExpandedRecursive(ArchiveNode node, bool expanded)
    {
        node.IsExpanded = expanded;
        if (node.Children != null)
            foreach (var child in node.Children)
                SetExpandedRecursive(child, expanded);
    }

    private static string FormatSize(long size)
    {
        if (size < 1024)              return $"{size} B";
        if (size < 1024 * 1024)       return $"{size / 1024.0:F1} KB";
        if (size < 1024L * 1024 * 1024) return $"{size / (1024.0 * 1024):F1} MB";
        return                               $"{size / (1024.0 * 1024 * 1024):F1} GB";
    }
}

// -- Data models --------------------------------------------------------------

public class ArchiveNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string? Name              { get; set; }
    public bool    IsFolder          { get; set; }
    public long    Size              { get; set; }
    public long    CompressedSize    { get; set; }
    public string? Crc               { get; set; }
    public string? CompressionMethod { get; set; }
    public ObservableCollection<ArchiveNode>? Children { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public string Icon       => IsFolder ? "\U0001F4C1" : GetFileIcon();
    public string SizeInfo   => IsFolder ? "" : $"({FormatSize(Size)})";
    public string StatusIcon => Crc != null && Crc.Contains("FAIL") ? "\u26A0\uFE0F" : "";

    private string GetFileIcon()
    {
        var ext = System.IO.Path.GetExtension(Name)?.ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".md"              => "\U0001F4C4",
            ".jpg" or ".png" or ".gif"   => "\U0001F5BC\uFE0F",
            ".zip" or ".rar" or ".7z"    => "\U0001F4E6",
            ".exe" or ".dll"             => "\u2699\uFE0F",
            ".mp3" or ".wav"             => "\U0001F3B5",
            ".mp4" or ".avi"             => "\U0001F3AC",
            _                            => "\U0001F4C4"
        };
    }

    private static string FormatSize(long size)
    {
        if (size < 1024)        return $"{size} B";
        if (size < 1024 * 1024) return $"{size / 1024.0:F0} KB";
        return                        $"{size / (1024.0 * 1024):F1} MB";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ArchiveStats
{
    public int  TotalFiles    { get; set; }
    public int  TotalFolders  { get; set; }
    public long TotalSize     { get; set; }
    public long CompressedSize { get; set; }
}
