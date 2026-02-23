//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.IO;

namespace WpfHexaEditor.Core
{
    /// <summary>
    /// Buffered file reader that reads data in blocks to minimize seek operations
    /// Dramatically improves performance when reading many small fields sequentially
    /// </summary>
    public class BufferedFileReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly int _blockSize;
        private byte[] _buffer;
        private long _bufferStartOffset;
        private int _bufferValidLength;
        private bool _disposed;

        /// <summary>
        /// Initialize buffered reader
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <param name="blockSize">Size of read buffer (default: 64KB)</param>
        public BufferedFileReader(Stream stream, int blockSize = 65536)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            if (blockSize <= 0)
                throw new ArgumentException("Block size must be positive", nameof(blockSize));

            _blockSize = blockSize;
            _buffer = new byte[blockSize];
            _bufferStartOffset = -1; // Invalid offset to force initial read
            _bufferValidLength = 0;
        }

        /// <summary>
        /// Read bytes from the stream at the specified offset
        /// Uses buffered reads to minimize seek operations
        /// </summary>
        /// <param name="offset">Absolute offset in the stream</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Byte array containing the requested data, or null if read failed</returns>
        public byte[] ReadBytes(long offset, int length)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BufferedFileReader));

            if (offset < 0 || offset >= _stream.Length)
                return null;

            if (length <= 0)
                return Array.Empty<byte>();

            // Clamp length to stream bounds
            if (offset + length > _stream.Length)
                length = (int)(_stream.Length - offset);

            // Check if requested data is entirely within the current buffer
            if (_bufferStartOffset >= 0 &&
                offset >= _bufferStartOffset &&
                offset + length <= _bufferStartOffset + _bufferValidLength)
            {
                // Cache hit - extract from buffer
                int bufferOffset = (int)(offset - _bufferStartOffset);
                byte[] result = new byte[length];
                Array.Copy(_buffer, bufferOffset, result, 0, length);
                return result;
            }

            // Cache miss - need to read new block
            // If requested data is larger than buffer, read directly
            if (length > _blockSize)
            {
                return ReadDirect(offset, length);
            }

            // Read a new block starting at the requested offset
            _bufferStartOffset = offset;
            _stream.Position = offset;
            _bufferValidLength = _stream.Read(_buffer, 0, _blockSize);

            if (_bufferValidLength == 0)
                return null;

            // Extract the requested bytes from the newly loaded buffer
            int bytesToCopy = Math.Min(length, _bufferValidLength);
            byte[] data = new byte[bytesToCopy];
            Array.Copy(_buffer, 0, data, 0, bytesToCopy);
            return data;
        }

        /// <summary>
        /// Read data directly from stream (bypassing buffer) for large requests
        /// </summary>
        private byte[] ReadDirect(long offset, int length)
        {
            _stream.Position = offset;
            byte[] data = new byte[length];
            int bytesRead = _stream.Read(data, 0, length);

            if (bytesRead < length)
            {
                // Resize if we read less than expected
                Array.Resize(ref data, bytesRead);
            }

            // Invalidate buffer after direct read
            _bufferStartOffset = -1;
            _bufferValidLength = 0;

            return data;
        }

        /// <summary>
        /// Clear the buffer cache (forces next read to reload from stream)
        /// </summary>
        public void InvalidateCache()
        {
            _bufferStartOffset = -1;
            _bufferValidLength = 0;
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public (long BufferStart, int BufferLength, int BlockSize) GetCacheInfo()
        {
            return (_bufferStartOffset, _bufferValidLength, _blockSize);
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Note: We don't dispose the stream as we don't own it
                _buffer = null;
                _disposed = true;
            }
        }
    }
}
