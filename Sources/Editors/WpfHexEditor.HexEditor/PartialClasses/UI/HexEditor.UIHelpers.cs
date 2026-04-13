// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.UIHelpers.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class containing UI utility and layout helper methods for the HexEditor.
//     Covers scroll synchronization, column width calculation, line count computation,
//     and visual state invalidation helpers used across multiple partial classes.
//
// Architecture Notes:
//     Utility methods only — no business logic. Uses WPF DispatcherTimer for
//     deferred refresh operations to avoid redundant rendering during rapid updates.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfHexEditor.HexEditor.Controls;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Models;
using WpfHexEditor.Core.Properties;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - UI Helpers
    /// Contains auto-scroll logic, column header generation, and context menu handlers
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
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        /// <summary>
        /// Returns the mouse position relative to HexViewport using Win32 GetCursorPos.
        /// Works regardless of WPF mouse capture or ancestor ClipToBounds restrictions.
        /// </summary>
        private Point GetMousePositionRelativeToViewport()
        {
            if (!GetCursorPos(out var pt)) return new Point(0, 0);
            var screen = new Point(pt.X, pt.Y);
            return HexViewport.PointFromScreen(screen);
        }

        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_viewModel == null || !_isMouseDown)
            {
                StopAutoScroll();
                return;
            }

            // Poll mouse position via Win32 — independent of WPF routing and ClipToBounds.
            var mousePos = GetMousePositionRelativeToViewport();
            _lastMousePosition = mousePos;
            double viewportHeight = HexViewport.ActualHeight;

            if (mousePos.Y < 0)
                _autoScrollDirection = -1;
            else if (mousePos.Y > viewportHeight)
                _autoScrollDirection = 1;
            else
                _autoScrollDirection = 0;

            if (_autoScrollDirection == 0)
                return;

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

                    if (_isOffsetLineDrag)
                    {
                        // Offset line drag: extend selection by whole lines
                        // Determine the edge line that just scrolled into view
                        long edgeLineStart;
                        if (_autoScrollDirection < 0)
                        {
                            // Scrolling up: select from the first visible line
                            edgeLineStart = newScrollPos * _viewModel.BytePerLine;
                        }
                        else
                        {
                            // Scrolling down: select to the last visible line
                            long lastVisibleLine = newScrollPos + _viewModel.VisibleLines - 1;
                            edgeLineStart = lastVisibleLine * _viewModel.BytePerLine;
                        }

                        long edgeLineEnd = Math.Min(edgeLineStart + _viewModel.BytePerLine - 1,
                            _viewModel.VirtualLength - 1);

                        // Merge with the anchor range to keep the original clicked lines selected
                        long selStart = Math.Min(_offsetDragAnchorStart.Value, edgeLineStart);
                        long selEnd = Math.Max(_offsetDragAnchorEnd.Value, edgeLineEnd);

                        var newStart = new VirtualPosition(selStart);
                        if (newStart != _lastAutoScrollPosition)
                        {
                            _viewModel.SetSelectionRange(newStart, new VirtualPosition(selEnd));
                            _lastAutoScrollPosition = newStart;
                        }
                    }
                    else
                    {
                        // Normal byte drag: use hit-test position
                        var hitResult = HexViewport.HitTestByteWithArea(_lastMousePosition);
                        if (hitResult.Position.HasValue)
                        {
                            var position = new VirtualPosition(hitResult.Position.Value);

                            // Only update selection if position actually changed (avoid redundant updates)
                            if (position != _lastAutoScrollPosition)
                            {
                                _viewModel.SetSelectionRange(_mouseDownPosition, position);
                                _lastAutoScrollPosition = position;
                            }
                        }
                    }
                }
                finally
                {
                    _viewModel.EndUpdate();
                }

                // Notify plugins after the EndUpdate batch — DP callbacks are not triggered here.
                OnSelectionStartChanged(EventArgs.Empty);
                OnSelectionStopChanged(EventArgs.Empty);
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

            // Calculate which column has SelectionStart (if any)
            int selectionStartColumn = -1;
            if (_viewModel != null && _viewModel.SelectionStart.IsValid)
            {
                selectionStartColumn = (int)(_viewModel.SelectionStart.Value % bytesPerLine);
            }

            // Phase 4: Calculate stride based on ByteSize (Bit8=1, Bit16=2, Bit32=4)
            int stride = (_viewModel?.ByteSize ?? Core.ByteSizeType.Bit8) switch
            {
                Core.ByteSizeType.Bit8 => 1,
                Core.ByteSizeType.Bit16 => 2,
                Core.ByteSizeType.Bit32 => 4,
                _ => 1
            };

            // Phase 4 / Bug 4: Calculate cell width dynamically based on font/DPI
            // Use HexViewport's dynamic calculation instead of hardcoded values
            double cellWidth = HexViewport.CalculateCellWidthForByteCount(stride);

            // Generate hex column headers with stride (e.g., 00 02 04 for Bit16)
            for (int i = 0; i < bytesPerLine; i += stride)
            {
                // Add byte spacer before this column if needed
                if (ByteSpacerPositioning == ByteSpacerPosition.Both ||
                    ByteSpacerPositioning == ByteSpacerPosition.HexBytePanel)
                {
                    AddByteSpacer(_hexHeaderStackPanel, i, forceEmpty: true);
                }

                // Add byte position header (format follows DataStringVisual: 00/0/00000000 for Hex/Decimal/Binary)
                bool isSelectionColumn = (i == selectionStartColumn);
                string headerFormat = DataStringVisual switch
                {
                    Core.DataVisualType.Hexadecimal => i.ToString("X2"),
                    Core.DataVisualType.Decimal => i.ToString(),
                    Core.DataVisualType.Binary => Convert.ToString(i, 2).PadLeft(8, '0'),
                    _ => i.ToString("X2")
                };
                var headerText = new TextBlock
                {
                    Text = headerFormat,
                    Width = cellWidth, // Phase 4: Dynamic width (24/52/106px)
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11,
                    FontWeight = isSelectionColumn ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = Resources["HeaderTextBrush"] as System.Windows.Media.Brush,
                    // No right margin: HexViewport uses HexByteSpacing=0, margin must match exactly
                };

                _hexHeaderStackPanel.Children.Add(headerText);
            }

            // Generate ASCII column headers (no byte spacers in ASCII panel)
            for (int i = 0; i < bytesPerLine; i++)
            {
                // Add placeholder for ASCII column (could show position or just be blank)
                bool isSelectionColumn = (i == selectionStartColumn);
                var headerText = new TextBlock
                {
                    Text = " ", // Blank or could show position like V1
                    Width = 10, // Match AsciiCharWidth from HexViewport
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11,
                    FontWeight = isSelectionColumn ? FontWeights.Bold : FontWeights.Normal,
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

            // Select the byte if no selection (preserve existing selection)
            if (SelectionLength < 1)
            {
                SelectionStart = position;
                SelectionStop = position;
            }

            // Access menu items from ContextMenu
            var contextMenu = this.ContextMenu;
            if (contextMenu == null) return;

            // Enable/disable menu items based on state
            var undoItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "UndoMenuItem") as MenuItem;
            var copyItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyMenuItem") as MenuItem;
            var copyAsItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyAsMenuItem") as MenuItem;
            var copyHexaItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyHexaMenuItem") as MenuItem;
            var copyAsciiItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyAsciiMenuItem") as MenuItem;
            var copyCSharpItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyCSharpMenuItem") as MenuItem;
            var copyCItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyCMenuItem") as MenuItem;
            var copyTblItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyTblMenuItem") as MenuItem;
            var copyFormattedViewItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "CopyFormattedViewMenuItem") as MenuItem;
            var findAllItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "FindAllMenuItem") as MenuItem;
            var pasteItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "PasteMenuItem") as MenuItem;
            var deleteItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "DeleteMenuItem") as MenuItem;
            var fillItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "FillByteMenuItem") as MenuItem;
            var replaceItem = LogicalTreeHelper.FindLogicalNode(contextMenu, "ReplaceByteMenuItem") as MenuItem;

            if (undoItem != null) undoItem.IsEnabled = CanUndo;
            if (copyItem != null) copyItem.IsEnabled = SelectionLength > 0;
            if (copyAsItem != null) copyAsItem.IsEnabled = SelectionLength > 0;
            if (copyHexaItem != null) copyHexaItem.IsEnabled = SelectionLength > 0;
            if (copyAsciiItem != null) copyAsciiItem.IsEnabled = SelectionLength > 0;
            if (copyCSharpItem != null) copyCSharpItem.IsEnabled = SelectionLength > 0;
            if (copyCItem != null) copyCItem.IsEnabled = SelectionLength > 0;
            if (copyTblItem != null) copyTblItem.IsEnabled = SelectionLength > 0 && _tblStream != null;
            if (copyFormattedViewItem != null) copyFormattedViewItem.IsEnabled = SelectionLength > 0;
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

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(); // Uses default copy mode
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

        private void CopyFormattedViewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(CopyPasteMode.FormattedView);
        }

        private void FindAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selection = GetSelectionByteArray();
            if (selection != null && selection.Length > 0)
            {
                // Find all occurrences
                var positions = FindAll(selection, 0);
                if (positions != null)
                {
                    var positionsList = positions.ToList();

                    // Clear existing search markers and highlights
                    if (_scrollMarkers != null)
                    {
                        _scrollMarkers.ClearMarkers(ScrollMarkerType.SearchResult);
                    }

                    if (positionsList.Count > 0)
                    {
                        // Add scroll marker for each result (orange markers)
                        if (_scrollMarkers != null)
                        {
                            foreach (var position in positionsList)
                            {
                                _scrollMarkers.AddMarker(position, ScrollMarkerType.SearchResult);
                            }

                            // Make scrollbar lighter when markers are active for better visibility
                            VerticalScroll.Opacity = 0.3;
                        }

                        // Highlight found bytes in the hex view (yellow background)
                        // Need to highlight ALL bytes in each match, not just the first one
                        if (HexViewport != null)
                        {
                            var highlightPositions = new HashSet<long>();
                            foreach (var startPos in positionsList)
                            {
                                // Add all bytes from this match (startPos to startPos + selection.Length - 1)
                                for (int i = 0; i < selection.Length; i++)
                                {
                                    highlightPositions.Add(startPos + i);
                                }
                            }
                            HexViewport.HighlightedPositions = highlightPositions;
                        }
                    }

                    StatusText.Text = $"Found {positionsList.Count} occurrence(s). Press ESC to clear.";
                }
                else
                {
                    StatusText.Text = "No matches found.";
                }
            }
        }

        /// <summary>
        /// Context menu handler for Find All (Async) - shows progress overlay
        /// </summary>
        private async void FindAllMenuItemAsync_Click(object sender, RoutedEventArgs e)
        {
            var selection = GetSelectionByteArray();
            if (selection != null && selection.Length > 0)
            {
                try
                {
                    // Find all occurrences asynchronously with progress overlay
                    var positionsList = await FindAllAsync(selection, 0);

                    if (positionsList != null && positionsList.Count > 0)
                    {
                        // Clear existing search markers and highlights
                        if (_scrollMarkers != null)
                        {
                            _scrollMarkers.ClearMarkers(ScrollMarkerType.SearchResult);
                        }

                        // Add scroll marker for each result (orange markers)
                        if (_scrollMarkers != null)
                        {
                            foreach (var position in positionsList)
                            {
                                _scrollMarkers.AddMarker(position, ScrollMarkerType.SearchResult);
                            }

                            // Make scrollbar lighter when markers are active for better visibility
                            VerticalScroll.Opacity = 0.3;
                        }

                        // Highlight found bytes in the hex view (yellow background)
                        // Need to highlight ALL bytes in each match, not just the first one
                        if (HexViewport != null)
                        {
                            var highlightPositions = new HashSet<long>();
                            foreach (var startPos in positionsList)
                            {
                                // Add all bytes from this match (startPos to startPos + selection.Length - 1)
                                for (int i = 0; i < selection.Length; i++)
                                {
                                    highlightPositions.Add(startPos + i);
                                }
                            }
                            HexViewport.HighlightedPositions = highlightPositions;
                        }

                        StatusText.Text = $"Found {positionsList.Count} occurrence(s). Press ESC to clear.";
                    }
                    else
                    {
                        StatusText.Text = "No matches found.";
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Search failed: {ex.Message}";
                }
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
            // Show modern MVVM dialog to get byte value
            var dialog = new Dialog.GiveByteWindow
            {
                Owner = Window.GetWindow(this)
            };

            // If there's a selection, pre-fill the current byte value
            if (SelectionLength == 1)
            {
                try
                {
                    var selectedByte = GetByte(SelectionStart);
                    dialog.HexTextBox.LongValue = selectedByte;
                    dialog.ViewModel.ByteValue = selectedByte;
                }
                catch
                {
                    // Ignore errors reading byte
                }
            }

            if (dialog.ShowDialog() == true)
            {
                byte fillByte = dialog.ByteValue;
                long fillStart = _viewModel.SelectionStart.Value;
                long fillLength = SelectionLength;

                FillWithByte(fillByte, fillStart, fillLength);
                StatusText.Text = $"Filled {fillLength} bytes with 0x{fillByte:X2}";
            }
        }

        private void ReplaceByteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Show modern MVVM dialog to get find/replace byte values
            var dialog = new Dialog.ReplaceByteWindow
            {
                Owner = Window.GetWindow(this)
            };

            // Pre-fill options if a selection exists
            if (SelectionLength > 0)
            {
                dialog.ViewModel.ReplaceInSelectionOnly = true;

                // If the selection is a single byte, pre-fill it as the value to find
                if (SelectionLength == 1)
                {
                    try
                    {
                        var selectedByte = GetByte(SelectionStart);
                        dialog.HexTextBox.LongValue = selectedByte;
                        dialog.ViewModel.FindByte = selectedByte;
                    }
                    catch
                    {
                        // Ignore errors reading byte
                    }
                }
            }

            if (dialog.ShowDialog() == true)
            {
                byte findByte = dialog.FindByte;
                byte replaceByte = dialog.ReplaceByte;
                byte[] findData = new byte[] { findByte };
                byte[] replaceData = new byte[] { replaceByte };

                bool inSelectionOnly = dialog.ReplaceInSelectionOnly;
                int replacedCount = 0;

                if (inSelectionOnly && SelectionLength > 0)
                {
                    // Replace only within selection - use ViewModel directly for accurate positions
                    long selStart = _viewModel.SelectionStart.Value;
                    long selLength = SelectionLength;

                    // Use BeginUpdate/EndUpdate to batch all modifications
                    _viewModel.BeginUpdate();
                    try
                    {
                        // Search and replace within selection only
                        // Start from selection start and search only within selection bounds
                        long searchPos = selStart;
                        long selEnd = selStart + selLength;

                        while (searchPos < selEnd)
                        {
                            // Find next occurrence starting from searchPos
                            long foundPos = FindFirst(findData, searchPos);

                            // If found and within selection bounds
                            if (foundPos >= 0 && foundPos < selEnd)
                            {
                                // Replace the byte at this position
                                SetByte(foundPos, replaceByte);
                                replacedCount++;

                                // Move search position past this occurrence
                                searchPos = foundPos + 1;
                            }
                            else
                            {
                                // No more occurrences within selection
                                break;
                            }
                        }
                    }
                    finally
                    {
                        // EndUpdate will refresh display once
                        _viewModel.EndUpdate();
                    }

                }
                else
                {
                    // Replace in entire file
                    var replaced = ReplaceAll(findData, replaceData, false, false);
                    replacedCount = replaced.Count();
                }

                // Clear selection after replacement
                ClearSelection();

                // Enhanced status message with scope information
                string scope = inSelectionOnly ? "in selection" : "in file";
                StatusText.Text = $"Replaced {replacedCount} occurrences (0x{findByte:X2} → 0x{replaceByte:X2}) {scope}";
            }
        }

        private void ReverseSelectionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ReverseSelection();
        }

        private void InvertSelectionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            InvertSelection();
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

                // Notify plugins — DP callbacks are not triggered by direct ViewModel assignment.
                OnSelectionStartChanged(EventArgs.Empty);
                OnSelectionStopChanged(EventArgs.Empty);

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
                StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusNoMoreBookmarksAfter;
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

                // Notify plugins — DP callbacks are not triggered by direct ViewModel assignment.
                OnSelectionStartChanged(EventArgs.Empty);
                OnSelectionStopChanged(EventArgs.Empty);

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
                StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusNoMoreBookmarksBefore;
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

        private void CompareFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CompareFileRequested?.Invoke(this, new CompareFileRequestedEventArgs(FileName));
        }

        private void CompareSelectionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectionLength <= 0 || string.IsNullOrEmpty(FileName)) return;

            // Write selected bytes to a temp file and raise the event with that path as "left"
            var selBytes = GetSelectionByteArray();
            if (selBytes is null or { Length: 0 }) return;

            var tmp = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "WpfHexEditor",
                $"sel_{System.IO.Path.GetFileNameWithoutExtension(FileName)}_{DateTime.UtcNow:HHmmss}.bin");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(tmp)!);
            System.IO.File.WriteAllBytes(tmp, selBytes);

            CompareSelectionRequested?.Invoke(this, new CompareFileRequestedEventArgs(tmp) { IsTempFile = true });
        }

        #endregion

        #region Scroll Markers Management

        /// <summary>
        /// Update scroll markers visibility based on ShowXxxxMarkers properties
        /// </summary>
        internal void UpdateScrollMarkersVisibility()
        {
            if (_scrollMarkers == null)
                return;

            // Temporarily store marker data
            var bookmarks = ShowBookmarkMarkers ? _scrollMarkers.BookmarkPositions : new HashSet<long>();
            var modified = ShowModifiedMarkers ? _scrollMarkers.ModifiedPositions : new HashSet<long>();
            var inserted = ShowInsertedMarkers ? _scrollMarkers.InsertedPositions : new HashSet<long>();
            var deleted = ShowDeletedMarkers ? _scrollMarkers.DeletedPositions : new HashSet<long>();
            var searchResults = ShowSearchResultMarkers ? _scrollMarkers.SearchResultPositions : new HashSet<long>();

            // Clear and restore only visible markers
            // This forces a redraw with only the enabled marker types
            if (!ShowBookmarkMarkers)
                _scrollMarkers.ClearMarkers(ScrollMarkerType.Bookmark);

            if (!ShowModifiedMarkers)
                _scrollMarkers.ClearMarkers(ScrollMarkerType.Modified);

            if (!ShowInsertedMarkers)
                _scrollMarkers.ClearMarkers(ScrollMarkerType.Inserted);

            if (!ShowDeletedMarkers)
                _scrollMarkers.ClearMarkers(ScrollMarkerType.Deleted);

            if (!ShowSearchResultMarkers)
                _scrollMarkers.ClearMarkers(ScrollMarkerType.SearchResult);

            // Force visual refresh
            _scrollMarkers.InvalidateVisual();
        }

        #endregion
    }
}
