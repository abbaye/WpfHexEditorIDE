//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Diagnostics and Profiling
    /// Contains methods for debugging, profiling, and cache statistics
    /// </summary>
    public partial class HexEditor
    {
        #region Public Methods - Diagnostics

        /// <summary>
        /// Get cache statistics for debugging and performance profiling
        /// </summary>
        /// <returns>
        /// A formatted string containing cache performance metrics including:
        /// - Total cache hits and misses
        /// - Cache hit ratio percentage
        /// - Current cache size and capacity
        /// - Memory usage information
        /// - Line cache statistics
        /// </returns>
        /// <remarks>
        /// This method provides insight into the ByteProvider's internal caching
        /// system, which is crucial for understanding performance characteristics.
        ///
        /// Use cases:
        /// - Profiling application performance
        /// - Debugging cache-related issues
        /// - Optimizing BytePerLine settings
        /// - Understanding memory usage patterns
        /// - Tuning cache size for specific workloads
        ///
        /// Example output:
        /// <code>
        /// Cache Statistics:
        /// ================
        /// Line Cache:
        ///   Hits: 15234
        ///   Misses: 892
        ///   Hit Ratio: 94.5%
        ///   Size: 128/256 entries
        ///   Memory: 512 KB
        /// </code>
        ///
        /// Returns "No data loaded" if no file or stream is currently open.
        /// </remarks>
        public string GetCacheStatistics()
        {
            if (_viewModel?.Provider != null)
            {
                return _viewModel.Provider.GetCacheStatistics();
            }

            return "No data loaded - cache statistics unavailable";
        }

        /// <summary>
        /// Get diagnostic information about the current editor state
        /// </summary>
        /// <returns>
        /// A formatted string containing diagnostic information including:
        /// - File/stream status
        /// - Total size and position
        /// - ViewModel state
        /// - Viewport configuration
        /// - Modification counts
        /// </returns>
        /// <remarks>
        /// Provides comprehensive diagnostic information useful for debugging
        /// and understanding the current state of the HexEditor control.
        ///
        /// Example output:
        /// <code>
        /// HexEditor Diagnostics:
        /// =====================
        /// Data Source: test.bin (1.5 MB)
        /// Position: 0x00001234 / 0x0017ABCD
        /// Modified: Yes (142 changes)
        /// BytePerLine: 16
        /// EditMode: Overwrite
        /// Visible Lines: 25/6144
        /// Selection: 0x1000 to 0x10FF (256 bytes)
        /// </code>
        /// </remarks>
        public string GetDiagnostics()
        {
            var diag = new System.Text.StringBuilder();
            diag.AppendLine("HexEditor Diagnostics:");
            diag.AppendLine("=====================");

            // Data source info
            if (string.IsNullOrEmpty(FileName))
            {
                diag.AppendLine("Data Source: Memory or Stream");
            }
            else
            {
                var sizeKB = Length / 1024.0;
                diag.AppendLine($"Data Source: {System.IO.Path.GetFileName(FileName)} ({sizeKB:F2} KB)");
            }

            // Position and size
            if (_viewModel != null)
            {
                diag.AppendLine($"Total Size: {Length:N0} bytes (0x{Length:X})");
                diag.AppendLine($"Position: 0x{SelectionStart:X8}");
                diag.AppendLine($"Modified: {(IsModified ? "Yes" : "No")}");

                // ViewModel info
                diag.AppendLine($"BytePerLine: {_viewModel.BytePerLine}");
                diag.AppendLine($"EditMode: {_viewModel.EditMode}");
                diag.AppendLine($"Visible Lines: {_viewModel.VisibleLines}/{_viewModel.TotalLines}");

                // Selection info
                if (SelectionLength > 0)
                {
                    diag.AppendLine($"Selection: 0x{SelectionStart:X} to 0x{(SelectionStart + SelectionLength):X} ({SelectionLength} bytes)");
                }
                else
                {
                    diag.AppendLine("Selection: None");
                }

                // Modification counts
                if (_viewModel.Provider != null)
                {
                    var modifiedCount = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Modified).Count;
                    var deletedCount = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Deleted).Count;
                    var addedCount = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Added).Count;

                    if (modifiedCount + deletedCount + addedCount > 0)
                    {
                        diag.AppendLine($"Changes: {modifiedCount} modified, {addedCount} added, {deletedCount} deleted");
                    }
                }
            }
            else
            {
                diag.AppendLine("Status: No data loaded");
            }

            return diag.ToString();
        }

        /// <summary>
        /// Get memory usage statistics for the HexEditor control
        /// </summary>
        /// <returns>
        /// A formatted string containing memory usage information
        /// </returns>
        /// <remarks>
        /// Provides information about memory consumption, useful for:
        /// - Detecting memory leaks
        /// - Optimizing large file handling
        /// - Understanding memory pressure
        /// - Capacity planning
        ///
        /// Example output:
        /// <code>
        /// Memory Statistics:
        /// =================
        /// File Size: 10.5 MB
        /// Cache Memory: 2.1 MB
        /// Edits Memory: 128 KB
        /// Total Allocated: 12.7 MB
        /// </code>
        /// </remarks>
        public string GetMemoryStatistics()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine("Memory Statistics:");
            stats.AppendLine("=================");

            if (_viewModel?.Provider != null)
            {
                var fileSizeMB = Length / (1024.0 * 1024.0);
                stats.AppendLine($"File Size: {fileSizeMB:F2} MB");

                // Get modification counts for memory estimation
                var modifiedCount = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Modified).Count;
                var deletedCount = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Deleted).Count;
                var addedCount = _viewModel.Provider.GetByteModifieds(Core.ByteAction.Added).Count;

                var editsMemoryKB = (modifiedCount + deletedCount + addedCount) * 32 / 1024.0; // Rough estimate
                stats.AppendLine($"Edits Memory: {editsMemoryKB:F2} KB");
                stats.AppendLine($"Total Changes: {modifiedCount + deletedCount + addedCount:N0}");
            }
            else
            {
                stats.AppendLine("No data loaded");
            }

            return stats.ToString();
        }

        #endregion
    }
}
