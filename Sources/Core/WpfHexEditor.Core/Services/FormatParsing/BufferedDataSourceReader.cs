// ==========================================================
// Project: WpfHexEditor.Core
// File: BufferedDataSourceReader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Wraps an IBinaryDataSource with a 64KB read-ahead buffer for
//     efficient sequential field parsing. Drop-in replacement for
//     BufferedFileReader(Stream) but works with any data source.
//
// Architecture Notes:
//     Same caching strategy as BufferedFileReader: single 64KB window,
//     repositioned on cache miss. Disposed after each parse session.
// ==========================================================

using System;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.Services.FormatParsing
{
    /// <summary>
    /// Buffered reader over <see cref="IBinaryDataSource"/> for efficient sequential reads.
    /// </summary>
    internal sealed class BufferedDataSourceReader : IDisposable
    {
        private readonly IBinaryDataSource _source;
        private byte[] _buffer;
        private long _bufferStart;
        private int _bufferLength;
        private readonly int _bufferSize;

        public BufferedDataSourceReader(IBinaryDataSource source, int bufferSize = 65536)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _bufferSize = bufferSize;
            _buffer = new byte[_bufferSize];
            _bufferStart = -1;
            _bufferLength = 0;
        }

        /// <summary>
        /// Read <paramref name="length"/> bytes at <paramref name="offset"/>.
        /// Returns null if the read extends beyond the source length.
        /// </summary>
        public byte[]? ReadBytes(long offset, int length)
        {
            if (offset < 0 || length <= 0 || offset + length > _source.Length)
                return null;

            // Check if fully within current buffer
            if (offset >= _bufferStart && offset + length <= _bufferStart + _bufferLength)
            {
                var result = new byte[length];
                Array.Copy(_buffer, (int)(offset - _bufferStart), result, 0, length);
                return result;
            }

            // Cache miss — refill buffer starting at the requested offset
            if (length <= _bufferSize)
            {
                int toRead = (int)Math.Min(_bufferSize, _source.Length - offset);
                var data = _source.ReadBytes(offset, toRead);
                if (data == null || data.Length < length)
                    return null;

                Array.Copy(data, 0, _buffer, 0, data.Length);
                _bufferStart = offset;
                _bufferLength = data.Length;

                var result = new byte[length];
                Array.Copy(_buffer, 0, result, 0, length);
                return result;
            }

            // Requested range larger than buffer — read directly
            return _source.ReadBytes(offset, length);
        }

        public void Dispose()
        {
            _buffer = null;
            _bufferLength = 0;
        }
    }
}
