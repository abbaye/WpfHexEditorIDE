// ==========================================================
// Project: WpfHexEditor.Core
// File: FileProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Pure file I/O layer responsible exclusively for reading and writing bytes
//     from/to the underlying stream. Works with physical file offsets only and
//     provides aggressive 64 KB block caching to minimize OS I/O calls.
//
// Architecture Notes:
//     Implements IDisposable for stream lifetime management. Only component in
//     ByteProvider V2 that touches the physical file. ByteReader delegates all
//     raw reads to FileProvider; no edit awareness here.
//
// ==========================================================

using System;
using System.IO;

namespace WpfHexEditor.Core.Bytes
{
    /// <summary>
    /// FileProvider - Pure file I/O operations with caching.
    /// Responsible ONLY for reading/writing bytes from/to the underlying stream.
    /// Works exclusively with PHYSICAL positions (file offsets).
    /// Ultra-optimized with aggressive caching strategy.
    /// </summary>
    public sealed class FileProvider : IDisposable
    {
        private Stream _stream;
        private readonly byte[] _cache;
        private long _cachePosition = -1;
        private int _cacheLength = 0;
        private const int CACHE_SIZE = 64 * 1024; // 64KB cache for sequential reads
        private bool _disposed = false;
        private readonly object _cacheLock = new object(); // Thread-safety for cache access

        /// <summary>
        /// Gets the file path (if opened from file).
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Gets the physical length of the file/stream.
        /// </summary>
        public long Length => _stream?.Length ?? 0;

        /// <summary>
        /// Gets whether a file/stream is currently open.
        /// </summary>
        public bool IsOpen => _stream != null;

        /// <summary>
        /// Gets whether the file is read-only.
        /// </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Gets the underlying stream.
        /// </summary>
        public Stream Stream => _stream;

        public FileProvider()
        {
            _cache = new byte[CACHE_SIZE];
        }

        /// <summary>
        /// Open a file for reading/writing.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="readOnly">Open in read-only mode</param>
        public void OpenFile(string filePath, bool readOnly = false)
        {
            if (IsOpen)
                throw new InvalidOperationException("A file is already open. Close it first.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            FilePath = filePath;
            IsReadOnly = readOnly;

            var fileAccess = readOnly ? FileAccess.Read : FileAccess.ReadWrite;
            var fileShare  = readOnly ? FileShare.ReadWrite : FileShare.Read;

            try
            {
                _stream = new FileStream(filePath, FileMode.Open, fileAccess, fileShare, CACHE_SIZE, FileOptions.RandomAccess);
            }
            catch (Exception ex) when (!readOnly && (ex is UnauthorizedAccessException || ex is IOException))
            {
                // File is write-protected (ACL restriction) OR locked by another process (sharing violation).
                // Fall back to read-only silently — the hex editor remains fully usable in view mode.
                IsReadOnly = true;
                _stream    = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                                            FileShare.ReadWrite, CACHE_SIZE, FileOptions.RandomAccess);
            }

            InvalidateCache();
        }

        /// <summary>
        /// Open from an existing stream.
        /// </summary>
        public void OpenStream(Stream stream, bool readOnly = false)
        {
            if (IsOpen)
                throw new InvalidOperationException("A stream is already open. Close it first.");

            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            IsReadOnly = readOnly;
            FilePath = null;
            InvalidateCache();
        }

        /// <summary>
        /// Open from a byte array in memory.
        /// Creates a MemoryStream from the byte array.
        /// </summary>
        /// <param name="data">Byte array to edit</param>
        /// <param name="readOnly">Open in read-only mode</param>
        public void OpenMemory(byte[] data, bool readOnly = false)
        {
            if (IsOpen)
                throw new InvalidOperationException("A stream is already open. Close it first.");

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            // Create a memory stream from the byte array
            // If not read-only, make it expandable
            _stream = readOnly
                ? new MemoryStream(data, false) // Read-only, non-writable
                : new MemoryStream(data, 0, data.Length, true, true); // Writable, expandable, expose buffer

            IsReadOnly = readOnly;
            FilePath = null;
            InvalidateCache();
        }

        /// <summary>
        /// Read a single byte at a physical position.
        /// Uses cache for performance.
        /// Thread-safe for concurrent reads.
        /// </summary>
        /// <param name="physicalPosition">Physical file offset</param>
        /// <returns>Byte value and success flag</returns>
        public (byte value, bool success) ReadByte(long physicalPosition)
        {
            if (!IsOpen || physicalPosition < 0 || physicalPosition >= Length)
                return (0, false);

            // CRITICAL: Lock cache access for thread-safety during async operations
            lock (_cacheLock)
            {
                // Check if byte is in cache
                if (IsInCache(physicalPosition))
                {
                    int cacheOffset = (int)(physicalPosition - _cachePosition);
                    // Defensive bounds check (race condition protection)
                    if (cacheOffset < 0 || cacheOffset >= _cacheLength)
                        return (0, false);
                    return (_cache[cacheOffset], true);
                }

                // Cache miss - read new block
                if (!FillCache(physicalPosition))
                    return (0, false);

                // Verify position is now in cache (handles edge cases like EOF)
                if (!IsInCache(physicalPosition))
                    return (0, false);

                int offset = (int)(physicalPosition - _cachePosition);
                // Defensive bounds check (race condition protection)
                if (offset < 0 || offset >= _cacheLength)
                    return (0, false);
                return (_cache[offset], true);
            }
        }

        /// <summary>
        /// Read multiple bytes starting at a physical position.
        /// Optimized for sequential and batch reads.
        /// Thread-safe for concurrent reads.
        /// </summary>
        /// <param name="physicalPosition">Starting physical offset</param>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>Byte array (may be shorter than requested if EOF)</returns>
        public byte[] ReadBytes(long physicalPosition, int count)
        {
            if (!IsOpen || physicalPosition < 0 || count <= 0)
                return Array.Empty<byte>();

            // Clamp to file length
            long available = Length - physicalPosition;
            if (available <= 0)
                return Array.Empty<byte>();

            count = (int)Math.Min(count, available);
            var result = new byte[count];

            // CRITICAL: Lock cache and stream access for thread-safety
            lock (_cacheLock)
            {
                // Try to read from cache first
                if (IsInCache(physicalPosition))
                {
                    int cacheOffset = (int)(physicalPosition - _cachePosition);
                    int availableInCache = _cacheLength - cacheOffset;

                    if (count <= availableInCache)
                    {
                        // Fully in cache
                        Array.Copy(_cache, cacheOffset, result, 0, count);
                        return result;
                    }
                    else
                    {
                        // Partially in cache
                        Array.Copy(_cache, cacheOffset, result, 0, availableInCache);

                        // Read remainder directly from stream
                        _stream.Position = physicalPosition + availableInCache;
                        int remaining = count - availableInCache;
                        int bytesRead = _stream.Read(result, availableInCache, remaining);

                        if (bytesRead < remaining)
                        {
                            // Resize if couldn't read all bytes
                            Array.Resize(ref result, availableInCache + bytesRead);
                        }

                        // Update cache with new data
                        FillCache(physicalPosition + availableInCache);
                        return result;
                    }
                }

                // Not in cache - read directly
                _stream.Position = physicalPosition;
                int totalRead = _stream.Read(result, 0, count);

                if (totalRead < count)
                    Array.Resize(ref result, totalRead);

                // Update cache if read was small enough
                if (count <= CACHE_SIZE)
                    FillCache(physicalPosition);

                return result;
            }
        }

        /// <summary>
        /// Read multiple bytes into a provided buffer (zero-allocation version).
        /// Optimized for high-performance scenarios where buffer reuse is critical.
        /// </summary>
        /// <param name="physicalPosition">Starting physical offset</param>
        /// <param name="buffer">Buffer to read into</param>
        /// <param name="offset">Offset in buffer to start writing</param>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>Number of bytes actually read</returns>
        public int ReadBytes(long physicalPosition, byte[] buffer, int offset, int count)
        {
            if (!IsOpen || physicalPosition < 0 || count <= 0 || buffer == null)
                return 0;

            // Clamp to file length
            long available = Length - physicalPosition;
            if (available <= 0)
                return 0;

            count = (int)Math.Min(count, available);

            // Try to read from cache first
            if (IsInCache(physicalPosition))
            {
                int cacheOffset = (int)(physicalPosition - _cachePosition);
                int availableInCache = _cacheLength - cacheOffset;

                if (count <= availableInCache)
                {
                    // Fully in cache
                    Array.Copy(_cache, cacheOffset, buffer, offset, count);
                    return count;
                }
                else
                {
                    // Partially in cache
                    Array.Copy(_cache, cacheOffset, buffer, offset, availableInCache);

                    // Read remainder directly from stream
                    _stream.Position = physicalPosition + availableInCache;
                    int remaining = count - availableInCache;
                    int bytesRead = _stream.Read(buffer, offset + availableInCache, remaining);

                    // Update cache with new data
                    FillCache(physicalPosition + availableInCache);
                    return availableInCache + bytesRead;
                }
            }

            // Not in cache - read directly
            _stream.Position = physicalPosition;
            int totalRead = _stream.Read(buffer, offset, count);

            // Update cache if read was small enough
            if (count <= CACHE_SIZE)
                FillCache(physicalPosition);

            return totalRead;
        }

        /// <summary>
        /// Write a single byte at a physical position.
        /// Invalidates cache at that position.
        /// </summary>
        public bool WriteByte(long physicalPosition, byte value)
        {
            if (!IsOpen || IsReadOnly || physicalPosition < 0)
                return false;

            try
            {
                _stream.Position = physicalPosition;
                _stream.WriteByte(value);
                // Note: Caller should call Flush() after batch writes for better performance

                // Invalidate cache around written position
                InvalidateCacheAt(physicalPosition);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Write multiple bytes at a physical position.
        /// Invalidates cache in that region.
        /// </summary>
        public bool WriteBytes(long physicalPosition, byte[] data)
        {
            if (!IsOpen || IsReadOnly || physicalPosition < 0 || data == null || data.Length == 0)
                return false;

            try
            {
                _stream.Position = physicalPosition;
                _stream.Write(data, 0, data.Length);
                _stream.Flush();

                // Invalidate cache in written region
                InvalidateCache();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Flush pending writes to disk.
        /// </summary>
        public void Flush()
        {
            _stream?.Flush();
        }

        /// <summary>
        /// Reload the file from disk by invalidating the cache and resetting the stream position.
        /// The underlying FileStream remains open — no re-open needed.
        /// Call this after an external process has modified the file on disk.
        /// </summary>
        public void Reload()
        {
            if (!IsOpen) return;

            lock (_cacheLock)
            {
                InvalidateCache();
                _stream.Position = 0;
            }
        }

        /// <summary>
        /// Close the file/stream.
        /// </summary>
        public void Close()
        {
            if (_stream != null)
            {
                _stream.Close();
                _stream = null;
            }

            FilePath = null;
            InvalidateCache();
        }

        #region Cache Management

        private bool IsInCache(long physicalPosition)
        {
            return _cachePosition >= 0 &&
                   physicalPosition >= _cachePosition &&
                   physicalPosition < _cachePosition + _cacheLength;
        }

        private bool FillCache(long physicalPosition)
        {
            if (!IsOpen)
                return false;

            try
            {
                // Align cache to block boundary for better performance
                _cachePosition = (physicalPosition / CACHE_SIZE) * CACHE_SIZE;

                _stream.Position = _cachePosition;
                _cacheLength = _stream.Read(_cache, 0, CACHE_SIZE);

                return _cacheLength > 0;
            }
            catch
            {
                InvalidateCache();
                return false;
            }
        }

        private void InvalidateCache()
        {
            _cachePosition = -1;
            _cacheLength = 0;
        }

        private void InvalidateCacheAt(long position)
        {
            // Simple strategy: invalidate entire cache if write intersects
            if (IsInCache(position))
                InvalidateCache();
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
            }
        }
    }
}
