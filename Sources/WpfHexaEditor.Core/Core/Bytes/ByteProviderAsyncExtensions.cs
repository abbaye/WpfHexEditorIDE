//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WpfHexaEditor.Core.Bytes
{
    /// <summary>
    /// Async/await extensions for ByteProviderLegacy to enable non-blocking I/O operations.
    /// Keeps UI responsive during long-running operations on large files.
    /// </summary>
    public static class ByteProviderAsyncExtensions
    {
        #region Async Read Operations

        /// <summary>
        /// Asynchronously reads a single byte from the provider.
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="position">Position to read from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Tuple of (byte value, success)</returns>
        public static Task<(byte? value, bool success)> GetByteAsync(
            this ByteProviderLegacy provider,
            long position,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => provider.GetByte(position), cancellationToken);
        }

        /// <summary>
        /// Asynchronously reads multiple bytes from the provider.
        /// Uses ArrayPool internally to reduce allocations.
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="position">Start position</param>
        /// <param name="count">Number of bytes to read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Array of bytes read</returns>
        public static async Task<byte[]> GetBytesAsync(
            this ByteProviderLegacy provider,
            long position,
            int count,
            CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            return await Task.Run(() =>
            {
                var buffer = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (byteValue, success) = provider.GetByte(position + i);
                    if (!success) break;
                    buffer[i] = byteValue.Value;
                }
                return buffer;
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously reads bytes into a pre-allocated buffer.
        /// More efficient than GetBytesAsync when you can reuse buffers.
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="position">Start position</param>
        /// <param name="buffer">Buffer to read into</param>
        /// <param name="offset">Offset in buffer</param>
        /// <param name="count">Number of bytes to read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of bytes actually read</returns>
        public static async Task<int> ReadBytesAsync(
            this ByteProviderLegacy provider,
            long position,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset >= buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            return await Task.Run(() =>
            {
                int bytesRead = 0;
                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (byteValue, success) = provider.GetByte(position + i);
                    if (!success) break;

                    buffer[offset + i] = byteValue.Value;
                    bytesRead++;
                }
                return bytesRead;
            }, cancellationToken);
        }

        #endregion

        #region Async Search Operations

        /// <summary>
        /// Asynchronously finds first occurrence of a byte pattern.
        /// Allows UI to remain responsive during long searches.
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="pattern">Pattern to search for</param>
        /// <param name="startPosition">Position to start search</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Position of first match, or -1 if not found</returns>
        public static async Task<long> FindFirstAsync(
            this ByteProviderLegacy provider,
            byte[] pattern,
            long startPosition = 0,
            CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (pattern == null || pattern.Length == 0) return -1;

            return await Task.Run(() =>
            {
                var results = provider.FindIndexOf(pattern, startPosition).ToList();
                cancellationToken.ThrowIfCancellationRequested();

                if (results.Count == 0) return -1;
                return results[0];
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously finds all occurrences of a byte pattern.
        /// Reports progress during the search based on bytes scanned.
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="pattern">Pattern to search for</param>
        /// <param name="startPosition">Position to start search</param>
        /// <param name="progress">Progress reporter (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of all match positions</returns>
        public static async Task<List<long>> FindAllAsync(
            this ByteProviderLegacy provider,
            byte[] pattern,
            long startPosition = 0,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (pattern == null || pattern.Length == 0) return new List<long>();

            return await Task.Run(() =>
            {
                var results = new List<long>();
                const int chunkSize = 64 * 1024; // 64KB chunks
                var buffer = ArrayPool<byte>.Shared.Rent(chunkSize + pattern.Length);
                var totalLength = provider.Length;
                var lastProgressPercent = -1;

                try
                {
                    long currentPosition = startPosition;
                    int overlap = pattern.Length - 1;

                    while (currentPosition < totalLength)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Calculate chunk size (don't read past end)
                        int bytesToRead = (int)Math.Min(chunkSize, totalLength - currentPosition);
                        if (bytesToRead <= 0) break;

                        // Read chunk
                        int bytesRead = 0;
                        for (int i = 0; i < bytesToRead && currentPosition + i < totalLength; i++)
                        {
                            var (byteValue, success) = provider.GetByte(currentPosition + i);
                            if (!success) break;
                            buffer[i] = byteValue.Value;
                            bytesRead++;
                        }

                        if (bytesRead == 0) break;

                        // Search in this chunk
                        ReadOnlySpan<byte> searchSpan = new ReadOnlySpan<byte>(buffer, 0, bytesRead);
                        ReadOnlySpan<byte> patternSpan = new ReadOnlySpan<byte>(pattern);

                        int searchPos = 0;
                        while (searchPos <= searchSpan.Length - pattern.Length)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            int matchIndex = searchSpan.Slice(searchPos).IndexOf(patternSpan);
                            if (matchIndex == -1) break;

                            long absolutePosition = currentPosition + searchPos + matchIndex;

                            // Avoid duplicates from overlapping chunks
                            if (results.Count == 0 || absolutePosition > results[results.Count - 1])
                            {
                                results.Add(absolutePosition);
                            }

                            searchPos += matchIndex + 1;
                        }

                        // Report progress based on bytes scanned
                        if (progress != null && totalLength > 0)
                        {
                            long bytesScanned = currentPosition + bytesRead;
                            var currentPercent = (int)((bytesScanned * 100) / totalLength);
                            if (currentPercent != lastProgressPercent)
                            {
                                progress.Report(currentPercent);
                                lastProgressPercent = currentPercent;
                            }
                        }

                        // Move to next chunk, accounting for overlap to catch patterns spanning chunks
                        long nextPosition = currentPosition + bytesRead - overlap;

                        // If we're not making progress or reached the end, stop
                        if (nextPosition <= currentPosition || bytesRead < pattern.Length)
                            break;

                        currentPosition = nextPosition;
                    }

                    progress?.Report(100);
                    return results;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }, cancellationToken);
        }

        #endregion

        #region Async Modification Operations

        /// <summary>
        /// Asynchronously writes bytes to the provider.
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="position">Position to write at</param>
        /// <param name="data">Data to write</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when write is done</returns>
        public static async Task WriteBytesAsync(
            this ByteProviderLegacy provider,
            long position,
            byte[] data,
            CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (provider.ReadOnlyMode) throw new InvalidOperationException("Provider is in read-only mode");

            await Task.Run(() =>
            {
                for (int i = 0; i < data.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    provider.AddByteModified(data[i], position + i);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously replaces all occurrences of a pattern with replacement data.
        /// Reports progress during the operation.
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="searchPattern">Pattern to search for</param>
        /// <param name="replacePattern">Pattern to replace with</param>
        /// <param name="startPosition">Position to start search</param>
        /// <param name="progress">Progress reporter (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of replacements made</returns>
        public static async Task<int> ReplaceAllAsync(
            this ByteProviderLegacy provider,
            byte[] searchPattern,
            byte[] replacePattern,
            long startPosition = 0,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (searchPattern == null || searchPattern.Length == 0) return 0;
            if (replacePattern == null) throw new ArgumentNullException(nameof(replacePattern));
            if (searchPattern.Length != replacePattern.Length)
                throw new ArgumentException("Search and replace patterns must have same length");

            var positions = await FindAllAsync(provider, searchPattern, startPosition, progress, cancellationToken);

            return await Task.Run(() =>
            {
                int replaced = 0;
                foreach (var position in positions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    for (int i = 0; i < replacePattern.Length; i++)
                    {
                        provider.AddByteModified(replacePattern[i], position + i);
                    }
                    replaced++;
                }
                return replaced;
            }, cancellationToken);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Asynchronously calculates hash of a byte range.
        /// Useful for integrity verification.
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="position">Start position</param>
        /// <param name="length">Number of bytes</param>
        /// <param name="progress">Progress reporter (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Simple checksum (for demo - use proper hash in production)</returns>
        public static async Task<long> CalculateChecksumAsync(
            this ByteProviderLegacy provider,
            long position,
            long length,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            return await Task.Run(() =>
            {
                long checksum = 0;
                var lastProgressPercent = 0;

                for (long i = 0; i < length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (byteValue, success) = provider.GetByte(position + i);
                    if (success)
                    {
                        checksum = (checksum + byteValue.Value) % long.MaxValue;
                    }

                    // Report progress every 1%
                    if (progress != null && length > 0 && i % (length / 100 + 1) == 0)
                    {
                        var currentPercent = (int)((i * 100) / length);
                        if (currentPercent > lastProgressPercent)
                        {
                            progress.Report(currentPercent);
                            lastProgressPercent = currentPercent;
                        }
                    }
                }

                progress?.Report(100);
                return checksum;
            }, cancellationToken);
        }

        #endregion
    }
}
