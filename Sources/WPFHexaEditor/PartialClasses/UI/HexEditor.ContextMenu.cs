//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfHexaEditor.Core;
using WpfHexaEditor.Models;
using WpfHexaEditor.Properties;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexEditor partial class - Context Menu and UI Helpers
    /// Contains context menu handlers, auto-scroll, and column header generation
    /// </summary>
    public partial class HexEditor
    {
        #region Auto-Scroll During Mouse Selection

        /// <summary>
        /// Start auto-scrolling in the specified direction
        /// </summary>
        /// <param name="direction">-1 for up, 1 for down</param>
        private void StartAutoScroll(int direction)
        {
            if (_autoScrollDirection != direction)
            {
                _autoScrollDirection = direction;
                _lastAutoScrollPosition = VirtualPosition.Invalid; // Reset tracking
                if (!_autoScrollTimer.IsEnabled)
                {
                    _autoScrollTimer.Start();
                }
            }
        }

        /// <summary>
        /// Stop auto-scrolling
        /// </summary>
        private void StopAutoScroll()
        {
            _autoScrollDirection = 0;
            _lastAutoScrollPosition = VirtualPosition.Invalid; // Reset tracking
            _autoScrollTimer.Stop();
        }

        /// <summary>
        /// Auto-scroll timer tick - scroll viewport and update selection
        /// Optimized to reduce redundant updates
        /// </summary>
        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_viewModel == null || !_isMouseDown || _autoScrollDirection == 0)
            {
                StopAutoScroll();
                return;
            }

            // Calculate new scroll position (scroll multiple lines per tick for faster auto-scroll)
            long newScrollPos = _viewModel.ScrollPosition + (_autoScrollDirection * AutoScrollSpeed);

            // Clamp to valid range
            long maxScroll = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);
            newScrollPos = Math.Max(0, Math.Min(newScrollPos, maxScroll));

            // Only update if position changed
            if (newScrollPos != _viewModel.ScrollPosition)
            {
                // Use batch update for better performance
                _viewModel.BeginUpdate();
                try
                {
                    _viewModel.ScrollPosition = newScrollPos;

                    // Update selection to the byte at the current mouse position
                    var position = GetVirtualPositionAtMouse(_lastMousePosition);

                    // Only update selection if position actually changed (avoid redundant updates)
                    if (position.IsValid && position != _lastAutoScrollPosition)
                    {
                        _viewModel.SetSelectionRange(_mouseDownPosition, position);
                        _lastAutoScrollPosition = position;
                    }
                }
                finally
                {
                    _viewModel.EndUpdate();
                }
            }
        }

        #endregion

        #region Column Header Generation

        /// <summary>
        /// Refresh the column headers with byte position numbers and byte spacers
        /// Called when BytePerLine, ByteGrouping, or ByteSpacer properties change
        /// </summary>
        private void RefreshColumnHeader()
        {
            if (_hexHeaderStackPanel == null || _asciiHeaderStackPanel == null)
                return;

            // Clear existing headers
            _hexHeaderStackPanel.Children.Clear();
            _asciiHeaderStackPanel.Children.Clear();

            int bytesPerLine = BytePerLine;

            // Generate hex column headers (00 01 02...0F)
            for (int i = 0; i < bytesPerLine; i++)
            {
                // Add byte spacer before this column if needed
                if (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                    ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel)
                {
                    AddByteSpacer(_hexHeaderStackPanel, i, forceEmpty: true);
                }

                // Add byte position header (00, 01, 02, etc.)
                var headerText = new TextBlock
                {
                    Text = i.ToString("X2"),
                    Width = 24, // Match HexByteWidth from HexViewport
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11,
                    FontWeight = FontWeights.Normal,
                    Foreground = Resources["HeaderTextBrush"] as System.Windows.Media.Brush,
                    Margin = new Thickness(0, 0, 2, 0) // HexByteSpacing
                };

                _hexHeaderStackPanel.Children.Add(headerText);
            }

            // Generate ASCII column headers (no byte spacers in ASCII panel)
            for (int i = 0; i < bytesPerLine; i++)
            {
                // Add placeholder for ASCII column (could show position or just be blank)
                var headerText = new TextBlock
                {
                    Text = " ", // Blank or could show position like V1
                    Width = 10, // Match AsciiCharWidth from HexViewport
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11,
                    Foreground = Resources["HeaderTextBrush"] as System.Windows.Media.Brush
                };

                _asciiHeaderStackPanel.Children.Add(headerText);
            }
        }

        /// <summary>
        /// Add byte spacer to a StackPanel at the specified column position
        /// Spacers are added every ByteGrouping bytes (e.g., every 8 bytes)
        /// </summary>
        /// <param name="stack">StackPanel to add spacer to</param>
        /// <param name="column">Current column index (0-based)</param>
        /// <param name="forceEmpty">Force empty spacer even if visual style is Line or Dash</param>
        private void AddByteSpacer(StackPanel stack, int column, bool forceEmpty = false)
        {
            // Only add spacer at group boundaries (e.g., every 8 bytes)
            // column % ByteGrouping must be 0, and column > 0 (no spacer before first byte)
            if (column % (int)ByteGrouping != 0 || column <= 0)
                return;

            int width = (int)ByteSpacerWidthTickness;

            if (!forceEmpty)
            {
                switch (ByteSpacerVisualStyle)
                {
                    case ByteSpacerVisual.Empty:
                        stack.Children.Add(new TextBlock { Width = width });
                        break;

                    case ByteSpacerVisual.Line:
                        // Solid vertical line
                        stack.Children.Add(new System.Windows.Shapes.Line
                        {
                            Y2 = 20, // Line height
                            X1 = width / 2.0,
                            X2 = width / 2.0,
                            Stroke = BorderBrush,
                            StrokeThickness = 1,
                            Width = width
                        });
                        break;

                    case ByteSpacerVisual.Dash:
                        // Dashed vertical line
                        stack.Children.Add(new System.Windows.Shapes.Line
                        {
                            Y2 = 19,
                            X1 = width / 2.0,
                            X2 = width / 2.0,
                            Stroke = BorderBrush,
                            StrokeDashArray = new DoubleCollection(new double[] { 2 }),
                            StrokeThickness = 1,
                            Width = width
                        });
                        break;
                }
            }
            else
            {
                // Force empty spacer (used for headers)
                stack.Children.Add(new TextBlock { Width = width });
            }
        }

        #endregion

        #region Context Menu Handlers

        private long _rightClickPosition = -1;

        /// <summary>
        /// Show context menu on right-click
        /// </summary>
        public void ShowContextMenu(long position)
        {
            if (!AllowContextMenu) return;

            _rightClickPosition = position;

            // Select the byte if no selection
            if (SelectionLength <= 1)
            {
                SelectionStart = position;
                SelectionStop = position;
            }

            // Access menu items from ContextMenu
            var contextMenu = this.ContextMenu;
            if (contextMenu == null) return;

            // Enable/disable menu items based on state
            var undoItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "UndoMenuItem") as MenuItem;
            var copyAsItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyAsMenuItem") as MenuItem;
            var copyHexaItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyHexaMenuItem") as MenuItem;
            var copyAsciiItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyAsciiMenuItem") as MenuItem;
            var copyCSharpItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyCSharpMenuItem") as MenuItem;
            var copyCItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyCMenuItem") as MenuItem;
            var copyTblItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyTblMenuItem") as MenuItem;
            var findAllItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "FindAllMenuItem") as MenuItem;
            var pasteItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "PasteMenuItem") as MenuItem;
            var deleteItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "DeleteMenuItem") as MenuItem;
            var fillItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "FillByteMenuItem") as MenuItem;
            var replaceItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "ReplaceByteMenuItem") as MenuItem;

            if (undoItem != null) undoItem.IsEnabled = CanUndo;
            if (copyAsItem != null) copyAsItem.IsEnabled = SelectionLength > 0;
            if (copyHexaItem != null) copyHexaItem.IsEnabled = SelectionLength > 0;
            if (copyAsciiItem != null) copyAsciiItem.IsEnabled = SelectionLength > 0;
            if (copyCSharpItem != null) copyCSharpItem.IsEnabled = SelectionLength > 0;
            if (copyCItem != null) copyCItem.IsEnabled = SelectionLength > 0;
            if (copyTblItem != null) copyTblItem.IsEnabled = SelectionLength > 0 && _tblStream != null;
            if (findAllItem != null) findAllItem.IsEnabled = SelectionLength > 0;
            if (pasteItem != null) pasteItem.IsEnabled = !ReadOnlyMode && Clipboard.ContainsText();
            if (deleteItem != null) deleteItem.IsEnabled = !ReadOnlyMode && SelectionLength > 0;
            if (fillItem != null) fillItem.IsEnabled = !ReadOnlyMode && SelectionLength > 0;
            if (replaceItem != null) replaceItem.IsEnabled = !ReadOnlyMode && SelectionLength > 0;

            // Show context menu
            contextMenu.Visibility = Visibility.Visible;
            contextMenu.IsOpen = true;
        }

        private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }

        private void CopyHexaMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.HexaString);
        }

        private void CopyAsciiMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.AsciiString);
        }

        private void CopyCSharpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.CSharpCode);
        }

        private void CopyCMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.CCode);
        }

        private void CopyTblMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.TblString);
        }

        private void FindAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selection = GetSelectionByteArray();
            if (selection != null && selection.Length > 0)
            {
                FindAll(selection, 0);
                StatusText.Text = Properties.Resources.StatusSearchComplete;
            }
        }

        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Paste();
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelection();
        }

        private void FillByteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Show dialog to get byte value
            var dialog = new Dialog.GiveByteWindow
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                byte fillByte = (byte)dialog.HexTextBox.LongValue;
                FillWithByte(fillByte, SelectionStart, SelectionLength);
                StatusText.Text = $"Filled {SelectionLength} bytes with 0x{fillByte:X2}";
            }
        }

        private void ReplaceByteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Show dialog to get find/replace byte values
            var dialog = new Dialog.ReplaceByteWindow
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                byte findByte = (byte)dialog.HexTextBox.LongValue;
                byte replaceByte = (byte)dialog.ReplaceHexTextBox.LongValue;
                byte[] findData = new byte[] { findByte };
                byte[] replaceData = new byte[] { replaceByte };
                var replaced = ReplaceAll(findData, replaceData, false, false);
                StatusText.Text = $"Replaced {replaced.Count()} occurrences (0x{findByte:X2} → 0x{replaceByte:X2})";
            }
        }

        private void ReverseSelectionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ReverseSelection();
        }

        private void SetBookmarkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || !_viewModel.SelectionStart.IsValid)
                return;

            // Add bookmark at current position
            long position = _viewModel.SelectionStart.Value;

            if (_bookmarkService.HasBookmarkAt(position))
            {
                // Toggle: Remove existing bookmark
                _bookmarkService.RemoveBookmark(position);
                StatusText.Text = $"Bookmark removed at position 0x{position:X}";
            }
            else
            {
                // Add new bookmark
                _bookmarkService.AddBookmark(position, $"Bookmark at 0x{position:X}");
                StatusText.Text = $"Bookmark added at position 0x{position:X}";
            }

            // Refresh view to show bookmark indicator
            _viewModel.RefreshDisplay();
        }

        private void ClearBookmarksMenuItem_Click(object sender, RoutedEventArgs e)
        {
            int count = _bookmarkService.ClearAll();
            StatusText.Text = count > 0
                ? $"Cleared {count} bookmark(s)"
                : "No bookmarks to clear";

            // Refresh view to remove bookmark indicators
            if (count > 0 && _viewModel != null)
                _viewModel.RefreshDisplay();
        }

        /// <summary>
        /// Navigate to next bookmark after current position (F2)
        /// </summary>
        public void GoToNextBookmark()
        {
            if (_viewModel == null || !_viewModel.SelectionStart.IsValid)
                return;

            long currentPos = _viewModel.SelectionStart.Value;
            var nextBookmark = _bookmarkService.GetNextBookmark(currentPos);

            if (nextBookmark != null)
            {
                // Navigate to bookmark position
                _viewModel.SelectionStart = new VirtualPosition(nextBookmark.BytePositionInStream);
                _viewModel.SelectionStop = new VirtualPosition(nextBookmark.BytePositionInStream);

                // Scroll to ensure bookmark is visible
                long targetLine = nextBookmark.BytePositionInStream / _viewModel.BytePerLine;
                if (targetLine < _viewModel.ScrollPosition ||
                    targetLine >= _viewModel.ScrollPosition + _viewModel.VisibleLines)
                {
                    _viewModel.ScrollPosition = Math.Max(0, targetLine - _viewModel.VisibleLines / 2);
                }

                StatusText.Text = string.IsNullOrEmpty(nextBookmark.Description)
                    ? $"Jumped to bookmark at 0x{nextBookmark.BytePositionInStream:X}"
                    : $"Jumped to: {nextBookmark.Description}";
            }
            else
            {
                StatusText.Text = Properties.Resources.StatusNoMoreBookmarksAfter;
            }
        }

        /// <summary>
        /// Navigate to previous bookmark before current position (Shift+F2)
        /// </summary>
        public void GoToPreviousBookmark()
        {
            if (_viewModel == null || !_viewModel.SelectionStart.IsValid)
                return;

            long currentPos = _viewModel.SelectionStart.Value;
            var prevBookmark = _bookmarkService.GetPreviousBookmark(currentPos);

            if (prevBookmark != null)
            {
                // Navigate to bookmark position
                _viewModel.SelectionStart = new VirtualPosition(prevBookmark.BytePositionInStream);
                _viewModel.SelectionStop = new VirtualPosition(prevBookmark.BytePositionInStream);

                // Scroll to ensure bookmark is visible
                long targetLine = prevBookmark.BytePositionInStream / _viewModel.BytePerLine;
                if (targetLine < _viewModel.ScrollPosition ||
                    targetLine >= _viewModel.ScrollPosition + _viewModel.VisibleLines)
                {
                    _viewModel.ScrollPosition = Math.Max(0, targetLine - _viewModel.VisibleLines / 2);
                }

                StatusText.Text = string.IsNullOrEmpty(prevBookmark.Description)
                    ? $"Jumped to bookmark at 0x{prevBookmark.BytePositionInStream:X}"
                    : $"Jumped to: {prevBookmark.Description}";
            }
            else
            {
                StatusText.Text = Properties.Resources.StatusNoMoreBookmarksBefore;
            }
        }

        private void PasteOverwriteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            // Save current mode
            var originalMode = _viewModel.EditMode;

            try
            {
                // Temporarily switch to Overwrite mode
                _viewModel.EditMode = EditMode.Overwrite;

                // Paste
                Paste();
            }
            finally
            {
                // Restore original mode
                _viewModel.EditMode = originalMode;
            }
        }

        private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectAll();
        }

        #endregion
    }
}
