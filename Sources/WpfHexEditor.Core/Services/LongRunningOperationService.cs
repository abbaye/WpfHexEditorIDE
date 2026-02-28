//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.Events;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service for managing long-running operations with progress reporting and cancellation support
    /// </summary>
    public class LongRunningOperationService : IDisposable
    {
        private CancellationTokenSource _currentCts;
        private bool _isOperationActive;
        private bool _disposed;
        private DateTime _lastProgressUpdate;
        private Task<bool> _currentTask; // Track current operation for graceful shutdown

        /// <summary>
        /// Minimum interval in milliseconds between progress updates (default: 33ms = ~30 updates/second)
        /// Lower values = smoother animation but more UI updates
        /// Higher values = less CPU usage but choppier animation
        /// </summary>
        public int MinProgressIntervalMs { get; set; } = 33;

        #region Events

        /// <summary>
        /// Raised when a long-running operation starts
        /// </summary>
        public event EventHandler<OperationProgressEventArgs> OperationStarted;

        /// <summary>
        /// Raised when progress is updated during an operation
        /// </summary>
        public event EventHandler<OperationProgressEventArgs> OperationProgress;

        /// <summary>
        /// Raised when an operation completes (success, failure, or cancelled)
        /// </summary>
        public event EventHandler<OperationCompletedEventArgs> OperationCompleted;

        #endregion

        #region Public Methods

        /// <summary>
        /// Execute a long-running operation with progress reporting
        /// </summary>
        /// <param name="operationTitle">Title displayed to user</param>
        /// <param name="canCancel">Whether user can cancel this operation</param>
        /// <param name="operation">The async operation to execute</param>
        /// <returns>True if operation completed successfully</returns>
        public async Task<bool> ExecuteOperationAsync(
            string operationTitle,
            bool canCancel,
            Func<IProgress<OperationProgress>, CancellationToken, Task<bool>> operation)
        {
            if (_isOperationActive)
                throw new InvalidOperationException("Another operation is already in progress");

            _isOperationActive = true;
            _currentCts = new CancellationTokenSource();
            _lastProgressUpdate = DateTime.MinValue;

            try
            {
                // Raise operation started event
                OnOperationStarted(new OperationProgressEventArgs
                {
                    OperationTitle = operationTitle,
                    StatusMessage = "Initializing...",
                    ProgressPercentage = 0,
                    IsIndeterminate = false,
                    CanCancel = canCancel
                });

                // Create progress reporter
                var progress = new Progress<OperationProgress>(p =>
                {
                    // Throttle progress updates
                    var now = DateTime.Now;
                    if ((now - _lastProgressUpdate).TotalMilliseconds >= MinProgressIntervalMs)
                    {
                        _lastProgressUpdate = now;
                        OnOperationProgress(new OperationProgressEventArgs
                        {
                            OperationTitle = operationTitle,
                            StatusMessage = p.Message,
                            ProgressPercentage = p.Percentage,
                            IsIndeterminate = false,
                            CanCancel = canCancel
                        });
                    }
                });

                // Execute the operation
                bool success = await operation(progress, _currentCts.Token);

                // Check if operation was cancelled (token was signaled but no exception thrown)
                bool wasCancelled = _currentCts.Token.IsCancellationRequested && !success;

                // Raise completion event
                OnOperationCompleted(new OperationCompletedEventArgs
                {
                    Success = success,
                    WasCancelled = wasCancelled
                });

                return success;
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled by user
                OnOperationCompleted(new OperationCompletedEventArgs
                {
                    Success = false,
                    WasCancelled = true,
                    ErrorMessage = "Operation cancelled by user"
                });
                return false;
            }
            catch (Exception ex)
            {
                // Operation failed with error
                OnOperationCompleted(new OperationCompletedEventArgs
                {
                    Success = false,
                    WasCancelled = false,
                    ErrorMessage = ex.Message
                });
                return false;
            }
            finally
            {
                _currentCts?.Dispose();
                _currentCts = null;
                _isOperationActive = false;
                _currentTask = null; // Clear task reference
            }
        }

        /// <summary>
        /// Open a file asynchronously with progress reporting
        /// </summary>
        public async Task<ByteProvider> OpenFileAsync(string filePath)
        {
            ByteProvider provider = null;

            await ExecuteOperationAsync(
                "Opening file",
                true, // Can cancel file open
                async (progress, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        progress.Report(new OperationProgress { Percentage = 10, Message = "Opening file stream..." });
                        cancellationToken.ThrowIfCancellationRequested();

                        // Open the file
                        provider = new ByteProvider();
                        provider.OpenFile(filePath);

                        progress.Report(new OperationProgress { Percentage = 50, Message = "Loading file data..." });
                        cancellationToken.ThrowIfCancellationRequested();

                        progress.Report(new OperationProgress { Percentage = 100, Message = "File opened successfully" });
                        return true;
                    }, cancellationToken);
                });

            return provider;
        }

        /// <summary>
        /// Save file changes asynchronously with progress reporting
        /// </summary>
        public async Task<bool> SaveFileAsync(ByteProvider provider, string filePath = null)
        {
            return await ExecuteOperationAsync(
                "Saving file",
                false, // Cannot cancel save (data integrity)
                async (progress, cancellationToken) =>
                {
                    return await Task.Run(() =>
                    {
                        progress.Report(new OperationProgress { Percentage = 10, Message = "Preparing to save..." });

                        // Submit changes to file
                        provider.SubmitChanges(filePath);

                        progress.Report(new OperationProgress { Percentage = 100, Message = "File saved successfully" });
                        return true;
                    }, cancellationToken);
                });
        }

        /// <summary>
        /// Find all occurrences asynchronously with progress reporting
        /// </summary>
        public async Task<List<long>> FindAllAsync(
            FindReplaceService findService,
            ByteProvider provider,
            byte[] searchPattern,
            long startPosition = 0)
        {
            List<long> results = new List<long>();

            await ExecuteOperationAsync(
                "Searching",
                true, // Can cancel search
                async (progress, cancellationToken) =>
                {
                    // Use existing FindReplaceService async method with progress
                    var searchProgress = new Progress<int>(percent =>
                    {
                        progress.Report(new OperationProgress
                        {
                            Percentage = percent,
                            Message = $"Searching... {percent}%"
                        });
                    });

                    results = await findService.FindAllAsync(
                        provider,
                        searchPattern,
                        startPosition,
                        searchProgress,
                        cancellationToken);

                    return true;
                });

            return results;
        }

        /// <summary>
        /// Replace all occurrences asynchronously with progress reporting
        /// </summary>
        public async Task<int> ReplaceAllAsync(
            FindReplaceService findService,
            ByteProvider provider,
            byte[] findPattern,
            byte[] replacePattern,
            bool truncateLength,
            bool readOnlyMode)
        {
            int replacementCount = 0;

            await ExecuteOperationAsync(
                "Replacing",
                false, // Cannot cancel replace (data integrity)
                async (progress, cancellationToken) =>
                {
                    // Use existing FindReplaceService async method with 2-phase progress
                    var replaceProgress = new Progress<int>(percent =>
                    {
                        string phase = percent <= 50 ? "Finding matches" : "Replacing";
                        progress.Report(new OperationProgress
                        {
                            Percentage = percent,
                            Message = $"{phase}... {percent}%"
                        });
                    });

                    replacementCount = await findService.ReplaceAllAsync(
                        provider,
                        findPattern,
                        replacePattern,
                        truncateLength,
                        readOnlyMode,
                        replaceProgress,
                        cancellationToken);

                    return true;
                });

            return replacementCount;
        }

        /// <summary>
        /// Cancel the currently running operation
        /// </summary>
        public void CancelCurrentOperation()
        {
            if (_isOperationActive && _currentCts != null && !_currentCts.IsCancellationRequested)
            {
                _currentCts.Cancel();
            }
        }

        /// <summary>
        /// Cancel current operation and wait for it to complete (with timeout)
        /// CRITICAL: Use this before disposing resources to prevent crashes
        /// </summary>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds (default: 2000ms)</param>
        /// <returns>True if operation completed within timeout</returns>
        public async Task<bool> CancelAndWaitAsync(int timeoutMs = 2000)
        {
            if (!_isOperationActive || _currentTask == null)
                return true; // No operation running

            // Cancel the operation
            CancelCurrentOperation();

            // Wait for completion with timeout
            try
            {
                var timeoutTask = Task.Delay(timeoutMs);
                var completedTask = await Task.WhenAny(_currentTask, timeoutTask);

                if (completedTask == _currentTask)
                {
                    // Operation completed within timeout
                    return true;
                }
                else
                {
                    // Timeout - operation still running
                    System.Diagnostics.Debug.WriteLine($"WARNING: Operation did not complete within {timeoutMs}ms timeout");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CancelAndWaitAsync error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Event Raising

        protected virtual void OnOperationStarted(OperationProgressEventArgs e)
        {
            OperationStarted?.Invoke(this, e);
        }

        protected virtual void OnOperationProgress(OperationProgressEventArgs e)
        {
            OperationProgress?.Invoke(this, e);
        }

        protected virtual void OnOperationCompleted(OperationCompletedEventArgs e)
        {
            OperationCompleted?.Invoke(this, e);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _currentCts?.Cancel();
                _currentCts?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Progress reporting structure for long-running operations
    /// </summary>
    public struct OperationProgress
    {
        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int Percentage { get; set; }

        /// <summary>
        /// Status message describing current operation
        /// </summary>
        public string Message { get; set; }
    }
}
