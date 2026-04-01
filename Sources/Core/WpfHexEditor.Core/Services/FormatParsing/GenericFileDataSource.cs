// ==========================================================
// Project: WpfHexEditor.Core
// File: GenericFileDataSource.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Read-only IBinaryDataSource that opens any file by path.
//     Used for format preview from Solution Explorer and Assembly Explorer
//     when no editor tab is open for the file.
//
// Architecture Notes:
//     Lazy stream opening — file is not opened until first ReadBytes() call.
//     Always read-only; DataChanged never fires (static file preview).
//     Disposed by the consumer (ParsedFieldsPlugin) on each new preview.
// ==========================================================

using System;
using System.IO;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.Services.FormatParsing
{
    /// <summary>
    /// Read-only <see cref="IBinaryDataSource"/> that opens any file by path.
    /// Used for format preview without requiring an editor tab.
    /// </summary>
    public sealed class GenericFileDataSource : IBinaryDataSource, IDisposable
    {
        private FileStream? _stream;

        public GenericFileDataSource(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public string? FilePath { get; }

        public long Length
        {
            get
            {
                EnsureStream();
                return _stream?.Length ?? 0;
            }
        }

        public bool IsReadOnly => true;

        public byte[] ReadBytes(long offset, int length)
        {
            EnsureStream();
            if (_stream == null || offset < 0 || length <= 0 || offset + length > _stream.Length)
                return Array.Empty<byte>();

            var buffer = new byte[length];
            _stream.Position = offset;
            int bytesRead = _stream.Read(buffer, 0, length);
            if (bytesRead != length)
            {
                var result = new byte[bytesRead];
                Array.Copy(buffer, result, bytesRead);
                return result;
            }
            return buffer;
        }

        public void WriteBytes(long offset, byte[] data)
            => throw new InvalidOperationException("GenericFileDataSource is read-only (preview mode).");

        /// <summary>Never fires — static file preview.</summary>
        public event EventHandler? DataChanged;

        private void EnsureStream()
        {
            if (_stream == null && FilePath != null && File.Exists(FilePath))
                _stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }
    }
}
