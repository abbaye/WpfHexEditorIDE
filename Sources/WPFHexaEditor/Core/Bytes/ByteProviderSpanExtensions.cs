//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Buffers;

namespace WpfHexaEditor.Core.Bytes
{
    /// <summary>
    /// High-performance extensions for ByteProvider using Span&lt;byte&gt; and ArrayPool
    /// to reduce memory allocations and improve performance
    /// </summary>
    public static class ByteProviderSpanExtensions
    {
        /// <summary>
        /// Gets bytes as a ReadOnlySpan using ArrayPool to avoid allocations.
        /// IMPORTANT: The returned span is only valid within the using block scope.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="position">Start position</param>
        /// <param name="count">Number of bytes to read</param>
        /// <param name="buffer">Rented buffer from ArrayPool - MUST be returned after use</param>
        /// <returns>ReadOnlySpan pointing to the rented buffer data</returns>
        /// <example>
        /// byte[] rentedBuffer = null;
        /// try
        /// {
        ///     var span = provider.GetBytesSpan(0, 100, out rentedBuffer);
        ///     // Use span here
        /// }
        /// finally
        /// {
        ///     if (rentedBuffer != null)
        ///         ArrayPool&lt;byte&gt;.Shared.Return(rentedBuffer);
        /// }
        /// </example>
        public static ReadOnlySpan<byte> GetBytesSpan(this ByteProvider provider, long position, int count, out byte[] buffer)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
            {
                buffer = null;
                return ReadOnlySpan<byte>.Empty;
            }

            // Rent buffer from ArrayPool - reduces GC pressure
            buffer = ArrayPool<byte>.Shared.Rent(count);

            // Read bytes into rented buffer
            for (int i = 0; i < count; i++)
            {
                var (byteValue, success) = provider.GetByte(position + i);
                if (!success)
                {
                    // If read fails, return partial data
                    return new ReadOnlySpan<byte>(buffer, 0, i);
                }
                buffer[i] = byteValue.Value;
            }

            return new ReadOnlySpan<byte>(buffer, 0, count);
        }

        /// <summary>
        /// Gets bytes as a Span for modification operations.
        /// The span points to a rented buffer that MUST be returned to the ArrayPool.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="position">Start position</param>
        /// <param name="count">Number of bytes to read</param>
        /// <param name="buffer">Rented buffer from ArrayPool - MUST be returned after use</param>
        /// <returns>Span pointing to the rented buffer data</returns>
        public static Span<byte> GetBytesSpanMutable(this ByteProvider provider, long position, int count, out byte[] buffer)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
            {
                buffer = null;
                return Span<byte>.Empty;
            }

            // Rent buffer from ArrayPool
            buffer = ArrayPool<byte>.Shared.Rent(count);

            // Read bytes into rented buffer
            for (int i = 0; i < count; i++)
            {
                var (byteValue, success) = provider.GetByte(position + i);
                if (!success)
                {
                    return new Span<byte>(buffer, 0, i);
                }
                buffer[i] = byteValue.Value;
            }

            return new Span<byte>(buffer, 0, count);
        }

        /// <summary>
        /// Writes span data to the ByteProvider.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="position">Start position</param>
        /// <param name="data">Data to write</param>
        /// <returns>Number of bytes written</returns>
        public static int WriteBytesSpan(this ByteProvider provider, long position, ReadOnlySpan<byte> data)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (provider.ReadOnlyMode) return 0;

            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    provider.AddByteModified(data[i], position + i);
                }
                return data.Length;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// High-performance buffer helper for reading large chunks.
        /// Automatically handles ArrayPool rental and disposal.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="position">Start position</param>
        /// <param name="count">Number of bytes</param>
        /// <returns>PooledBuffer that MUST be disposed</returns>
        public static PooledBuffer GetBytesPooled(this ByteProvider provider, long position, int count)
        {
            return new PooledBuffer(provider, position, count);
        }

        /// <summary>
        /// Compares two byte sequences for equality using Span (faster than array comparison).
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="position">Position to start comparison</param>
        /// <param name="pattern">Pattern to compare against</param>
        /// <returns>True if sequences match</returns>
        public static bool SequenceEqualAt(this ByteProvider provider, long position, ReadOnlySpan<byte> pattern)
        {
            if (provider == null) return false;
            if (pattern.Length == 0) return false;

            byte[] buffer = null;
            try
            {
                var span = provider.GetBytesSpan(position, pattern.Length, out buffer);
                return span.SequenceEqual(pattern);
            }
            finally
            {
                if (buffer != null)
                    ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// RAII wrapper for ArrayPool buffer - ensures automatic return to pool.
    /// Use with 'using' statement for automatic disposal.
    /// </summary>
    /// <example>
    /// using (var pooled = provider.GetBytesPooled(0, 1000))
    /// {
    ///     ReadOnlySpan&lt;byte&gt; data = pooled.Span;
    ///     // Use data here
    /// } // Buffer automatically returned to pool
    /// </example>
    public readonly struct PooledBuffer : IDisposable
    {
        private readonly byte[] _buffer;
        private readonly int _length;

        internal PooledBuffer(ByteProvider provider, long position, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            _length = count;

            if (count == 0)
            {
                _buffer = null;
                return;
            }

            _buffer = ArrayPool<byte>.Shared.Rent(count);

            // Read data
            for (int i = 0; i < count; i++)
            {
                var (byteValue, success) = provider.GetByte(position + i);
                if (!success)
                {
                    _length = i;
                    break;
                }
                _buffer[i] = byteValue.Value;
            }
        }

        /// <summary>
        /// Gets the span view of the pooled buffer.
        /// WARNING: Only valid until Dispose() is called!
        /// </summary>
        public ReadOnlySpan<byte> Span => _buffer == null ? ReadOnlySpan<byte>.Empty : new ReadOnlySpan<byte>(_buffer, 0, _length);

        /// <summary>
        /// Gets the length of actual data in the buffer
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// Returns the buffer to the ArrayPool
        /// </summary>
        public void Dispose()
        {
            if (_buffer != null)
                ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}
