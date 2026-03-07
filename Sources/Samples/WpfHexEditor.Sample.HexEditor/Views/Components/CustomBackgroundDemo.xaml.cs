//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Custom Background Blocks Demo Component
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HexEditorControl = WpfHexEditor.HexEditor.HexEditor;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Events;

namespace WpfHexEditor.Sample.HexEditor.Views.Components
{
    /// <summary>
    /// UserControl for demonstrating Custom Background Blocks feature
    /// Shows how to add, remove, and manage custom background highlights
    /// </summary>
    public partial class CustomBackgroundDemo : UserControl
    {
        private HexEditorControl _hexEditor;
        private readonly Random _random = new Random();

        /// <summary>
        /// Observable collection for ListBox binding
        /// </summary>
        public ObservableCollection<BlockDisplayItem> BlockItems { get; } = new ObservableCollection<BlockDisplayItem>();

        public CustomBackgroundDemo()
        {
            InitializeComponent();

            BlocksListBox.ItemsSource = BlockItems;
        }

        /// <summary>
        /// Set the HexEditor instance to work with
        /// Call this from MainWindow after initialization
        /// </summary>
        public void SetHexEditor(HexEditorControl hexEditor)
        {
            // Unsubscribe from old editor if any
            if (_hexEditor != null)
            {
                _hexEditor.CustomBackgroundBlockChanged -= OnCustomBackgroundBlockChanged;
            }

            _hexEditor = hexEditor;

            if (_hexEditor != null)
            {
                // Subscribe to block changes
                _hexEditor.CustomBackgroundBlockChanged += OnCustomBackgroundBlockChanged;

                // Initial sync
                SyncBlocksList();
            }
        }

        #region Event Handlers - Quick Actions

        private void AddRedBlock_Click(object sender, RoutedEventArgs e)
        {
            if (_hexEditor == null)
            {
                ShowStatus("❌ HexEditor not initialized");
                return;
            }

            var block = new CustomBackgroundBlock(
                start: 0,
                length: 16,
                color: new SolidColorBrush(Color.FromRgb(255, 200, 200)),
                description: "File Header (Bytes 0-15)",
                opacity: 0.4);

            _hexEditor.AddCustomBackgroundBlock(block);
            ShowStatus("✅ Added red block (bytes 0-16)");
        }

        private void AddBlueBlock_Click(object sender, RoutedEventArgs e)
        {
            if (_hexEditor == null) return;

            var block = new CustomBackgroundBlock(
                start: 16,
                length: 16,
                color: new SolidColorBrush(Color.FromRgb(200, 220, 255)),
                description: "Metadata Section (Bytes 16-31)",
                opacity: 0.4);

            _hexEditor.AddCustomBackgroundBlock(block);
            ShowStatus("✅ Added blue block (bytes 16-32)");
        }

        private void AddGreenBlock_Click(object sender, RoutedEventArgs e)
        {
            if (_hexEditor == null) return;

            var block = new CustomBackgroundBlock(
                start: 32,
                length: 32,
                color: new SolidColorBrush(Color.FromRgb(200, 255, 200)),
                description: "Data Section (Bytes 32-63)",
                opacity: 0.4);

            _hexEditor.AddCustomBackgroundBlock(block);
            ShowStatus("✅ Added green block (bytes 32-64)");
        }

        private void AddYellowBlock_Click(object sender, RoutedEventArgs e)
        {
            if (_hexEditor == null) return;

            var block = new CustomBackgroundBlock(
                start: 64,
                length: 64,
                color: new SolidColorBrush(Color.FromRgb(255, 255, 200)),
                description: "Extended Data (Bytes 64-127)",
                opacity: 0.4);

            _hexEditor.AddCustomBackgroundBlock(block);
            ShowStatus("✅ Added yellow block (bytes 64-128)");
        }

        private void AddRandomBlock_Click(object sender, RoutedEventArgs e)
        {
            if (_hexEditor == null) return;

            long start = _random.Next(0, 1000);
            long length = _random.Next(16, 128);

            var block = new CustomBackgroundBlock(
                start: start,
                length: length,
                setRandomBrush: true,
                opacity: 0.35);

            block.Description = $"Random Block #{BlockItems.Count + 1}";

            _hexEditor.AddCustomBackgroundBlock(block);
            ShowStatus($"✅ Added random block at 0x{start:X} (length: {length})");
        }

        private void DetectFormat_Click(object sender, RoutedEventArgs e)
        {
            if (_hexEditor?.Stream == null)
            {
                ShowStatus("❌ No file loaded");
                return;
            }

            try
            {
                // Read first 16 bytes from stream
                var stream = _hexEditor.Stream;
                var originalPosition = stream.Position;
                stream.Position = 0;

                var header = new byte[Math.Min(16, (int)stream.Length)];
                var bytesRead = stream.Read(header, 0, header.Length);
                stream.Position = originalPosition; // Restore position

                if (bytesRead == 0)
                {
                    ShowStatus("❌ Could not read file header");
                    return;
                }

                // Clear existing blocks
                _hexEditor.ClearCustomBackgroundBlock();

                // Detect common file formats
                if (header.Length >= 4 &&
                    header[0] == 0x50 && header[1] == 0x4B &&
                    header[2] == 0x03 && header[3] == 0x04)
                {
                    // ZIP file
                    _hexEditor.AddCustomBackgroundBlock(new CustomBackgroundBlock(
                        0, 4, new SolidColorBrush(Color.FromRgb(255, 100, 100)),
                        "ZIP Signature (PK\\x03\\x04)", 0.5));

                    ShowStatus("✅ Detected ZIP archive! Added signature block.");
                }
                else if (header.Length >= 8 &&
                         header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                         header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                {
                    // PNG file
                    _hexEditor.AddCustomBackgroundBlock(new CustomBackgroundBlock(
                        0, 8, new SolidColorBrush(Color.FromRgb(100, 255, 100)),
                        "PNG Signature", 0.5));

                    ShowStatus("✅ Detected PNG image! Added signature block.");
                }
                else if (header.Length >= 4 &&
                         header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                {
                    // PDF file
                    _hexEditor.AddCustomBackgroundBlock(new CustomBackgroundBlock(
                        0, 4, new SolidColorBrush(Color.FromRgb(255, 150, 150)),
                        "PDF Signature (%PDF)", 0.5));

                    ShowStatus("✅ Detected PDF document! Added signature block.");
                }
                else if (header.Length >= 2 &&
                         header[0] == 0xFF && header[1] == 0xD8)
                {
                    // JPEG file
                    _hexEditor.AddCustomBackgroundBlock(new CustomBackgroundBlock(
                        0, 2, new SolidColorBrush(Color.FromRgb(255, 200, 100)),
                        "JPEG Signature (FF D8)", 0.5));

                    ShowStatus("✅ Detected JPEG image! Added signature block.");
                }
                else if (header.Length >= 2 &&
                         header[0] == 0x4D && header[1] == 0x5A)
                {
                    // EXE file (Windows PE)
                    _hexEditor.AddCustomBackgroundBlock(new CustomBackgroundBlock(
                        0, 2, new SolidColorBrush(Color.FromRgb(150, 150, 255)),
                        "EXE Signature (MZ)", 0.5));

                    ShowStatus("✅ Detected Windows executable! Added signature block.");
                }
                else
                {
                    ShowStatus("❓ Unknown file format (no signature detected)");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Error: {ex.Message}");
            }
        }

        private void ClearBlocks_Click(object sender, RoutedEventArgs e)
        {
            if (_hexEditor == null) return;

            var count = _hexEditor.CustomBackgroundService.GetBlockCount();
            _hexEditor.ClearCustomBackgroundBlock();
            ShowStatus($"✅ Cleared {count} block(s)");
        }

        private void RemoveBlock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CustomBackgroundBlock block)
            {
                _hexEditor?.RemoveCustomBackgroundBlock(block);
                ShowStatus($"✅ Removed block at 0x{block.StartOffset:X}");
            }
        }

        #endregion

        #region Event Handling

        private void OnCustomBackgroundBlockChanged(object sender, CustomBackgroundBlockEventArgs e)
        {
            // Update the list when blocks change
            Dispatcher.Invoke(() =>
            {
                SyncBlocksList();

                // Update status based on change type
                switch (e.ChangeType)
                {
                    case BlockChangeType.Added:
                        ShowStatus($"✅ Block added (Total: {e.TotalBlockCount})");
                        break;
                    case BlockChangeType.AddedMultiple:
                        ShowStatus($"✅ {e.AffectedCount} blocks added (Total: {e.TotalBlockCount})");
                        break;
                    case BlockChangeType.Removed:
                        ShowStatus($"✅ Block removed (Total: {e.TotalBlockCount})");
                        break;
                    case BlockChangeType.RemovedMultiple:
                        ShowStatus($"✅ {e.AffectedCount} blocks removed (Total: {e.TotalBlockCount})");
                        break;
                    case BlockChangeType.Cleared:
                        ShowStatus($"✅ All blocks cleared ({e.AffectedCount} removed)");
                        break;
                }
            });
        }

        #endregion

        #region Helper Methods

        private void SyncBlocksList()
        {
            if (_hexEditor == null) return;

            BlockItems.Clear();

            var blocks = _hexEditor.CustomBackgroundService.GetBlocksSorted();
            foreach (var block in blocks)
            {
                BlockItems.Add(new BlockDisplayItem
                {
                    Block = block,
                    DisplayText = $"0x{block.StartOffset:X8} - 0x{block.StopOffset:X8} (Length: {block.Length})",
                    Description = string.IsNullOrEmpty(block.Description) ? "(No description)" : block.Description,
                    BackgroundBrush = block.GetTransparentBrush()
                });
            }
        }

        private void ShowStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        #endregion

        #region Display Item Class

        public class BlockDisplayItem
        {
            public CustomBackgroundBlock Block { get; set; }
            public string DisplayText { get; set; }
            public string Description { get; set; }
            public SolidColorBrush BackgroundBrush { get; set; }
        }

        #endregion
    }
}
