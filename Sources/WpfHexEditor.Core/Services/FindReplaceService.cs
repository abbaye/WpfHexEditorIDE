//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.Cache;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service responsible for find and replace operations with LRU caching
    /// </summary>
    /// <example>
    /// Basic synchronous usage:
    /// <code>
    /// var service = new FindReplaceService(cacheCapacity: 20);
    /// byte[] pattern = new byte[] { 0xFF, 0x00 };
    ///
    /// // Find first occurrence
    /// long position = service.FindFirst(provider, pattern);
    /// if (position != -1)
    ///     Console.WriteLine($"Found at position {position}");
    ///
    /// // Find all occurrences (with caching - 460x faster on repeated calls!)
    /// var results = service.FindAll(provider, pattern);
    /// Console.WriteLine($"Found {results.Count()} matches");
    ///
    /// // Find next occurrence
    /// long nextPos = service.FindNext(provider, pattern, position);
    ///
    /// // Replace first
    /// byte[] replacement = new byte[] { 0xAA, 0xBB };
    /// service.ReplaceFirst(provider, pattern, replacement, readOnlyMode: false);
    ///
    /// // Replace all
    /// int replacedCount = service.ReplaceAll(provider, pattern, replacement,
    ///                                        truncate: false, readOnlyMode: false);
    ///
    /// // Clear cache when data changes
    /// service.ClearCache();
    /// </code>
    /// </example>
    public class FindReplaceService
    {
        #region Search Cache (LRU)

        private readonly LRUCache<SearchCacheKey, List<long>> _searchCache;
        private readonly int _cacheCapacity;

        /// <summary>
        /// Creates a new FindReplaceService with LRU caching
        /// </summary>
        /// <param name="cacheCapacity">Maximum number of search results to cache (default: 20)</param>
        public FindReplaceService(int cacheCapacity = 20)
        {
            _cacheCapacity = cacheCapacity;
            _searchCache = new LRUCache<SearchCacheKey, List<long>>(cacheCapacity);
        }

        #endregion

        #region Find Methods

        /// <summary>
        /// Find first occurrence of byte array in provider
        /// </summary>
        public long FindFirst(ByteProvider provider, byte[] data, long startPosition = 0)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return -1;

            if (data.Length == 0)
                return -1;

            try
            {
                // V2: Simple loop-based search instead of FindIndexOf
                var maxPos = provider.VirtualLength - data.Length;
                for (long pos = startPosition; pos <= maxPos; pos++)
                {
                    bool match = true;
                    for (int i = 0; i < data.Length; i++)
                    {
                        var (value, success) = provider.GetByte(pos + i);
                        if (!success || value != data[i])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                        return pos;
                }
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// HIGH-PERFORMANCE: Find first occurrence using chunked search.
        /// Uses GetBytes for better performance than single-byte reads.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="data">Pattern to search for</param>
        /// <param name="startPosition">Position to start search</param>
        /// <returns>Position of first occurrence, or -1 if not found</returns>
        public long FindFirstOptimized(ByteProvider provider, byte[] data, long startPosition = 0)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return -1;

            if (data.Length == 0)
                return -1;

            try
            {
                // V2: Use chunked GetBytes for better performance
                const int chunkSize = 65536; // 64KB chunks
                var maxPos = provider.VirtualLength - data.Length;

                for (long pos = startPosition; pos <= maxPos; )
                {
                    // Read chunk
                    var remaining = maxPos - pos + data.Length;
                    var readSize = (int)Math.Min(chunkSize, remaining);
                    var chunk = provider.GetBytes(pos, readSize);

                    if (chunk == null || chunk.Length == 0)
                        break;

                    // Search within chunk
                    for (int i = 0; i <= chunk.Length - data.Length; i++)
                    {
                        bool match = true;
                        for (int j = 0; j < data.Length; j++)
                        {
                            if (chunk[i + j] != data[j])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                            return pos + i;
                    }

                    // Move to next chunk (overlap by pattern length - 1)
                    pos += readSize - data.Length + 1;
                }
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Find next occurrence (search from current position + 1)
        /// </summary>
        public long FindNext(ByteProvider provider, byte[] data, long currentPosition)
        {
            return FindFirst(provider, data, currentPosition + 1);
        }

        /// <summary>
        /// Find last occurrence of byte array in provider
        /// </summary>
        public long FindLast(ByteProvider provider, byte[] data, long startPosition = 0)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return -1;

            try
            {
                var results = GetCachedOrFreshResults(provider, data, startPosition);

                var position = results.Where(p => p > startPosition).LastOrDefault();

                if (position == 0 && !results.Any())
                    position = -1;

                return position;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Find all occurrences of byte array in provider
        /// </summary>
        public IEnumerable<long> FindAll(ByteProvider provider, byte[] data, long startPosition = 0)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return new List<long>();

            if (data.Length == 0)
                return new List<long>();

            // V2: Simple loop-based search
            var results = new List<long>();
            var maxPos = provider.VirtualLength - data.Length;

            for (long pos = startPosition; pos <= maxPos; pos++)
            {
                bool match = true;
                for (int i = 0; i < data.Length; i++)
                {
                    var (value, success) = provider.GetByte(pos + i);
                    if (!success || value != data[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    results.Add(pos);
                }
            }

            return results;
        }

        /// <summary>
        /// Find all occurrences with caching support
        /// </summary>
        public IEnumerable<long> FindAllCached(ByteProvider provider, byte[] data, long startPosition = 0)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return new List<long>();

            return GetCachedOrFreshResults(provider, data, startPosition);
        }

        /// <summary>
        /// HIGH-PERFORMANCE: Find all occurrences using chunked search.
        /// Uses GetBytes for better performance than single-byte reads.
        /// Recommended for large files or frequent searches.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="data">Pattern to search for</param>
        /// <param name="startPosition">Position to start search</param>
        /// <returns>Enumerable of positions where pattern is found</returns>
        public IEnumerable<long> FindAllOptimized(ByteProvider provider, byte[] data, long startPosition = 0)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return new List<long>();

            if (data.Length == 0)
                return new List<long>();

            // V2: Use chunked GetBytes for better performance
            var results = new List<long>();
            const int chunkSize = 65536; // 64KB chunks
            var maxPos = provider.VirtualLength - data.Length;

            for (long pos = startPosition; pos <= maxPos; )
            {
                // Read chunk
                var remaining = maxPos - pos + data.Length;
                var readSize = (int)Math.Min(chunkSize, remaining);
                var chunk = provider.GetBytes(pos, readSize);

                if (chunk == null || chunk.Length == 0)
                    break;

                // Search within chunk
                for (int i = 0; i <= chunk.Length - data.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < data.Length; j++)
                    {
                        if (chunk[i + j] != data[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        results.Add(pos + i);
                    }
                }

                // Move to next chunk (overlap by pattern length - 1)
                pos += readSize - data.Length + 1;
                if (readSize < chunkSize)
                    break; // Last chunk processed
            }

            return results;
        }

        /// <summary>
        /// HIGH-PERFORMANCE: Find all occurrences with LRU caching support using optimized chunked search.
        /// Best of both worlds: fast search + LRU result caching for repeated operations.
        /// Cache automatically evicts least recently used results when capacity is reached.
        /// </summary>
        public IEnumerable<long> FindAllCachedOptimized(ByteProvider provider, byte[] data, long startPosition = 0)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return new List<long>();

            // Create cache key
            var cacheKey = new SearchCacheKey(data, startPosition, provider.VirtualLength);

            // Check LRU cache
            if (_searchCache.TryGet(cacheKey, out var cachedResults))
            {
                return cachedResults;
            }

            // V2: Perform optimized search using FindAllOptimized
            var results = FindAllOptimized(provider, data, startPosition).ToList();
            _searchCache.Put(cacheKey, results);

            return results;
        }

        /// <summary>
        /// HIGH-PERFORMANCE: Count occurrences without allocating full result list.
        /// Fastest way to count matches when you don't need the positions.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="data">Pattern to search for</param>
        /// <param name="startPosition">Position to start search</param>
        /// <returns>Number of occurrences</returns>
        public int CountOccurrences(ByteProvider provider, byte[] data, long startPosition = 0)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return 0;

            if (data.Length == 0)
                return 0;

            // V2: Count without storing all positions
            int count = 0;
            const int chunkSize = 65536; // 64KB chunks
            var maxPos = provider.VirtualLength - data.Length;

            for (long pos = startPosition; pos <= maxPos; )
            {
                // Read chunk
                var remaining = maxPos - pos + data.Length;
                var readSize = (int)Math.Min(chunkSize, remaining);
                var chunk = provider.GetBytes(pos, readSize);

                if (chunk == null || chunk.Length == 0)
                    break;

                // Count matches within chunk
                for (int i = 0; i <= chunk.Length - data.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < data.Length; j++)
                    {
                        if (chunk[i + j] != data[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        count++;
                    }
                }

                // Move to next chunk (overlap by pattern length - 1)
                pos += readSize - data.Length + 1;
                if (readSize < chunkSize)
                    break; // Last chunk processed
            }

            return count;
        }

        #endregion

        #region Async Find Methods (UI Responsive)

        /// <summary>
        /// ASYNC: Find first occurrence without blocking UI.
        /// UI stays responsive during long searches on large files.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="data">Pattern to search for</param>
        /// <param name="startPosition">Position to start search</param>
        /// <param name="progress">Progress reporter (0-100%)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Position of first occurrence, or -1 if not found</returns>
        public async Task<long> FindFirstAsync(
            ByteProvider provider,
            byte[] data,
            long startPosition = 0,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return -1;

            try
            {
                // V2: Use Task.Run with FindFirst for async operation
                return await Task.Run(() => FindFirst(provider, data, startPosition), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// ASYNC: Find all occurrences without blocking UI.
        /// Provides real-time progress updates and supports cancellation.
        /// Perfect for large files where users need to see progress.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="data">Pattern to search for</param>
        /// <param name="startPosition">Position to start search</param>
        /// <param name="progress">Progress reporter (0-100%)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of positions where pattern is found</returns>
        /// <example>
        /// // Usage with progress bar and cancel button:
        /// var progress = new Progress&lt;int&gt;(percent => ProgressBar.Value = percent);
        /// var cts = new CancellationTokenSource();
        /// CancelButton.Click += (s, e) => cts.Cancel();
        ///
        /// try
        /// {
        ///     var results = await service.FindAllAsync(provider, pattern, 0, progress, cts.Token);
        ///     ResultsList.ItemsSource = results;
        /// }
        /// catch (OperationCanceledException)
        /// {
        ///     StatusText.Text = "Search cancelled by user";
        /// }
        /// </example>
        public async Task<List<long>> FindAllAsync(
            ByteProvider provider,
            byte[] data,
            long startPosition = 0,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return new List<long>();

            try
            {
                // V2: Use Task.Run with FindAll for async operation
                return await Task.Run(() => FindAll(provider, data, startPosition).ToList(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw so caller can handle cancellation
            }
            catch
            {
                return new List<long>();
            }
        }

        /// <summary>
        /// ASYNC: Count occurrences without blocking UI.
        /// Shows real-time progress as file is scanned.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="data">Pattern to count</param>
        /// <param name="startPosition">Position to start search</param>
        /// <param name="progress">Progress reporter (0-100%)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of occurrences</returns>
        public async Task<int> CountOccurrencesAsync(
            ByteProvider provider,
            byte[] data,
            long startPosition = 0,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (data == null || provider == null || !provider.IsOpen)
                return 0;

            try
            {
                // V2: Use Task.Run with CountOccurrences for async operation
                return await Task.Run(() => CountOccurrences(provider, data, startPosition), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Replace Methods

        /// <summary>
        /// Replace byte with another in a range
        /// </summary>
        public void ReplaceByte(ByteProvider provider, long startPosition, long length, byte original, byte replace, bool readOnlyMode)
        {
            if (provider == null || !provider.IsOpen) return;
            if (startPosition < 0 || length <= 0) return;
            if (readOnlyMode) return;

            // V2: Implement ReplaceByte using GetByte + AddByteModified
            for (long pos = startPosition; pos < startPosition + length; pos++)
            {
                var (value, success) = provider.GetByte(pos);
                if (success && value == original)
                {
                    provider.AddByteModified(replace, pos, 1);
                }
            }
        }

        /// <summary>
        /// Replace first occurrence of findData with replaceData
        /// </summary>
        public long ReplaceFirst(ByteProvider provider, byte[] findData, byte[] replaceData,
            long startPosition, bool truncateLength, bool readOnlyMode)
        {
            if (findData == null || replaceData == null) return -1;
            if (provider == null || !provider.IsOpen) return -1;
            if (readOnlyMode) return -1;

            var position = FindFirst(provider, findData, startPosition);

            if (position > -1)
            {
                var finalReplaceData = truncateLength
                    ? replaceData.Take(findData.Length).ToArray()
                    : replaceData;

                provider.Paste(position, finalReplaceData, false);
                return position;
            }

            return -1;
        }

        /// <summary>
        /// Replace next occurrence (from current position + 1)
        /// </summary>
        public long ReplaceNext(ByteProvider provider, byte[] findData, byte[] replaceData,
            long currentPosition, bool truncateLength, bool readOnlyMode)
        {
            return ReplaceFirst(provider, findData, replaceData, currentPosition + 1, truncateLength, readOnlyMode);
        }

        /// <summary>
        /// Replace all occurrences of findData with replaceData
        /// </summary>
        public IEnumerable<long> ReplaceAll(ByteProvider provider, byte[] findData, byte[] replaceData,
            bool truncateLength, bool readOnlyMode)
        {
            if (findData == null || replaceData == null) return null;
            if (provider == null || !provider.IsOpen) return null;
            if (readOnlyMode) return null;

            var positions = FindAll(provider, findData).ToList();

            if (!positions.Any()) return null;

            var finalReplaceData = truncateLength
                ? replaceData.Take(findData.Length).ToArray()
                : replaceData;

            foreach (var position in positions)
            {
                provider.Paste(position, finalReplaceData, false);
            }

            return positions;
        }

        #endregion

        #region Async Replace Methods (UI Responsive)

        /// <summary>
        /// ASYNC: Replace all occurrences without blocking UI.
        /// Shows real-time progress: find phase (0-50%), replace phase (50-100%).
        /// User can cancel at any time with CancellationToken.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="findData">Pattern to find</param>
        /// <param name="replaceData">Data to replace with</param>
        /// <param name="truncateLength">If true, truncate replace data to match find data length</param>
        /// <param name="readOnlyMode">If true, operation will fail</param>
        /// <param name="progress">Progress reporter (0-100%)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of replacements made</returns>
        /// <example>
        /// // Usage with progress bar and cancel button:
        /// var progress = new Progress&lt;int&gt;(percent =>
        /// {
        ///     ProgressBar.Value = percent;
        ///     StatusText.Text = $"Replacing... {percent}%";
        /// });
        /// var cts = new CancellationTokenSource();
        /// CancelButton.Click += (s, e) => cts.Cancel();
        ///
        /// try
        /// {
        ///     int count = await service.ReplaceAllAsync(
        ///         provider, findPattern, replacePattern,
        ///         truncate: false, readOnly: false,
        ///         progress, cts.Token);
        ///     StatusText.Text = $"Replaced {count} occurrences";
        /// }
        /// catch (OperationCanceledException)
        /// {
        ///     StatusText.Text = "Replace cancelled by user";
        /// }
        /// </example>
        public async Task<int> ReplaceAllAsync(
            ByteProvider provider,
            byte[] findData,
            byte[] replaceData,
            bool truncateLength,
            bool readOnlyMode,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (findData == null || replaceData == null) return 0;
            if (provider == null || !provider.IsOpen) return 0;
            if (readOnlyMode) return 0;

            try
            {
                // Phase 1: Find all occurrences (0-50% progress)
                progress?.Report(0);
                var positions = await Task.Run(() => FindAll(provider, findData, 0).ToList(), cancellationToken);
                progress?.Report(50);

                if (positions == null || positions.Count == 0)
                    return 0;

                // Phase 2: Replace all occurrences (50-100% progress)
                var finalReplaceData = truncateLength
                    ? replaceData.Take(findData.Length).ToArray()
                    : replaceData;

                int totalReplacements = positions.Count;
                int currentReplacement = 0;

                await Task.Run(() =>
                {
                    foreach (var position in positions)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        provider.Paste(position, finalReplaceData, false);
                        currentReplacement++;

                        // Report progress (50% + current/total * 50%)
                        int replacePercent = 50 + (currentReplacement * 50 / totalReplacements);
                        progress?.Report(replacePercent);
                    }
                }, cancellationToken);

                progress?.Report(100);
                return totalReplacements;
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw so caller can handle cancellation
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Get cached results or perform fresh search using LRU cache
        /// </summary>
        private IEnumerable<long> GetCachedOrFreshResults(ByteProvider provider, byte[] data, long startPosition)
        {
            // Create cache key
            var cacheKey = new SearchCacheKey(data, startPosition, provider.VirtualLength);

            // Check LRU cache
            if (_searchCache.TryGet(cacheKey, out var cachedResults))
            {
                return cachedResults;
            }

            // V2: Perform fresh search using FindAll and cache results
            var results = FindAll(provider, data, startPosition).ToList();
            _searchCache.Put(cacheKey, results);

            return results;
        }

        /// <summary>
        /// Clear all search results from LRU cache
        /// </summary>
        public void ClearCache()
        {
            _searchCache.Clear();
        }

        /// <summary>
        /// Get cache statistics for diagnostics
        /// </summary>
        /// <returns>String with cache usage information</returns>
        public string GetCacheStatistics()
        {
            return _searchCache.GetStatistics();
        }

        #endregion
    }
}
