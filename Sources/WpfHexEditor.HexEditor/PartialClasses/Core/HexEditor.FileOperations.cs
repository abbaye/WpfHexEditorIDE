// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.FileOperations.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class containing file open/save/close operations for the HexEditor.
//     Manages file path handling, read-only detection, dirty state tracking,
//     and integration with the ScrollMarkerPanel for file visualization.
//     Includes external file-change detection via FileSystemWatcher with 500 ms debounce.
//
// Architecture Notes:
//     File I/O delegates to HexViewport and the underlying stream/data model.
//     Fires FileSaved/FileOpened/FileClosed events for external integration.
//     FileSystemWatcher lifetime is tied to the open file: created in OpenFile(),
//     disposed in Close(). External changes are marshalled to the UI thread via Dispatcher.
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Core.Events;
using WpfHexEditor.HexEditor.Controls;
using WpfHexEditor.Core.Models;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - File Operations
    /// Contains methods for opening, saving, and closing files
    /// </summary>
    public partial class HexEditor
    {
        #region External File-Change Detection

        // FileSystemWatcher watching the currently open file for external modifications.
        private FileSystemWatcher? _fileWatcher;

        // Debounce timer: coalesces rapid FS events into a single notification (500 ms).
        private Timer? _fileWatcherDebounce;
        private const int FileWatcherDebounceMs = 500;

        /// <summary>
        /// Raised on the UI thread when the file currently open in the editor has been
        /// modified by an external process. Use <see cref="ExternalFileChangedEventArgs.HasUnsavedChanges"/>
        /// to decide whether to auto-reload or prompt the user.
        /// </summary>
        public event EventHandler<ExternalFileChangedEventArgs>? FileExternallyChanged;

        /// <summary>
        /// Reload the file content from disk, discarding all pending in-memory edits.
        /// The underlying FileStream is kept open — only the cache is invalidated.
        /// </summary>
        public void ReloadFromDisk()
        {
            if (_viewModel == null) return;

            _viewModel.ReloadFromDisk();

            // Refresh scroll markers to reflect new file length
            if (_scrollMarkers != null)
            {
                _scrollMarkers.FileLength = _viewModel.VirtualLength;
                Dispatcher.BeginInvoke(new Action(UpdateScrollMarkers),
                    System.Windows.Threading.DispatcherPriority.Background);
            }

            IsModified = false;
            StatusText.Text = $"Reloaded: {System.IO.Path.GetFileName(FileName)}";
            UpdateFileSizeDisplay();
            RaiseHexStatusChanged();
        }

        private void StartFileWatcher(string filePath)
        {
            StopFileWatcher();

            var directory = System.IO.Path.GetDirectoryName(filePath);
            var fileName  = System.IO.Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) return;

            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents   = true,
                IncludeSubdirectories = false,
            };

            _fileWatcher.Changed += OnFileWatcherEvent;
            _fileWatcher.Error   += OnFileWatcherError;
        }

        private void StopFileWatcher()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Changed -= OnFileWatcherEvent;
                _fileWatcher.Error   -= OnFileWatcherError;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            _fileWatcherDebounce?.Dispose();
            _fileWatcherDebounce = null;
        }

        private void OnFileWatcherEvent(object sender, FileSystemEventArgs e)
        {
            // Debounce: reset timer on each FS event; callback fires 500 ms after last event.
            _fileWatcherDebounce?.Dispose();
            _fileWatcherDebounce = new Timer(_ => OnFileChangedDebounced(), null,
                FileWatcherDebounceMs, Timeout.Infinite);
        }

        private void OnFileWatcherError(object sender, ErrorEventArgs e)
        {
            // Watcher can fail on network paths or after too many pending events.
            // Restart it silently so detection continues.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!string.IsNullOrEmpty(FileName))
                    StartFileWatcher(FileName);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnFileChangedDebounced()
        {
            // Marshal to UI thread — all WPF access must be on the dispatcher thread.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_viewModel == null || string.IsNullOrEmpty(FileName)) return;

                bool hasUnsavedChanges = _viewModel.Provider?.HasChanges ?? false;
                var args = new ExternalFileChangedEventArgs(FileName, hasUnsavedChanges);

                // Primary path: IDE (or any host) handles the event and decides what to show.
                FileExternallyChanged?.Invoke(this, args);

                // Standalone fallback: if no subscriber is listening and there are no unsaved
                // changes, auto-reload silently so the control remains useful without a host IDE.
                if (FileExternallyChanged is null && !hasUnsavedChanges)
                    ReloadFromDisk();
            }));
        }

        #endregion

        #region Public Methods - File Operations

        /// <summary>
        /// Open a file for editing.
        /// Returns immediately — file I/O and initialization run asynchronously on a background thread.
        /// The hex editor shows "Opening…" status until content is ready.
        /// </summary>
        public void OpenFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            // CRITICAL: Set flag to prevent infinite recursion in OnFileNamePropertyChanged
            _isOpeningFile = true;

            // Show status immediately — the tab is not blank while the background FileStream open runs.
            StatusText.Text = $"Opening: {System.IO.Path.GetFileName(filePath)}…";

            // Fire-and-forget: returns to caller immediately, unblocking the UI thread.
            _ = OpenFileCoreAsync(filePath);
        }

        /// <summary>
        /// Async core for OpenFile.
        /// ByteProvider/FileStream creation runs on a background thread (may block for seconds
        /// on large assemblies due to OS I/O or antivirus scans).
        /// All UI-thread work executes after the await continuation on the WPF dispatcher.
        /// </summary>
        private async Task OpenFileCoreAsync(string filePath)
        {
            try
            {
                // CRITICAL: Close previous file properly to reset all state.
                // This is safe here because we are on the UI thread (before the first await).
                if (_viewModel != null)
                    Close();

                // ByteProvider.OpenFile → new FileStream() on a background thread.
                var vm = await Task.Run(() => HexEditorViewModel.OpenFile(filePath));

                // ── Continuation runs on the UI/dispatcher thread ─────────────────────────
                _viewModel = vm;
                HexViewport.LinesSource = _viewModel.Lines;

                // Synchronize ViewModel with control's BytePerLine (which may have been set in XAML before file opened)
                _viewModel.BytePerLine = BytePerLine;
                HexViewport.BytesPerLine = BytePerLine;

                // Synchronize ViewModel with control's EditMode (which may have been set before file opened, e.g., from settings)
                _viewModel.EditMode = EditMode;
                HexViewport.EditMode = EditMode; // Also sync to HexViewport for caret display

                // CRITICAL FIX: Synchronize ByteSize and ByteOrder from DependencyProperties
                // This ensures multi-byte mode settings are preserved across file open/close
                _viewModel.ByteSize = ByteSize;
                _viewModel.ByteOrder = ByteOrder;

                // Synchronize ByteShiftLeft (V1 Legacy feature)
                _viewModel.ByteShiftLeft = ByteShiftLeft;

                // ByteProvider V2 always supports insertion anywhere - no need to set flag
                if (EditMode == EditMode.Insert)
                {
                }

                // Initialize byte spacer properties on viewport (V1 compatibility)
                HexViewport.ByteSpacerPositioning = ByteSpacerPositioning;
                HexViewport.ByteSpacerWidthTickness = ByteSpacerWidthTickness;
                HexViewport.ByteGrouping = ByteGrouping;
                HexViewport.ByteSpacerVisualStyle = ByteSpacerVisualStyle;

                // Initialize byte foreground colors
                var normalBrush = Resources["ByteForegroundBrush"] as Brush;
                var alternateBrush = Resources["AlternateByteForegroundBrush"] as Brush;
                HexViewport.SetByteForegroundColors(normalBrush, alternateBrush);

                // Store file info
                FileName = filePath;
                IsModified = false;
                IsFileOrStreamLoaded = true;  // FIX: Update read-only DP for settings panel

                // Subscribe to property changes
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;

                // Calculate initial visible lines AFTER the control is fully loaded AND layout is complete
                // Use ApplicationIdle priority to ensure BaseGrid.RowDefinitions[1].ActualHeight is set
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateVisibleLines();
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                // Update scrollbar with initial values
                VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
                VerticalScroll.ViewportSize = _viewModel.VisibleLines;

                // Raise FileOpened event
                OnFileOpened(EventArgs.Empty);

                // Start watching the file for external modifications.
                StartFileWatcher(filePath);

                // Update status bar
                StatusText.Text = $"Loaded: {System.IO.Path.GetFileName(filePath)}";
                UpdateFileSizeDisplay();
                BytesPerLineText.Text = $"Bytes/Line: {_viewModel.BytePerLine}";
                EditModeText.Text = $"Mode: {_viewModel.EditMode}";
                RaiseHexStatusChanged();

                // Auto-detect format: snapshot bytes on UI thread, then detect on Task.Run.
                if (EnableAutoFormatDetection && Stream != null && Stream.Length > 0)
                {
                    // Snapshot bytes on UI thread — stream access is not thread-safe across threads.
                    var bytesToRead = (int)Math.Min(Stream.Length, 1024 * 1024);
                    var sample = new byte[bytesToRead];
                    var savedPos = Stream.Position;
                    Stream.Position = 0;
                    var bytesRead = Stream.Read(sample, 0, bytesToRead);
                    Stream.Position = savedPos;
                    if (bytesRead < bytesToRead) Array.Resize(ref sample, bytesRead);

                    // CPU-intensive detection on a thread-pool thread so the UI thread stays responsive.
                    var capturedPath = filePath;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var result = _formatDetectionService.DetectFormat(sample, capturedPath, null);

                            await Dispatcher.InvokeAsync(() =>
                            {
                                if (result.Success && result.Blocks?.Count > 0)
                                {
                                    ClearCustomBackgroundBlock();
                                    foreach (var b in result.Blocks)
                                        AddCustomBackgroundBlock(b);

                                    var fileLen = Length;
                                    if (fileLen > 0)
                                        foreach (var b in _customBackgroundService.GetAllBlocks())
                                            if (b.Length >= fileLen * 0.8) b.ShowInTooltip = false;

                                    _detectedFormat      = result.Format;
                                    _detectionVariables  = result.Variables;
                                    _detectionCandidates = result.Candidates;

                                    RefreshParsedFields();
                                    UpdateEnrichedFormatPanel(result.Format);
                                    ResetBreadcrumbCache();
                                    UpdateBreadcrumb();

                                    FormatDetected?.Invoke(this, new FormatDetectedEventArgs
                                    {
                                        Success         = true,
                                        Format          = result.Format,
                                        Blocks          = result.Blocks,
                                        DetectionTimeMs = result.DetectionTimeMs,
                                    });
                                }

                                if (ShowFormatDetectionStatus)
                                    StatusText.Text = result.Success
                                        ? $"Format detected: {result.Format?.FormatName ?? "Unknown"}"
                                        : string.Empty;
                                RaiseHexStatusChanged();

                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[FormatDetection] Background error: {ex.Message}");
                        }
                    });
                }

                // Notify external byte distribution panel (e.g. BarChartPanel) in background
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NotifyByteDistributionPanel();
                }), System.Windows.Threading.DispatcherPriority.Background);

                // Update scroll markers in background
                // Scroll markers don't need to be ready immediately
                if (_scrollMarkers != null)
                {
                    // Use VirtualLength for correct marker positioning (includes insertions)
                    _scrollMarkers.FileLength = _viewModel.VirtualLength;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateScrollMarkers();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileOperations] Error opening file '{filePath}': {ex.Message}");
                StatusText.Text = $"Error opening file: {ex.Message}";
            }
            finally
            {
                // CRITICAL: Always reset flag to allow future opens
                _isOpeningFile = false;
            }
        }

        /// <summary>
        /// Save current file
        /// </summary>
        public void Save()
        {
            if (_viewModel == null)
                throw new InvalidOperationException("No file loaded");

            _viewModel.Save();
            StatusText.Text = "File saved";
            OnChangesSubmited(EventArgs.Empty);
        }

        /// <summary>
        /// Clear all modification-related state (markers for modified, inserted, deleted bytes)
        /// </summary>
        private void ClearModificationState()
        {
            try
            {
                if (_scrollMarkers != null)
                {
                    _scrollMarkers.ClearMarkers(ScrollMarkerType.Modified);
                    _scrollMarkers.ClearMarkers(ScrollMarkerType.Inserted);
                    _scrollMarkers.ClearMarkers(ScrollMarkerType.Deleted);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearModificationState error: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear selection state in viewport and markers
        /// </summary>
        private void ClearSelectionState()
        {
            try
            {
                if (HexViewport != null)
                {
                    HexViewport.SelectionStart = -1;
                    HexViewport.SelectionStop = -1;
                    HexViewport.CursorPosition = 0;
                }

                _scrollMarkers?.ClearSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearSelectionState error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset status bar to initial state
        /// </summary>
        private void ResetStatusBar()
        {
            try
            {
                if (StatusText != null) StatusText.Text = "Ready";
                if (FileSizeText != null) FileSizeText.Text = "Size: -";
                if (SelectionInfo != null) SelectionInfo.Text = "No selection";
                if (PositionInfo != null) PositionInfo.Text = "Position: 0";
                if (EditModeText != null) EditModeText.Text = "Mode: Overwrite";
                if (BytesPerLineText != null) BytesPerLineText.Text = "Bytes/Line: 16";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResetStatusBar error: {ex.Message}");
            }
        }

        /// <summary>
        /// Close current file and reset control to initial state
        /// Clears all state: ViewModel, search results, modifications, selection, UI
        /// </summary>
        public void Close()
        {
            // 0. CRITICAL: Set closing flag FIRST to prevent async operations from accessing resources
            _isClosing = true;

            try
            {
                // 0.5. Stop external file watcher before releasing the file stream.
                StopFileWatcher();

                // 1. CRITICAL: Cancel any ongoing async operations before closing (prevents crashes on shutdown)
                if (_longRunningService != null)
                {
                    _longRunningService.CancelCurrentOperation();

                    // CRITICAL: Give async operation time to see cancellation and stop gracefully
                    // This prevents race condition where operation tries to read bytes after file is closed
                    // 1 second should be enough for even large chunks (256KB) to complete reading
                    System.Threading.Thread.Sleep(1000);
                }

                // 2. Dispose ViewModel and Provider
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                    _viewModel.Close();
                    _viewModel = null;
                }

                // 3. Clear viewport data source
            if (HexViewport != null)
            {
                HexViewport.LinesSource = null;
                HexViewport.InvalidateCustomBackgroundCache();
                HexViewport.Refresh();
            }

            // 3. Clear search state (highlights, search markers, scrollbar opacity)
            ClearSearchState();

            // 4. Clear modification state (modification markers)
            ClearModificationState();

            // 5. Clear selection state (selection, cursor position)
            ClearSelectionState();

            // 5.5. Clear custom background blocks (visual markers from format detection)
            ClearCustomBackgroundBlock();

            // 5.6. Clear format detection state (parsed fields, detected format)
            ClearFormatDetectionState();

            // 6. Reset file info
            FileName = string.Empty;
            IsModified = false;
            IsFileOrStreamLoaded = false;  // FIX: Update read-only DP for settings panel

            // 7. Clear byte distribution panel
            ClearByteDistributionPanel();

                // 8. Reset status bar to initial state
                ResetStatusBar();

                // 9. Raise FileClosed event
                OnFileClosed(EventArgs.Empty);
            }
            finally
            {
                // CRITICAL: Always reset closing flag, even if Close() throws
                _isClosing = false;
            }
        }


        /// <summary>
        /// Update auto-highlight byte value when selection changes
        /// </summary>
        private void UpdateAutoHighlightByte()
        {
            if (HexViewport == null || _viewModel == null)
                return;

            // Only update if auto-highlight is enabled
            if (!AllowAutoHighLightSelectionByte)
            {
                HexViewport.AutoHighlightByteValue = null;
                HexViewport.InvalidateVisual();
                return;
            }

            // Get byte value at current selection position or first byte
            try
            {
                VirtualPosition positionToUse;

                // Use selection start if valid, otherwise use position 0
                if (_viewModel.SelectionStart.IsValid && _viewModel.SelectionStart.Value < _viewModel.VirtualLength)
                {
                    positionToUse = _viewModel.SelectionStart;
                }
                else if (_viewModel.VirtualLength > 0)
                {
                    positionToUse = new VirtualPosition(0);
                }
                else
                {
                    HexViewport.AutoHighlightByteValue = null;
                    HexViewport.InvalidateVisual();
                    return;
                }

                byte byteValue = _viewModel.GetByteAt(positionToUse);
                HexViewport.AutoHighlightByteValue = byteValue;
                HexViewport.InvalidateVisual();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Auto-highlight error: {ex.Message}";
                HexViewport.AutoHighlightByteValue = null;
                HexViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Select all bytes with specified value
        /// V1 ALGORITHM: Expands selection bidirectionally from clicked position
        /// </summary>
        private void SelectAllBytesWith(byte byteValue)
        {

            if (_viewModel == null)
            {
                StatusText.Text = "DEBUG: ViewModel is null";
                return;
            }

            try
            {
                // V1 ALGORITHM: Expand selection bidirectionally from current position
                // Scan backwards and forwards until we hit a different byte value

                long startPosition = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : 0;
                long stopPosition = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value : startPosition;


                // Scan BACKWARDS from startPosition while byte matches
                long scanStart = startPosition;
                while (scanStart > 0)
                {
                    var prevPos = new VirtualPosition(scanStart - 1);
                    if (!prevPos.IsValid || (scanStart - 1) < 0)
                        break;

                    byte prevByte = _viewModel.GetByteAt(prevPos);
                    if (prevByte != byteValue)
                        break;

                    scanStart--;
                }


                // Scan FORWARDS from stopPosition while byte matches
                long scanStop = stopPosition;
                while (scanStop < _viewModel.VirtualLength - 1)
                {
                    var nextPos = new VirtualPosition(scanStop + 1);
                    if (!nextPos.IsValid || (scanStop + 1) >= _viewModel.VirtualLength)
                        break;

                    byte nextByte = _viewModel.GetByteAt(nextPos);
                    if (nextByte != byteValue)
                        break;

                    scanStop++;
                }


                // Set the expanded selection in ViewModel
                _viewModel.SetSelectionRange(new VirtualPosition(scanStart), new VirtualPosition(scanStop));

                // Notify plugins (DataInspector, etc.) — DP callbacks are not triggered here.
                OnSelectionStartChanged(EventArgs.Empty);
                OnSelectionStopChanged(EventArgs.Empty);

                // CRITICAL: Synchronize viewport properties IMMEDIATELY (don't wait for PropertyChanged)
                HexViewport.SelectionStart = scanStart;
                HexViewport.SelectionStop = scanStop;
                HexViewport.CursorPosition = scanStop;

                // Verify viewport properties were set correctly

                // Force viewport refresh to show the selection
                HexViewport.InvalidateVisual();

                // Ensure the selection start is visible
                EnsurePositionVisible(new VirtualPosition(scanStart));

                // Verify ViewModel selection was set

                // Update status bar
                StatusText.Text = $"Selected {scanStop - scanStart + 1} consecutive bytes with value 0x{byteValue:X2}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Auto-select failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Update scroll markers with bookmarks, modifications, and search results
        /// </summary>
        private void UpdateScrollMarkers()
        {
            if (_scrollMarkers == null || _viewModel == null || _viewModel.Provider == null)
                return;

            try
            {
                // Update bookmarks
                _scrollMarkers.BookmarkPositions = new HashSet<long>(_bookmarks);

                // Get modifications by type from Provider
                var modifiedDict = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Modified);
                var insertedDict = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Added);
                var deletedDict = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Deleted);

                // Update scroll markers with separate positions by type
                _scrollMarkers.ModifiedPositions = modifiedDict != null ? new HashSet<long>(modifiedDict.Keys) : new HashSet<long>();
                _scrollMarkers.InsertedPositions = insertedDict != null ? new HashSet<long>(insertedDict.Keys) : new HashSet<long>();
                _scrollMarkers.DeletedPositions = deletedDict != null ? new HashSet<long>(deletedDict.Keys) : new HashSet<long>();

                // Search results would be updated separately when FindAll is called
                // (we'll add that later)
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Scroll markers update failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Update scroll markers selection bar to show current selection
        /// </summary>
        private void UpdateScrollMarkersSelection()
        {
            if (_scrollMarkers == null || _viewModel == null)
                return;

            try
            {
                long start = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : -1;
                long stop = _viewModel.SelectionStop.IsValid ? _viewModel.SelectionStop.Value : -1;

                if (start >= 0 && stop >= 0)
                {
                    _scrollMarkers.SetSelection(start, stop);
                }
                else
                {
                    _scrollMarkers.ClearSelection();
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Clear all search-related state (highlights, markers, scrollbar opacity)
        /// MUST be called before opening a new file to prevent crashes from stale state
        /// </summary>
        private void ClearSearchState()
        {
            try
            {
                // Clear highlighted search positions in viewport
                if (HexViewport != null)
                {
                    HexViewport.HighlightedPositions = null;
                    HexViewport.InvalidateVisual();
                }

                // Clear search result markers in scroll bar
                if (_scrollMarkers != null)
                {
                    _scrollMarkers.ClearMarkers(ScrollMarkerType.SearchResult);
                    _scrollMarkers.ClearSelection();
                }

                // Restore scrollbar normal opacity (was set to 0.3 when markers were visible)
                if (VerticalScroll != null)
                {
                    VerticalScroll.Opacity = 1.0;
                }

                // Update status bar to remove "Press ESC to clear" message
                if (StatusText != null && StatusText.Text.Contains("Press ESC to clear"))
                {
                    StatusText.Text = "Ready";
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash - this is defensive cleanup
                System.Diagnostics.Debug.WriteLine($"ClearSearchState error: {ex.Message}");
            }
        }

        #endregion
    }
}
