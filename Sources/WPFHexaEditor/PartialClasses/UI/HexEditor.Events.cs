//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using WpfHexaEditor.Core;
using WpfHexaEditor.Models;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexEditor partial class - Internal Events
    /// Contains event handlers for mouse, keyboard, scrolling, and UI updates
    /// </summary>
    public partial class HexEditor
    {
        #region Internal Events

        private void Content_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null || e.ChangedButton != MouseButton.Left)
                return;

            // Set keyboard focus to enable keyboard input
            HexViewport.Focus();

            // Phase 4: Use HexViewport's HitTestByteWithArea (same as mouseover - guaranteed consistent!)
            var mousePos = e.GetPosition(HexViewport);
            var hitResult = HexViewport.HitTestByteWithArea(mousePos);

            // Check if we hit a valid byte position
            if (!hitResult.Position.HasValue)
                return;

            // Convert long position to VirtualPosition
            var position = new VirtualPosition(hitResult.Position.Value);

            // Detect which area was clicked (Hex or ASCII) from hit result
            _isAsciiEditMode = !hitResult.IsHexArea; // If not hex, then ASCII

            // Set the active panel for dual-color selection
            HexViewport.ActivePanel = hitResult.IsHexArea
                ? Controls.ActivePanelType.Hex
                : Controls.ActivePanelType.Ascii;

            // Check for Shift key (extend selection)
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                _viewModel.ExtendSelection(position);
            }
            else
            {
                // Start new selection
                _viewModel.SetSelection(position);
                _isMouseDown = true;
                _mouseDownPosition = position;

                // Capture mouse for drag operation
                HexViewport.CaptureMouse();
            }

            e.Handled = true;
        }

        private void Content_MouseMove(object sender, MouseEventArgs e)
        {
            if (_viewModel == null || !_isMouseDown || e.LeftButton != MouseButtonState.Pressed)
            {
                StopAutoScroll();
                return;
            }

            Point mousePos = e.GetPosition(HexViewport);
            _lastMousePosition = mousePos;

            // Phase 4: Use HexViewport's HitTestByteWithArea (same as mouseover - guaranteed consistent!)
            var hitResult = HexViewport.HitTestByteWithArea(mousePos);
            if (hitResult.Position.HasValue)
            {
                var position = new VirtualPosition(hitResult.Position.Value);
                // Update selection range during drag
                _viewModel.SetSelectionRange(_mouseDownPosition, position);
            }

            // Check if mouse is near the top or bottom edge for auto-scroll
            double viewportHeight = HexViewport.ActualHeight;

            if (mousePos.Y < AutoScrollEdgeThreshold)
            {
                // Near top edge - scroll up
                StartAutoScroll(-1);
            }
            else if (mousePos.Y > viewportHeight - AutoScrollEdgeThreshold)
            {
                // Near bottom edge - scroll down
                StartAutoScroll(1);
            }
            else
            {
                // In the middle - stop auto-scroll
                StopAutoScroll();
            }

            e.Handled = true;
        }

        private void Content_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isMouseDown)
            {
                _isMouseDown = false;
                HexViewport.ReleaseMouseCapture();
                StopAutoScroll();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Helper method to find the virtual position at mouse coordinates
        /// Uses HexViewport's precise hit-testing with actual measured dimensions
        /// </summary>
        /// <summary>
        /// Click area types for mouse input
        /// </summary>
        private enum ClickArea
        {
            Hex,
            Ascii,
            Other
        }

        /// <summary>
        /// Get which area was clicked at mouse position
        /// Uses HexViewport's calculated layout dimensions for accurate hit-testing
        /// </summary>
        private ClickArea GetClickAreaAtMouse(Point mousePosition)
        {
            if (_viewModel == null || HexViewport == null)
                return ClickArea.Other;

            double x = mousePosition.X;

            // Use HexViewport's actual calculated dimensions
            double hexStartX = HexViewport.HexPanelStartX;
            double separatorX = HexViewport.SeparatorStartX;
            double asciiStartX = HexViewport.AsciiPanelStartX;

            // Check if in hex area (between hex start and separator)
            if (x >= hexStartX && x < separatorX)
                return ClickArea.Hex;

            // Check if in ASCII area (after ASCII start)
            if (x >= asciiStartX)
                return ClickArea.Ascii;

            return ClickArea.Other;
        }

        private VirtualPosition GetVirtualPositionAtMouse(Point mousePosition)
        {
            if (_viewModel == null || _viewModel.Lines.Count == 0 || HexViewport == null)
                return VirtualPosition.Invalid;

            // Use HexViewport's actual LineHeight (calculated from font metrics)
            double lineHeight = HexViewport.LineHeight;
            if (lineHeight <= 0)
                return VirtualPosition.Invalid;

            // Layout constants from HexViewport (must match rendering)
            const double HexByteSpacing = 2;
            const double TopMargin = 2;
            const double AsciiCharWidth = 10;
            const int ByteGrouping = 4;
            const double ByteSpacerWidthTickness = 6;

            // Phase 4: Calculate stride and cell width based on ByteSize
            int stride = _viewModel.ByteSize switch
            {
                Core.ByteSizeType.Bit8 => 1,
                Core.ByteSizeType.Bit16 => 2,
                Core.ByteSizeType.Bit32 => 4,
                _ => 1
            };

            // Calculate actual cell width based on stride (using HexViewport's calculation)
            double cellWidth = HexViewport.CalculateCellWidthForByteCount(stride);

            // Use HexViewport's actual calculated dimensions
            double hexStartX = HexViewport.HexPanelStartX;
            double separatorX = HexViewport.SeparatorStartX;
            double asciiStartX = HexViewport.AsciiPanelStartX;

            // Calculate line number from Y coordinate
            double y = mousePosition.Y - TopMargin;
            if (y < 0)
                return VirtualPosition.Invalid;

            int lineIndex = (int)(y / lineHeight);

            // Clamp to valid line range
            if (lineIndex < 0 || lineIndex >= _viewModel.Lines.Count)
                return VirtualPosition.Invalid;

            var line = _viewModel.Lines[lineIndex];
            if (line.Bytes.Count == 0)
                return VirtualPosition.Invalid;

            double x = mousePosition.X;

            // Check if clicked in offset area - select first byte
            if (x < hexStartX)
            {
                return line.Bytes[0].VirtualPos;
            }

            // Check if click is in hex area
            if (x >= hexStartX && x < separatorX)
            {
                // Click in hex area - need to account for byte spacers
                double relativeX = x - hexStartX;

                // Calculate byte index accounting for spacers
                // Phase 4: In multi-byte mode, line.Bytes contains grouped ByteData objects (8 for Bit16, 4 for Bit32)
                int byteIndex = 0;
                double currentX = 0;

                for (int i = 0; i < line.Bytes.Count; i++)
                {
                    // Phase 4: Calculate actual byte position (i * stride) for ByteGrouping check
                    int actualBytePos = i * stride;

                    // Add spacer width before this byte if needed
                    if (_viewModel.BytePerLine >= ByteGrouping && actualBytePos > 0 && actualBytePos % ByteGrouping == 0)
                    {
                        currentX += ByteSpacerWidthTickness;
                    }

                    // Phase 4: Use dynamic cellWidth instead of hardcoded HexByteWidth
                    // Check if click is within this byte's bounds
                    if (relativeX >= currentX && relativeX < currentX + cellWidth + HexByteSpacing)
                    {
                        byteIndex = i;
                        break;
                    }

                    currentX += cellWidth + HexByteSpacing;
                    byteIndex = i;
                }

                // Clamp to valid byte range
                byteIndex = Math.Max(0, Math.Min(byteIndex, line.Bytes.Count - 1));
                return line.Bytes[byteIndex].VirtualPos;
            }

            // Check if click is in ASCII area
            if (x >= asciiStartX)
            {
                // Click in ASCII area - need to account for byte spacers
                double relativeX = x - asciiStartX;

                // Calculate byte index accounting for spacers
                // Phase 4: In multi-byte mode, line.Bytes contains grouped ByteData objects
                // Each ByteData group shows multiple ASCII chars (stride chars)
                int byteIndex = -1; // -1 means no byte found
                double currentX = 0;

                for (int i = 0; i < line.Bytes.Count; i++)
                {
                    // Phase 4: Calculate actual byte position (i * stride) for ByteGrouping check
                    int actualBytePos = i * stride;

                    // Add spacer width before this byte if needed
                    if (_viewModel.BytePerLine >= ByteGrouping && actualBytePos > 0 && actualBytePos % ByteGrouping == 0)
                    {
                        currentX += ByteSpacerWidthTickness;
                    }

                    // Phase 4: In multi-byte mode, each ByteData shows 'stride' ASCII characters
                    // Width = stride * AsciiCharWidth
                    double asciiGroupWidth = stride * AsciiCharWidth;

                    // Check if click is within this byte group's ASCII bounds
                    if (relativeX >= currentX && relativeX < currentX + asciiGroupWidth)
                    {
                        byteIndex = i;
                        break;
                    }

                    currentX += asciiGroupWidth; // Phase 4: Advance by group width, not single char width
                }

                // Validate that the clicked byte actually exists on this line
                if (byteIndex >= 0 && byteIndex < line.Bytes.Count)
                {
                    return line.Bytes[byteIndex].VirtualPos;
                }

                // Click in ASCII area but beyond actual bytes - return Invalid
                return VirtualPosition.Invalid;
            }

            // Click in separator or beyond (empty area) - return Invalid to prevent selection
            return VirtualPosition.Invalid;
        }

        private void Content_KeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null) return;

            bool handled = true;
            bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            // Get current position:
            // - If there's a selection: use SelectionStop (the active/cursor end)
            // - If no selection: use SelectionStart (the cursor position)
            var currentPos = _viewModel.HasSelection && _viewModel.SelectionStop.IsValid
                ? _viewModel.SelectionStop
                : (_viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart : VirtualPosition.Zero);

            VirtualPosition newPos = currentPos;

            // CRITICAL FIX: Calculate stride based on ByteSize for multi-byte navigation
            int stride = _viewModel.ByteSize switch
            {
                Core.ByteSizeType.Bit8 => 1,
                Core.ByteSizeType.Bit16 => 2,
                Core.ByteSizeType.Bit32 => 4,
                _ => 1
            };

            switch (e.Key)
            {
                // Arrow keys navigation
                case Key.Left:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Left: Jump left by 8 bytes (or to start of line if closer)
                        long jumpSize = 8;
                        long lineStart = (currentPos.Value / _viewModel.BytePerLine) * _viewModel.BytePerLine;
                        long targetPos = currentPos.Value - jumpSize;

                        // Don't go before start of current line (unless we're at the very start)
                        if (currentPos.Value > 0)
                        {
                            if (targetPos < lineStart && currentPos.Value != lineStart)
                                newPos = new VirtualPosition(lineStart);
                            else
                                newPos = new VirtualPosition(Math.Max(0, targetPos));
                        }
                    }
                    else
                    {
                        // FIXED: Move by stride (respects ByteSize: 1/2/4 bytes)
                        if (currentPos.Value > 0)
                        {
                            long targetPos = currentPos.Value - stride;
                            // Snap to group boundary (floor division)
                            if (stride > 1)
                                targetPos = (targetPos / stride) * stride;
                            newPos = new VirtualPosition(Math.Max(0, targetPos));
                        }
                    }
                    break;

                case Key.Right:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Right: Jump right by 8 bytes (or to end of line if closer)
                        long jumpSize = 8;
                        long lineStart = (currentPos.Value / _viewModel.BytePerLine) * _viewModel.BytePerLine;
                        long lineEnd = Math.Min(lineStart + _viewModel.BytePerLine - 1, _viewModel.VirtualLength - 1);
                        long targetPos = currentPos.Value + jumpSize;

                        // Don't go past end of current line (unless we're at the very end)
                        if (currentPos.Value < _viewModel.VirtualLength - 1)
                        {
                            if (targetPos > lineEnd && currentPos.Value != lineEnd)
                                newPos = new VirtualPosition(lineEnd);
                            else
                                newPos = new VirtualPosition(Math.Min(_viewModel.VirtualLength - 1, targetPos));
                        }
                    }
                    else
                    {
                        // FIXED: Move by stride (respects ByteSize: 1/2/4 bytes)
                        if (currentPos.Value < _viewModel.VirtualLength - 1)
                        {
                            long targetPos = currentPos.Value + stride;
                            // Snap to group boundary (ceiling division for forward movement)
                            if (stride > 1)
                                targetPos = ((targetPos + stride - 1) / stride) * stride;
                            newPos = new VirtualPosition(Math.Min(_viewModel.VirtualLength - 1, targetPos));
                        }
                    }
                    break;

                case Key.Up:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Up: Jump up by a page
                        long jump = _viewModel.BytePerLine * _viewModel.VisibleLines;
                        newPos = new VirtualPosition(Math.Max(0, currentPos.Value - jump));
                    }
                    else
                    {
                        // Normal Up: Move one line up
                        if (currentPos.Value >= _viewModel.BytePerLine)
                            newPos = new VirtualPosition(currentPos.Value - _viewModel.BytePerLine);
                    }
                    break;

                case Key.Down:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Down: Jump down by a page
                        long jump = _viewModel.BytePerLine * _viewModel.VisibleLines;
                        newPos = new VirtualPosition(Math.Min(_viewModel.VirtualLength - 1, currentPos.Value + jump));
                    }
                    else
                    {
                        // Normal Down: Move one line down
                        if (currentPos.Value + _viewModel.BytePerLine < _viewModel.VirtualLength)
                            newPos = new VirtualPosition(currentPos.Value + _viewModel.BytePerLine);
                    }
                    break;

                // Page Up/Down
                case Key.PageUp:
                    {
                        long jump = _viewModel.BytePerLine * _viewModel.VisibleLines;
                        newPos = new VirtualPosition(Math.Max(0, currentPos.Value - jump));
                    }
                    break;

                case Key.PageDown:
                    {
                        long jump = _viewModel.BytePerLine * _viewModel.VisibleLines;
                        newPos = new VirtualPosition(Math.Min(_viewModel.VirtualLength - 1, currentPos.Value + jump));
                    }
                    break;

                // Home/End
                case Key.Home:
                    if (isCtrlPressed)
                    {
                        // Ctrl+Home: Go to start of file
                        newPos = VirtualPosition.Zero;
                    }
                    else
                    {
                        // Home: Go to start of current line
                        long lineStart = (currentPos.Value / _viewModel.BytePerLine) * _viewModel.BytePerLine;
                        newPos = new VirtualPosition(lineStart);
                    }
                    break;

                case Key.End:
                    if (isCtrlPressed)
                    {
                        // Ctrl+End: Go to end of file
                        newPos = new VirtualPosition(_viewModel.VirtualLength - 1);
                    }
                    else
                    {
                        // End: Go to end of current line
                        long lineStart = (currentPos.Value / _viewModel.BytePerLine) * _viewModel.BytePerLine;
                        long lineEnd = Math.Min(lineStart + _viewModel.BytePerLine - 1, _viewModel.VirtualLength - 1);
                        newPos = new VirtualPosition(lineEnd);
                    }
                    break;

                // Insert key: Toggle edit mode
                case Key.Insert:
                    _viewModel.EditMode = _viewModel.EditMode == EditMode.Insert
                        ? EditMode.Overwrite
                        : EditMode.Insert;
                    break;

                // Delete key: Delete selection (multi-byte aware)
                case Key.Delete:
                    if (!_viewModel.ReadOnlyMode)
                    {
                        // Use stride calculated at method start (lines 298-305)
                        // No need to recalculate - already available in scope

                        if (_viewModel.HasSelection && _viewModel.SelectionStart != _viewModel.SelectionStop)
                        {
                            long start = Math.Min(_viewModel.SelectionStart.Value, _viewModel.SelectionStop.Value);
                            long stop = Math.Max(_viewModel.SelectionStart.Value, _viewModel.SelectionStop.Value);

                            // Multi-byte mode: Expand to group boundaries
                            if (stride > 1)
                            {
                                // Snap start DOWN to group boundary (floor division)
                                start = (start / stride) * stride;

                                // Snap stop UP to include full group (ceiling division)
                                stop = ((stop + stride - 1) / stride) * stride - 1;

                                // Ensure we don't go past file end
                                stop = Math.Min(stop, _viewModel.VirtualLength - 1);
                            }

                            // OPTIMIZED: Use bulk deletion instead of loop
                            long length = stop - start + 1;
                            _viewModel.DeleteBytes(start, length);
                        }
                        else if (_viewModel.SelectionStart.IsValid)
                        {
                            // No selection: Delete stride bytes at cursor position
                            long cursorPos = _viewModel.SelectionStart.Value;

                            // Snap cursor to group boundary
                            long alignedStart = (cursorPos / stride) * stride;
                            int bytesToDelete = (int)Math.Min(stride, _viewModel.VirtualLength - alignedStart);

                            // OPTIMIZED: Use bulk deletion instead of loop
                            _viewModel.DeleteBytes(alignedStart, bytesToDelete);
                        }

                        // CRITICAL UX FIX: After deletion, position cursor (with scroll) and restore focus
                        if (_viewModel.SelectionStart.IsValid)
                        {
                            SetPosition(_viewModel.SelectionStart.Value);
                        }

                        // Ensure keyboard focus is on the control for immediate input
                        Focus();
                    }
                    break;

                // Ctrl+Z: Undo
                case Key.Z:
                    if (isCtrlPressed && !_viewModel.ReadOnlyMode)
                    {
                        Undo();
                    }
                    break;

                // Ctrl+Y: Redo
                case Key.Y:
                    if (isCtrlPressed && !_viewModel.ReadOnlyMode)
                    {
                        Redo();
                    }
                    break;

                // Ctrl+C: Copy
                case Key.C:
                    if (isCtrlPressed && _viewModel.HasSelection)
                    {
                        Copy();
                    }
                    break;

                // Ctrl+X: Cut
                case Key.X:
                    if (isCtrlPressed && _viewModel.HasSelection && !_viewModel.ReadOnlyMode)
                    {
                        Cut();
                    }
                    break;

                // Ctrl+V: Paste
                case Key.V:
                    if (isCtrlPressed && !_viewModel.ReadOnlyMode)
                    {
                        Paste();
                    }
                    break;

                // Ctrl+A: Select all
                case Key.A:
                    if (isCtrlPressed)
                    {
                        SelectAll();
                    }
                    break;

                // F2: Next Bookmark
                case Key.F2:
                    if (isShiftPressed)
                        GoToPreviousBookmark();
                    else
                        GoToNextBookmark();
                    break;

                // Ctrl+B: Toggle Bookmark
                case Key.B:
                    if (isCtrlPressed)
                    {
                        SetBookmarkMenuItem_Click(null, null);
                    }
                    break;

                // Text/Hex input editing
                default:
                    if (!_viewModel.ReadOnlyMode)
                    {
                        // ASCII mode: Handle text input (A-Z, a-z, 0-9, space, punctuation)
                        if (_isAsciiEditMode && TryGetAsciiChar(e.Key, out char asciiChar))
                        {
                            HandleAsciiInput(asciiChar, currentPos);
                            handled = true;
                        }
                        // Format-aware byte editing (Hex/Decimal/Binary)
                        else if (!_isAsciiEditMode)
                        {
                            switch (DataStringVisual)
                            {
                                case DataVisualType.Hexadecimal:
                                    if (TryGetHexValue(e.Key, out byte hexValue))
                                    {
                                        HandleHexInput(hexValue, currentPos);
                                        handled = true;
                                    }
                                    else
                                    {
                                        handled = false;
                                    }
                                    break;

                                case DataVisualType.Decimal:
                                    if (TryGetDecimalDigit(e.Key, out int decimalDigit))
                                    {
                                        HandleDecimalInput(decimalDigit, currentPos);
                                        handled = true;
                                    }
                                    else
                                    {
                                        handled = false;
                                    }
                                    break;

                                case DataVisualType.Binary:
                                    if (TryGetBinaryDigit(e.Key, out int binaryDigit))
                                    {
                                        HandleBinaryInput(binaryDigit, currentPos);
                                        handled = true;
                                    }
                                    else
                                    {
                                        handled = false;
                                    }
                                    break;

                                default:
                                    handled = false;
                                    break;
                            }
                        }
                        else
                        {
                            handled = false;
                        }
                    }
                    else
                    {
                        handled = false;
                    }
                    break;
            }

            // Update selection based on navigation
            if (newPos != currentPos)
            {
                if (isShiftPressed)
                {
                    // Shift+navigation: Extend selection
                    _viewModel.ExtendSelection(newPos);
                }
                else
                {
                    // Normal navigation: Move cursor
                    _viewModel.SetSelection(newPos);
                }

                // Auto-scroll to keep selection visible
                EnsurePositionVisible(newPos);
            }

            if (handled)
                e.Handled = true;
        }

        /// <summary>
        /// Ensure a virtual position is visible by scrolling if necessary
        /// </summary>
        private void EnsurePositionVisible(VirtualPosition position)
        {
            if (_viewModel == null || !position.IsValid || HexViewport == null)
                return;

            long bytePosition = position.Value;
            long lineNumber = bytePosition / _viewModel.BytePerLine;
            long currentScroll = _viewModel.ScrollPosition;

            // Scroll up if position is above viewport
            if (lineNumber < currentScroll)
            {
                _viewModel.ScrollPosition = lineNumber;
                VerticalScroll.Value = lineNumber; // Sync scrollbar visual
            }
            // Scroll down if cursor moves beyond the last visible byte (Legacy behavior)
            // This accounts for status bar and other UI elements that may hide bottom lines
            // EXCEPT when all content fits in viewport (nothing to scroll)
            else
            {
                // Disable adjustment if all content is visible (handles resize dynamically)
                // Add +4 to account for lines hidden by status bar (extra margin for safety)
                long maxScroll = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 4);

                // Only apply scroll adjustment if:
                // 1. Scrolling is possible (maxScroll > 0)
                // 2. Not already scrolled to the bottom (currentScroll < maxScroll)
                if (maxScroll > 0 && currentScroll < maxScroll)
                {
                    long lastVisibleByte = HexViewport.LastVisibleBytePosition;
                    if (lastVisibleByte >= 0 && bytePosition > lastVisibleByte)
                    {
                        _viewModel.ScrollPosition = currentScroll + 1;
                        VerticalScroll.Value = currentScroll + 1; // Sync scrollbar visual
                    }
                }
            }
        }

        private void VerticalScroll_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.ScrollPosition = (long)e.NewValue;
            }
        }

        /// <summary>
        /// <summary>
        /// Handle BaseGrid size changes to adjust visible lines (exact V1 approach)
        /// </summary>
        private void BaseGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Only react to height changes (like V1's Grid_SizeChanged)
            if (!e.HeightChanged || _viewModel == null)
                return;

            // Delay UpdateVisibleLines to ensure BaseGrid.RowDefinitions[1].ActualHeight is updated
            // Use Render priority for better responsiveness while still ensuring layout is complete
            Dispatcher.BeginInvoke(new Action(() => UpdateVisibleLines()), System.Windows.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// Handle mouse wheel scrolling on ContentScroller
        /// </summary>
        private void ContentScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_viewModel == null)
                return;

            // Ctrl+MouseWheel = Zoom
            if (Keyboard.Modifiers == ModifierKeys.Control && AllowZoom)
            {
                double delta = e.Delta > 0 ? 0.1 : -0.1;
                double newZoom = ZoomScale + delta;
                ZoomScale = Math.Max(0.5, Math.Min(2.0, newZoom));
                e.Handled = true;
                return;
            }

            // Standard behavior: scroll 3 lines per wheel notch
            // Delta is typically 120 per notch, so divide by 40 to get 3 lines
            int linesToScroll = -e.Delta / 40;

            long newScrollPos = _viewModel.ScrollPosition + linesToScroll;
            long maxScroll = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines);

            // Clamp to valid range
            newScrollPos = Math.Max(0, Math.Min(maxScroll, newScrollPos));

            if (_viewModel.ScrollPosition != newScrollPos)
            {
                _viewModel.ScrollPosition = newScrollPos;
                VerticalScroll.Value = newScrollPos;
            }

            // Mark event as handled to prevent ScrollViewer from scrolling
            e.Handled = true;
        }

        /// <summary>
        /// Update visible lines based on BaseGrid Row 1 height (exact V1 approach)
        /// V1 uses: (int)(BaseGrid.RowDefinitions[1].ActualHeight / (LineHeight * ZoomScale)) + 1
        /// V2 uses: (int)(BaseGrid.RowDefinitions[1].ActualHeight / LineHeight) + 1 (no ZoomScale)
        /// </summary>
        private void UpdateVisibleLines()
        {
            if (_viewModel == null)
                return;

            // Get the actual line height from HexViewport
            double lineHeight = HexViewport.LineHeight;
            if (lineHeight <= 0)
                return; // Not initialized yet

            // Use BaseGrid.RowDefinitions[1].ActualHeight (EXACTLY like V1 does)
            // Row 0 = Header, Row 1 = Content area, Row 2 = Status bar
            double actualHeight = BaseGrid.RowDefinitions[1].ActualHeight;
            if (actualHeight <= 0)
                return; // Not initialized yet

            // Calculate how many lines fit in the viewport
            // Use Math.Ceiling to ensure we always have enough space for partial lines at bottom
            // Add 2 extra lines to prevent last line from being cut off (increased safety margin)
            int calculatedLines = (int)Math.Ceiling(actualHeight / lineHeight) + 2;

            // Clamp to reasonable range (minimum 5, maximum 100)
            calculatedLines = Math.Max(5, Math.Min(100, calculatedLines));

            // Only update if different (avoid thrashing)
            if (_viewModel.VisibleLines != calculatedLines)
            {
                _viewModel.VisibleLines = calculatedLines; // Property setter calls RefreshVisibleLines() internally
                VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
                VerticalScroll.ViewportSize = _viewModel.VisibleLines;

                // Force full refresh after collection update completes (like V1's RefreshView(true) call)
                // Use Dispatcher to ensure collection changes are processed before refreshing viewport
                Dispatcher.BeginInvoke(new Action(() => HexViewport.Refresh()), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(HexEditorViewModel.SelectionLength):
                    UpdateSelectionInfo();
                    break;

                case nameof(HexEditorViewModel.SelectionStart):
                    UpdatePositionInfo();
                    // Update HexViewport selection anchor
                    HexViewport.SelectionStart = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : -1;
                    // Sync DependencyProperties for TwoWay binding in settings panel
                    var newStart = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : -1L;
                    if (SelectionStart != newStart)
                        SetValue(SelectionStartProperty, newStart);
                    // Position is an alias for SelectionStart, sync it too
                    if (newStart >= 0 && GetValue(PositionProperty) is long currentPos && currentPos != newStart)
                        SetValue(PositionProperty, newStart);
                    // Update auto-highlight to match the byte at the new selection
                    UpdateAutoHighlightByte();
                    // Update scroll markers selection bar
                    UpdateScrollMarkersSelection();
                    // Refresh column headers to highlight the SelectionStart column
                    RefreshColumnHeader();
                    break;

                case nameof(HexEditorViewModel.SelectionStop):
                    // Update HexViewport cursor (active end) and selection
                    // Cursor is at SelectionStop (the end that moves during Shift+navigation)
                    HexViewport.CursorPosition = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value :
                                                 (_viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : 0);
                    HexViewport.SelectionStop = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value : -1;

                    // Initialize editing visual feedback: show first character/nibble as bold and ready to edit
                    if (!_viewModel.ReadOnlyMode && _viewModel.SelectionStop.IsValid)
                    {
                        HexViewport.EditingBytePosition = _viewModel.SelectionStop.Value;
                        HexViewport.EditingNibbleIndex = 0; // First character is ready to edit
                    }
                    else
                    {
                        HexViewport.EditingBytePosition = -1; // Clear if read-only or invalid
                    }

                    // Sync DependencyProperty for TwoWay binding in settings panel
                    var newStop = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value : -1L;
                    if (SelectionStop != newStop)
                        SetValue(SelectionStopProperty, newStop);
                    // Update scroll markers selection bar
                    UpdateScrollMarkersSelection();
                    break;

                case nameof(HexEditorViewModel.TotalLines):
                    var newMaximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
                    VerticalScroll.Maximum = newMaximum;
                    // Update file size display (VirtualLength may have changed due to insertions)
                    UpdateFileSizeDisplay();
                    break;

                case nameof(HexEditorViewModel.VirtualLength):
                    // VirtualLength changed due to insertions/deletions
                    // Update scroll markers FileLength and refresh markers
                    if (_scrollMarkers != null)
                    {
                        _scrollMarkers.FileLength = _viewModel.VirtualLength;
                    }
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateScrollMarkers();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                    break;

                case nameof(HexEditorViewModel.EditMode):
                    EditModeText.Text = $"Mode: {_viewModel.EditMode}";
                    // Sync DependencyProperty for TwoWay binding in settings panel
                    if (EditMode != _viewModel.EditMode)
                        SetValue(EditModeProperty, _viewModel.EditMode);
                    break;

                case nameof(HexEditorViewModel.BytePerLine):
                    BytesPerLineText.Text = $"Bytes/Line: {_viewModel.BytePerLine}";
                    HexViewport.BytesPerLine = _viewModel.BytePerLine;
                    // Sync DependencyProperty for TwoWay binding in settings panel
                    if (BytePerLine != _viewModel.BytePerLine)
                        SetValue(BytePerLineProperty, _viewModel.BytePerLine);
                    RefreshColumnHeader(); // Regenerate headers to match new BytesPerLine
                    break;

                case nameof(HexEditorViewModel.ByteSize):
                    // Phase 5: Sync ByteSize DP for TwoWay binding in settings panel
                    System.Diagnostics.Debug.WriteLine($"[ViewModel PropertyChanged] ByteSize changed to: {_viewModel.ByteSize}");
                    if (ByteSize != _viewModel.ByteSize)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ViewModel PropertyChanged] Syncing DP: {ByteSize} → {_viewModel.ByteSize}");
                        SetValue(ByteSizeProperty, _viewModel.ByteSize);
                    }
                    RefreshColumnHeader(); // Update headers for new stride
                    break;

                case nameof(HexEditorViewModel.ByteOrder):
                    // Phase 5: Sync ByteOrder DP for TwoWay binding in settings panel
                    if (ByteOrder != _viewModel.ByteOrder)
                        SetValue(ByteOrderProperty, _viewModel.ByteOrder);
                    // ByteOrder change is visual only, no header update needed
                    break;
            }

            // FIX: Update IsModified DP after any property change (in case edits happened)
            UpdateIsModifiedState();
        }

        /// <summary>
        /// Updates IsModified DP based on ByteProvider UndoCount
        /// </summary>
        private void UpdateIsModifiedState()
        {
            if (_viewModel?.Provider != null)
            {
                var isModified = _viewModel.Provider.UndoCount > 0;
                if (IsModified != isModified)
                    IsModified = isModified;
            }
        }

        private void UpdateFileSizeDisplay()
        {
            if (_viewModel != null)
            {
                // Show VirtualLength (includes insertions in Insert mode)
                long displayLength = _viewModel.VirtualLength;
                FileSizeText.Text = $"Size: {FormatFileSize(displayLength)}";
            }
        }

        private void UpdateSelectionInfo()
        {
            if (_viewModel?.HasSelection == true)
            {
                SelectionInfo.Text = $"Selection: {_viewModel.SelectionLength} bytes";
            }
            else
            {
                SelectionInfo.Text = "No selection";
            }
        }

        private void UpdatePositionInfo()
        {
            if (_viewModel?.SelectionStart.IsValid == true)
            {
                PositionInfo.Text = $"Position: 0x{_viewModel.SelectionStart.Value:X}";
            }
            else
            {
                PositionInfo.Text = "Position: 0";
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]} ({bytes:N0} bytes)";
        }

        /// <summary>
        /// Try to convert a Key to a hex value (0-15)
        /// </summary>
        private bool TryGetHexValue(Key key, out byte value)
        {
            // Number keys (0-9)
            if (key >= Key.D0 && key <= Key.D9)
            {
                value = (byte)(key - Key.D0);
                return true;
            }

            // Numpad keys (0-9)
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                value = (byte)(key - Key.NumPad0);
                return true;
            }

            // Letter keys (A-F)
            if (key >= Key.A && key <= Key.F)
            {
                value = (byte)(key - Key.A + 10);
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Try to get decimal digit (0-9) from key press
        /// </summary>
        private bool TryGetDecimalDigit(Key key, out int digit)
        {
            digit = 0;

            // Number row (0-9)
            if (key >= Key.D0 && key <= Key.D9)
            {
                digit = (int)(key - Key.D0);
                return true;
            }

            // Numpad (0-9)
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                digit = (int)(key - Key.NumPad0);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try to get binary digit (0 or 1) from key press
        /// </summary>
        private bool TryGetBinaryDigit(Key key, out int digit)
        {
            digit = 0;

            // Only accept 0 or 1
            if (key == Key.D0 || key == Key.NumPad0)
            {
                digit = 0;
                return true;
            }

            if (key == Key.D1 || key == Key.NumPad1)
            {
                digit = 1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try to convert a key press to an ASCII character
        /// </summary>
        private bool TryGetAsciiChar(Key key, out char asciiChar)
        {
            bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            // Letters A-Z
            if (key >= Key.A && key <= Key.Z)
            {
                asciiChar = (char)('A' + (key - Key.A));
                if (!isShiftPressed)
                    asciiChar = char.ToLower(asciiChar);
                return true;
            }

            // Digits 0-9 (with shift for symbols)
            if (key >= Key.D0 && key <= Key.D9)
            {
                if (isShiftPressed)
                {
                    // Shift+digit produces symbols: )!@#$%^&*(
                    char[] shiftDigits = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
                    asciiChar = shiftDigits[key - Key.D0];
                }
                else
                {
                    asciiChar = (char)('0' + (key - Key.D0));
                }
                return true;
            }

            // NumPad digits 0-9
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                asciiChar = (char)('0' + (key - Key.NumPad0));
                return true;
            }

            // Space
            if (key == Key.Space)
            {
                asciiChar = ' ';
                return true;
            }

            // Common punctuation (basic ASCII support)
            var punctuation = new Dictionary<Key, (char normal, char shifted)>
            {
                { Key.OemPeriod, ('.', '>') },
                { Key.OemComma, (',', '<') },
                { Key.OemSemicolon, (';', ':') },
                { Key.OemQuotes, ('\'', '"') },
                { Key.OemQuestion, ('/', '?') },
                { Key.OemOpenBrackets, ('[', '{') },
                { Key.OemCloseBrackets, (']', '}') },
                { Key.OemPipe, ('\\', '|') },
                { Key.OemMinus, ('-', '_') },
                { Key.OemPlus, ('=', '+') },
                { Key.OemTilde, ('`', '~') }
            };

            if (punctuation.TryGetValue(key, out var chars))
            {
                asciiChar = isShiftPressed ? chars.shifted : chars.normal;
                return true;
            }

            asciiChar = '\0';
            return false;
        }

        /// <summary>
        /// Handle ASCII text input for editing bytes in ASCII mode
        /// </summary>
        private void HandleAsciiInput(char asciiChar, VirtualPosition currentPos)
        {

            if (_viewModel == null || _viewModel.ReadOnlyMode)
                return;

            // Convert ASCII character to byte
            byte byteValue = (byte)asciiChar;

            // Determine action based on edit mode
            if (_viewModel.EditMode == EditMode.Insert)
            {
                // Insert mode: insert new byte
                _viewModel.InsertByte(currentPos, byteValue);
            }
            else
            {
                // Overwrite mode: modify existing byte
                _viewModel.ModifyByte(currentPos, byteValue);
            }

            // Move to next byte
            var nextPos = new VirtualPosition(currentPos.Value + 1);
            if (nextPos.Value < _viewModel.VirtualLength)
            {
                _viewModel.SetSelection(nextPos);
                EnsurePositionVisible(nextPos);
            }

            // Update status
            StatusText.Text = $"ASCII input: '{asciiChar}' at {currentPos.Value:X8}";
        }

        /// <summary>
        /// Handle hex digit input for editing bytes
        /// </summary>
        private void HandleHexInput(byte hexValue, VirtualPosition currentPos)
        {

            if (_viewModel == null || _viewModel.ReadOnlyMode)
                return;

            // Start new byte edit if not currently editing, or position changed
            // SPECIAL CASE: In Insert mode, if waiting for low nibble and position drifted by ±1,
            // DON'T reset - this is position drift after InsertByte, continue with same edit
            bool shouldResetEdit = false;

            if (!_isEditingByte)
            {
                // Not currently editing - definitely start new edit
                shouldResetEdit = true;
            }
            else if (_editingPosition != currentPos)
            {
                // Position changed - check if this is acceptable drift in Insert mode
                if (_viewModel.EditMode == EditMode.Insert && _editingCharIndex == 1)
                {
                    // We're in Insert mode, waiting for low nibble (second char), and position drifted
                    long drift = Math.Abs(currentPos.Value - _editingPosition.Value);
                    if (drift <= 1)
                    {
                        // Position drift of ±1 is acceptable in Insert mode after InsertByte
                        // Force currentPos back to _editingPosition and continue same edit
                        currentPos = _editingPosition; // Force back to editing position
                        shouldResetEdit = false;
                    }
                    else
                    {
                        // Drift is too large - must be a real position change
                        shouldResetEdit = true;
                    }
                }
                else
                {
                    // In Overwrite mode or editing first char - any position change means new edit
                    shouldResetEdit = true;
                }
            }

            if (shouldResetEdit)
            {
                _isEditingByte = true;
                _editingPosition = currentPos;
                _editingCharIndex = 0;
                _editingMaxChars = 2;  // Hexadecimal: 2 characters
                _editingBuffer = "";

                // Update HexViewport to show bold nibble
                HexViewport.EditingBytePosition = currentPos.Value;
                HexViewport.EditingNibbleIndex = 0;

                // Initialize editing value based on mode
                // Insert mode: start with 0x00 (creating new byte)
                // Overwrite mode: start with existing byte value (modifying it)
                if (_viewModel.EditMode == EditMode.Insert)
                {
                    _editingValue = 0;
                }
                else
                {
                    var physicalPos = _viewModel.VirtualToPhysical(currentPos);
                    _editingValue = physicalPos.IsValid
                        ? _viewModel.GetByteAt(currentPos)
                        : (byte)0;
                }

            }

            // Update the appropriate nibble based on character index
            if (_editingCharIndex == 0)
            {
                // First character: Update high nibble (bits 4-7)
                byte oldValue = _editingValue;
                _editingValue = (byte)((_editingValue & 0x0F) | (hexValue << 4));

                _editingCharIndex = 1; // Move to second character (low nibble)
                HexViewport.EditingNibbleIndex = 1; // Update visual feedback

                // IN INSERT MODE: Insert byte IMMEDIATELY after first nibble (don't wait for second nibble)
                if (_viewModel.EditMode == EditMode.Insert)
                {

                    // Save the insertion position BEFORE calling InsertByte
                    var insertionPos = _editingPosition;

                    _viewModel.InsertByte(insertionPos, _editingValue);

                    // CRITICAL FIX: Force synchronous position update to prevent drift
                    // Use Dispatcher.Invoke with Send priority to ensure position is set IMMEDIATELY
                    // before any other UI updates or events can interfere
                    Dispatcher.Invoke(() =>
                    {
                        _viewModel.SetSelection(insertionPos);

                        // Verify the position was set correctly
                        var actualPos = _viewModel.SelectionStart;

                        if (actualPos != insertionPos)
                        {
                        }
                    }, System.Windows.Threading.DispatcherPriority.Send);

                    // Keep _editingPosition at the insertion point (where the byte was inserted)
                    // The low nibble MUST modify this same position
                    _editingPosition = insertionPos;

                }
                else
                {
                    // OVERWRITE MODE: Update visual display immediately after high nibble (preview only)
                    UpdateBytePreview(_editingPosition, _editingValue);
                }
            }
            else // _editingCharIndex == 1
            {
                // Second character: Update low nibble (bits 0-3)
                byte oldValue = _editingValue;
                _editingValue = (byte)((_editingValue & 0xF0) | hexValue);

                // Byte is complete, write it
                CommitByteEdit();

                // Move to next byte
                var nextPos = new VirtualPosition(currentPos.Value + 1);

                if (nextPos.Value < _viewModel.VirtualLength)
                {
                    _viewModel.SetSelection(nextPos);
                    EnsurePositionVisible(nextPos);
                }
                else
                {
                    _isEditingByte = false;
                    HexViewport.EditingBytePosition = -1; // Clear bold nibble feedback
                }
            }
        }

        /// <summary>
        /// Handle decimal digit input for editing bytes (0-255)
        /// Allows typing 3 decimal digits to enter byte values
        /// </summary>
        private void HandleDecimalInput(int decimalDigit, VirtualPosition currentPos)
        {
            if (_viewModel == null || _viewModel.ReadOnlyMode)
                return;

            // Check if starting new byte edit
            if (!_isEditingByte || _editingPosition != currentPos)
            {
                // Initialize decimal edit
                _isEditingByte = true;
                _editingPosition = currentPos;
                _editingCharIndex = 0;
                _editingMaxChars = 3;  // Decimal: 3 digits (000-255)
                _editingBuffer = "";

                // Update HexViewport to show bold character
                HexViewport.EditingBytePosition = currentPos.Value;
                HexViewport.EditingNibbleIndex = 0;

                // Get existing byte value in Overwrite mode, or 0 in Insert mode
                if (_viewModel.EditMode == EditMode.Insert)
                {
                    _editingValue = 0;
                }
                else
                {
                    var physicalPos = _viewModel.VirtualToPhysical(currentPos);
                    _editingValue = physicalPos.IsValid
                        ? _viewModel.GetByteAt(currentPos)
                        : (byte)0;
                }
            }

            // Append digit to buffer
            _editingBuffer += decimalDigit.ToString();
            _editingCharIndex++;
            HexViewport.EditingNibbleIndex = _editingCharIndex; // Update visual feedback

            // Parse buffer with position-by-position logic (left to right like Hex)
            // Pad right with zeros for remaining positions: "1" → "100", "12" → "120", "123" → "123"
            int charsRemaining = _editingMaxChars - _editingCharIndex; // How many positions left
            string paddedBuffer = _editingBuffer.PadRight(_editingBuffer.Length + charsRemaining, '0');

            if (int.TryParse(paddedBuffer, out int decimalValue))
            {
                // Validate range (0-255)
                if (decimalValue > 255)
                {
                    // Invalid: reset to previous state
                    _editingBuffer = _editingBuffer.Substring(0, _editingBuffer.Length - 1);
                    _editingCharIndex--;
                    HexViewport.EditingNibbleIndex = _editingCharIndex;
                    return;
                }

                // Update editing value
                _editingValue = (byte)decimalValue;

                // Show preview
                UpdateBytePreview(_editingPosition, _editingValue);

                // Commit if reached max chars OR user typed a 2-digit value where adding any third digit would exceed 255
                bool shouldCommit = (_editingCharIndex >= _editingMaxChars) ||
                                   (_editingCharIndex == 2 && int.TryParse(_editingBuffer, out int bufferValue) && bufferValue >= 26);  // "26"-"99" can't become valid 3-digit

                if (shouldCommit)
                {
                    CommitByteEdit();
                    _isEditingByte = false;

                    // Move to next byte
                    var nextPos = new VirtualPosition(currentPos.Value + 1);
                    if (nextPos.Value < _viewModel.VirtualLength)
                    {
                        _viewModel.SetSelection(nextPos);
                        EnsurePositionVisible(nextPos);
                    }
                }
            }
        }

        /// <summary>
        /// Handle binary digit input for editing bytes (00000000-11111111)
        /// Allows typing 8 binary digits (0 or 1) to enter byte values
        /// </summary>
        private void HandleBinaryInput(int binaryDigit, VirtualPosition currentPos)
        {
            if (_viewModel == null || _viewModel.ReadOnlyMode)
                return;

            // Check if starting new byte edit
            if (!_isEditingByte || _editingPosition != currentPos)
            {
                // Initialize binary edit
                _isEditingByte = true;
                _editingPosition = currentPos;
                _editingCharIndex = 0;
                _editingMaxChars = 8;  // Binary: 8 bits
                _editingBuffer = "";

                // Update HexViewport to show bold character
                HexViewport.EditingBytePosition = currentPos.Value;
                HexViewport.EditingNibbleIndex = 0;

                // Get existing byte value in Overwrite mode, or 0 in Insert mode
                if (_viewModel.EditMode == EditMode.Insert)
                {
                    _editingValue = 0;
                }
                else
                {
                    var physicalPos = _viewModel.VirtualToPhysical(currentPos);
                    _editingValue = physicalPos.IsValid
                        ? _viewModel.GetByteAt(currentPos)
                        : (byte)0;
                }
            }

            // Append bit to buffer
            _editingBuffer += binaryDigit.ToString();
            _editingCharIndex++;
            HexViewport.EditingNibbleIndex = _editingCharIndex; // Update visual feedback

            // Parse buffer with position-by-position logic (left to right like Hex)
            // Pad right with zeros for remaining positions: "1" → "10000000", "10" → "10000000", etc.
            int bitsRemaining = _editingMaxChars - _editingCharIndex; // How many bits left
            string paddedBinary = _editingBuffer.PadRight(_editingBuffer.Length + bitsRemaining, '0');
            _editingValue = Convert.ToByte(paddedBinary, 2);

            // Show preview
            UpdateBytePreview(_editingPosition, _editingValue);

            // Commit if reached 8 bits
            if (_editingCharIndex >= _editingMaxChars)
            {
                CommitByteEdit();
                _isEditingByte = false;

                // Move to next byte
                var nextPos = new VirtualPosition(currentPos.Value + 1);
                if (nextPos.Value < _viewModel.VirtualLength)
                {
                    _viewModel.SetSelection(nextPos);
                    EnsurePositionVisible(nextPos);
                }
            }
        }

        /// <summary>
        /// Commit the current byte edit to the provider
        /// </summary>
        /// <summary>
        /// Update byte display temporarily during editing (before commit)
        /// Shows real-time feedback as user types each nibble
        /// </summary>
        private void UpdateBytePreview(VirtualPosition position, byte previewValue)
        {
            if (_viewModel == null || !position.IsValid)
                return;

            // Update the byte value in the ViewModel temporarily for preview
            _viewModel.UpdateBytePreview(position, previewValue);

            // Force visual refresh to show the change immediately
            HexViewport.InvalidateVisual();
        }

        private void CommitByteEdit()
        {
            if (!_isEditingByte || _viewModel == null)
            {
                return;
            }


            // In Insert mode, the byte was already inserted after the first nibble
            // So the low nibble should MODIFY the inserted byte (not insert again)
            // In Overwrite mode, modify the existing byte
            _viewModel.ModifyByte(_editingPosition, _editingValue);

            // Reset all editing state variables
            _isEditingByte = false;
            _editingPosition = VirtualPosition.Invalid;
            HexViewport.EditingBytePosition = -1; // Clear bold nibble feedback
            _editingValue = 0;
            _editingCharIndex = 0;
            _editingMaxChars = 2;
            _editingBuffer = "";

            // Update status
            StatusText.Text = $"Edited byte at {_editingPosition.Value:X8}";

            // Update scroll markers in background (low priority)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateScrollMarkers();
            }), System.Windows.Threading.DispatcherPriority.Background);

        }

        #endregion
    }
}
