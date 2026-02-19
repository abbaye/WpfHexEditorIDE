//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WpfHexaEditor.Models;
using WpfHexaEditor.Services;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor
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
                Properties.Resources.ProgressTitleOpeningFile,
                true, // Can cancel
                async (progress, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        progress.Report(new OperationProgress
                        {
                            Percentage = 10,
                            Message = Properties.Resources.ProgressMessageOpeningFileStream
                        });
                        cancellationToken.ThrowIfCancellationRequested();

                        // Open file via ViewModel
                        viewModel = HexEditorViewModel.OpenFile(filePath);

                        progress.Report(new OperationProgress
                        {
                            Percentage = 50,
                            Message = Properties.Resources.ProgressMessageLoadingFileData
                        });
                        cancellationToken.ThrowIfCancellationRequested();

                        progress.Report(new OperationProgress
                        {
                            Percentage = 100,
                            Message = Properties.Resources.ProgressMessageFileOpenedSuccessfully
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
                Properties.Resources.ProgressTitleSavingFile,
                false, // Cannot cancel (data integrity)
                async (progress, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        progress.Report(new OperationProgress
                        {
                            Percentage = 10,
                            Message = Properties.Resources.ProgressMessagePreparingToSave
                        });

                        // Save via ViewModel
                        _viewModel.Save();

                        progress.Report(new OperationProgress
                        {
                            Percentage = 100,
                            Message = Properties.Resources.ProgressMessageFileSavedSuccessfully
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

            await _longRunningService.ExecuteOperationAsync(
                Properties.Resources.ProgressTitleSearching,
                true, // Can cancel
                async (progress, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        progress.Report(new OperationProgress
                        {
                            Percentage = 0,
                            Message = Properties.Resources.ProgressMessageStartingSearch
                        });
                        cancellationToken.ThrowIfCancellationRequested();

                        // Manual search with progress reporting
                        long fileLength = _viewModel.VirtualLength;
                        long currentPos = startPosition;
                        int lastProgressPercent = 0;

                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Find next occurrence
                            long foundPos = _viewModel.Provider.FindFirst(pattern, currentPos);

                            if (foundPos == -1)
                                break; // No more matches

                            results.Add(foundPos);
                            currentPos = foundPos + 1;

                            // Report progress based on search position
                            int progressPercent = fileLength > 0 ? (int)((currentPos * 100) / fileLength) : 100;
                            if (progressPercent != lastProgressPercent)
                            {
                                progress.Report(new OperationProgress
                                {
                                    Percentage = progressPercent,
                                    Message = string.Format(Properties.Resources.ProgressMessageSearchingFoundFormat, results.Count)
                                });
                                lastProgressPercent = progressPercent;
                            }
                        }

                        progress.Report(new OperationProgress
                        {
                            Percentage = 100,
                            Message = string.Format(Properties.Resources.ProgressMessageFoundMatchesFormat, results.Count)
                        });

                        return true;
                    }, cancellationToken);
                });

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
                Properties.Resources.ProgressTitleReplacing,
                false, // Cannot cancel (data integrity)
                async (progress, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        progress.Report(new OperationProgress
                        {
                            Percentage = 10,
                            Message = Properties.Resources.ProgressMessageFindingMatches
                        });

                        // Phase 1: Find all occurrences
                        var matches = _viewModel.Provider.FindAll(findPattern, 0).ToList();

                        progress.Report(new OperationProgress
                        {
                            Percentage = 50,
                            Message = string.Format(Properties.Resources.ProgressMessageReplacingFormat, matches.Count)
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
                            Message = string.Format(Properties.Resources.ProgressMessageReplacedFormat, replacementCount)
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

            // Defer expensive operations to background
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateBarChart();
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
