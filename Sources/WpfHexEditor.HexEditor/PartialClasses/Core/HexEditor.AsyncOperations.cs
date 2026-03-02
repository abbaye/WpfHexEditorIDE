//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WpfHexEditor.Core.Models;
using WpfHexEditor.Core.Services;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Async Operations
    /// Contains public async methods for long-running operations with progress feedback
    /// </summary>
    public partial class HexEditor
    {
        #region Public Async Methods

        /// <summary>
        /// Open a file asynchronously with progress reporting
        /// Automatically closes current file if one is already open
        /// </summary>
        /// <param name="filePath">Path to the file to open</param>
        /// <returns>True if file was opened successfully</returns>
        public async Task<bool> OpenFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            // CRITICAL: Set flag to prevent infinite recursion in OnFileNamePropertyChanged
            _isOpeningFile = true;

            try
            {
                // CRITICAL: Close previous file on UI thread before async operation
                // This prevents crashes and memory leaks from stale state
                if (_viewModel != null)
                {
                    Dispatcher.Invoke(() => Close());
                }

                HexEditorViewModel viewModel = null;

                bool success = await _longRunningService.ExecuteOperationAsync(
                    WpfHexEditor.Core.Properties.Resources.ProgressTitleOpeningFile,
                    true, // Can cancel
                    async (progress, cancellationToken) =>
                    {
                        return await Task.Run(() =>
                        {
                            progress.Report(new OperationProgress
                            {
                                Percentage = 10,
                                Message = WpfHexEditor.Core.Properties.Resources.ProgressMessageOpeningFileStream
                            });

                            // Check for cancellation - return false instead of throwing
                            if (cancellationToken.IsCancellationRequested)
                                return false;

                            // Open file via ViewModel
                            viewModel = HexEditorViewModel.OpenFile(filePath);

                            progress.Report(new OperationProgress
                            {
                                Percentage = 50,
                                Message = WpfHexEditor.Core.Properties.Resources.ProgressMessageLoadingFileData
                            });

                            // Check for cancellation - return false instead of throwing
                            if (cancellationToken.IsCancellationRequested)
                                return false;

                            progress.Report(new OperationProgress
                            {
                                Percentage = 100,
                                Message = WpfHexEditor.Core.Properties.Resources.ProgressMessageFileOpenedSuccessfully
                            });

                            return true;
                        }, cancellationToken);
                    });

                if (success && viewModel != null)
                {
                    // Complete the file opening on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        CompleteFileOpen(viewModel, filePath);
                    });
                }

                return success;
            }
            finally
            {
                // CRITICAL: Always reset flag to allow future opens
                _isOpeningFile = false;
            }
        }

        /// <summary>
        /// Save current file asynchronously with progress reporting
        /// </summary>
        /// <returns>True if file was saved successfully</returns>
        public async Task<bool> SaveAsync()
        {
            if (_viewModel == null)
                throw new InvalidOperationException("No file loaded");

            bool success = await _longRunningService.ExecuteOperationAsync(
                WpfHexEditor.Core.Properties.Resources.ProgressTitleSavingFile,
                false, // Cannot cancel (data integrity)
                async (progress, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        progress.Report(new OperationProgress
                        {
                            Percentage = 10,
                            Message = WpfHexEditor.Core.Properties.Resources.ProgressMessagePreparingToSave
                        });

                        // Save via ViewModel
                        _viewModel.Save();

                        progress.Report(new OperationProgress
                        {
                            Percentage = 100,
                            Message = WpfHexEditor.Core.Properties.Resources.ProgressMessageFileSavedSuccessfully
                        });

                        return true;
                    }, cancellationToken);
                });

            if (success)
            {
                // Update UI on completion
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "File saved";
                    OnChangesSubmited(EventArgs.Empty);
                });
            }

            return success;
        }

        /// <summary>
        /// Find all occurrences of a byte pattern asynchronously with progress reporting
        /// </summary>
        /// <param name="pattern">Byte pattern to search for</param>
        /// <param name="startPosition">Starting position for search (default: 0)</param>
        /// <returns>List of positions where pattern was found</returns>
        public async Task<List<long>> FindAllAsync(byte[] pattern, long startPosition = 0)
        {
            if (_viewModel == null)
                throw new InvalidOperationException("No file loaded");

            if (pattern == null || pattern.Length == 0)
                throw new ArgumentException("Search pattern cannot be empty", nameof(pattern));

            List<long> results = new List<long>();

            bool success = await _longRunningService.ExecuteOperationAsync(
                WpfHexEditor.Core.Properties.Resources.ProgressTitleSearching,
                true, // Can cancel
                async (progress, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        progress.Report(new OperationProgress
                        {
                            Percentage = 0,
                            Message = WpfHexEditor.Core.Properties.Resources.ProgressMessageStartingSearch
                        });

                        if (cancellationToken.IsCancellationRequested)
                            return false;

                        // Chunk-based search with continuous progress updates
                        // Uses GetBytes(virtualPosition, count) which respects modifications
                        var provider = _viewModel.GetByteProvider();
                        if (provider == null)
                            return false;

                        long fileLength = provider.VirtualLength;
                        int lastProgressPercent = 0;

                        const int CHUNK_SIZE = 256 * 1024; // 256KB chunks for good balance
                        long currentPos = startPosition;

                        while (currentPos < fileLength)
                        {
                            // Check cancellation between chunks (responsive!)
                            if (cancellationToken.IsCancellationRequested)
                                return false;

                            // CRITICAL: Check if control is being closed (prevents crash on shutdown)
                            if (_isClosing)
                                return false;

                            // Calculate chunk size
                            long remainingBytes = fileLength - currentPos;
                            int chunkSize = (int)Math.Min(CHUNK_SIZE, remainingBytes);
                            int bufferSize = (int)Math.Min(chunkSize + pattern.Length - 1, remainingBytes);

                            // Read chunk using GetBytes (respects virtual positions)
                            byte[] buffer;
                            try
                            {
                                buffer = provider.GetBytes(currentPos, bufferSize);
                            }
                            catch (Exception ex)
                            {
                                // CRITICAL: Provider might be closed/disposed during shutdown
                                System.Diagnostics.Debug.WriteLine($"GetBytes failed (likely closing): {ex.Message}");
                                return false; // Stop gracefully
                            }

                            if (buffer == null || buffer.Length == 0)
                                break;

                            // Search pattern in chunk
                            for (int i = 0; i <= buffer.Length - pattern.Length; i++)
                            {
                                bool match = true;
                                for (int j = 0; j < pattern.Length; j++)
                                {
                                    if (buffer[i + j] != pattern[j])
                                    {
                                        match = false;
                                        break;
                                    }
                                }

                                if (match)
                                    results.Add(currentPos + i);
                            }

                            // Advance position (with overlap for patterns spanning boundaries)
                            currentPos += Math.Max(1, chunkSize - pattern.Length + 1);

                            // Update progress (updates every chunk = responsive!)
                            int progressPercent = (int)((currentPos * 100) / fileLength);
                            if (progressPercent != lastProgressPercent)
                            {
                                progress.Report(new OperationProgress
                                {
                                    Percentage = Math.Min(progressPercent, 100),
                                    Message = string.Format(WpfHexEditor.Core.Properties.Resources.ProgressMessageSearchingFoundFormat, results.Count)
                                });
                                lastProgressPercent = progressPercent;
                            }
                        }

                        progress.Report(new OperationProgress
                        {
                            Percentage = 100,
                            Message = string.Format(WpfHexEditor.Core.Properties.Resources.ProgressMessageFoundMatchesFormat, results.Count)
                        });

                        return true;
                    }, cancellationToken);
                });

            // If operation was cancelled, clear search state (like pressing Escape)
            if (!success)
            {
                Dispatcher.Invoke(() =>
                {
                    ClearSearchState();
                });
                return new List<long>(); // Return empty list
            }

            return results;
        }

        /// <summary>
        /// Replace all occurrences of a byte pattern asynchronously with progress reporting
        /// </summary>
        /// <param name="findPattern">Byte pattern to find</param>
        /// <param name="replacePattern">Byte pattern to replace with</param>
        /// <param name="truncateLength">Whether to truncate to original length</param>
        /// <returns>Number of replacements made</returns>
        public async Task<int> ReplaceAllAsync(byte[] findPattern, byte[] replacePattern, bool truncateLength = false)
        {
            if (_viewModel == null)
                throw new InvalidOperationException("No file loaded");

            if (findPattern == null || findPattern.Length == 0)
                throw new ArgumentException("Find pattern cannot be empty", nameof(findPattern));

            if (replacePattern == null || replacePattern.Length == 0)
                throw new ArgumentException("Replace pattern cannot be empty", nameof(replacePattern));

            int replacementCount = 0;

            await _longRunningService.ExecuteOperationAsync(
                WpfHexEditor.Core.Properties.Resources.ProgressTitleReplacing,
                false, // Cannot cancel (data integrity)
                async (progress, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        progress.Report(new OperationProgress
                        {
                            Percentage = 10,
                            Message = WpfHexEditor.Core.Properties.Resources.ProgressMessageFindingMatches
                        });

                        // Phase 1: Find all occurrences
                        var matches = _viewModel.Provider.FindAll(findPattern, 0).ToList();

                        progress.Report(new OperationProgress
                        {
                            Percentage = 50,
                            Message = string.Format(WpfHexEditor.Core.Properties.Resources.ProgressMessageReplacingFormat, matches.Count)
                        });

                        if (matches.Count == 0)
                            return true;

                        // Phase 2: Replace each occurrence
                        // Note: Replace in reverse order to maintain position validity
                        var sortedMatches = matches.OrderByDescending(pos => pos).ToList();

                        foreach (var position in sortedMatches)
                        {
                            // Delete old pattern
                            for (int i = 0; i < findPattern.Length; i++)
                            {
                                _viewModel.Provider.DeleteByte(new VirtualPosition(position));
                            }

                            // Insert new pattern
                            for (int i = replacePattern.Length - 1; i >= 0; i--)
                            {
                                _viewModel.Provider.InsertByte(new VirtualPosition(position), replacePattern[i]);
                            }

                            replacementCount++;
                        }

                        progress.Report(new OperationProgress
                        {
                            Percentage = 100,
                            Message = string.Format(WpfHexEditor.Core.Properties.Resources.ProgressMessageReplacedFormat, replacementCount)
                        });

                        return true;
                    }, cancellationToken);
                });

            return replacementCount;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Complete file open operation on UI thread (separated from async OpenFile to avoid dispatcher issues)
        /// Note: Close() was already called before async operation, so state is clean
        /// </summary>
        private void CompleteFileOpen(HexEditorViewModel viewModel, string filePath)
        {
            _viewModel = viewModel;
            HexViewport.LinesSource = _viewModel.Lines;

            // Synchronize ViewModel with control's BytePerLine
            _viewModel.BytePerLine = BytePerLine;
            HexViewport.BytesPerLine = BytePerLine;

            // Synchronize ViewModel with control's EditMode
            _viewModel.EditMode = EditMode;
            HexViewport.EditMode = EditMode;

            // Synchronize ByteSize, ByteOrder and ByteShiftLeft from DependencyProperties
            _viewModel.ByteSize = ByteSize;
            _viewModel.ByteOrder = ByteOrder;
            _viewModel.ByteShiftLeft = ByteShiftLeft;

            // Initialize byte spacer properties on viewport
            HexViewport.ByteSpacerPositioning = ByteSpacerPositioning;
            HexViewport.ByteSpacerWidthTickness = ByteSpacerWidthTickness;
            HexViewport.ByteGrouping = ByteGrouping;
            HexViewport.ByteSpacerVisualStyle = ByteSpacerVisualStyle;

            // Initialize byte foreground colors
            var normalBrush = Resources["ByteForegroundBrush"] as System.Windows.Media.Brush;
            var alternateBrush = Resources["AlternateByteForegroundBrush"] as System.Windows.Media.Brush;
            HexViewport.SetByteForegroundColors(normalBrush, alternateBrush);

            // Store file info
            FileName = filePath;
            IsModified = false;
            IsFileOrStreamLoaded = true;  // FIX: Update read-only DP for settings panel

            // Subscribe to property changes
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Calculate initial visible lines
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateVisibleLines();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            // Update scrollbar
            VerticalScroll.Maximum = Math.Max(0, _viewModel.TotalLines - _viewModel.VisibleLines + 3);
            VerticalScroll.ViewportSize = _viewModel.VisibleLines;

            // Raise FileOpened event
            OnFileOpened(EventArgs.Empty);

            // Update status bar
            StatusText.Text = $"Loaded: {System.IO.Path.GetFileName(filePath)}";
            UpdateFileSizeDisplay();
            BytesPerLineText.Text = $"Bytes/Line: {_viewModel.BytePerLine}";
            EditModeText.Text = $"Mode: {_viewModel.EditMode}";
            RaiseHexStatusChanged();

            // Defer expensive operations to background
            Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyByteDistributionPanel();
            }), System.Windows.Threading.DispatcherPriority.Background);

            if (_scrollMarkers != null)
            {
                _scrollMarkers.FileLength = _viewModel.VirtualLength;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateScrollMarkers();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        #endregion
    }
}
