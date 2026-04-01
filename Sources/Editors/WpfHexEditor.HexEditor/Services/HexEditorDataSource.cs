// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditorDataSource.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Thin adapter bridging HexEditor to IBinaryDataSource + IEditorNavigationCallback.
//     Wraps the HexEditor's Stream, selection, bookmarks, and custom background blocks.
//
// Architecture Notes:
//     Adapter pattern — no logic, pure delegation to HexEditor public API.
//     Created by HexEditor on file open, passed to FormatParsingService.Attach().
// ==========================================================

using System;
using System.IO;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.HexEditor.Services
{
    /// <summary>
    /// Adapts a <see cref="HexEditor"/> instance to <see cref="IBinaryDataSource"/>
    /// and <see cref="IEditorNavigationCallback"/> for the format parsing service.
    /// </summary>
    internal sealed class HexEditorDataSource : IBinaryDataSource, IEditorNavigationCallback
    {
        private readonly HexEditor _editor;

        public HexEditorDataSource(HexEditor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));

            // Wire ByteModified → DataChanged
            _editor.ByteModified += OnByteModified;
        }

        // ── IBinaryDataSource ────────────────────────────────────────────

        public string? FilePath => _editor.FileName;

        public long Length => _editor.Length;

        public bool IsReadOnly => _editor.ReadOnlyMode;

        public byte[] ReadBytes(long offset, int length)
        {
            var stream = _editor.Stream;
            if (stream == null || offset < 0 || length <= 0 || offset + length > stream.Length)
                return Array.Empty<byte>();

            var buffer = new byte[length];
            stream.Position = offset;
            int bytesRead = stream.Read(buffer, 0, length);
            if (bytesRead != length)
            {
                var result = new byte[bytesRead];
                Array.Copy(buffer, result, bytesRead);
                return result;
            }
            return buffer;
        }

        public void WriteBytes(long offset, byte[] data)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("HexEditor is in read-only mode.");

            var stream = _editor.Stream;
            if (stream == null) return;

            stream.Position = offset;
            stream.Write(data, 0, data.Length);
            stream.Flush();
            _editor.RefreshView();
        }

        public event EventHandler? DataChanged;

        private void OnByteModified(object? sender, ByteModifiedEventArgs e)
            => DataChanged?.Invoke(this, EventArgs.Empty);

        // ── IEditorNavigationCallback ────────────────────────────────────

        public void NavigateTo(long offset)
        {
            // SetPosition scrolls to make offset visible + sets cursor
            _editor.SetPosition(offset);
        }

        public void SetSelection(long start, long end)
        {
            _editor.SelectionStart = start;
            _editor.SelectionStop = end;
            _editor.RefreshView();
        }

        public void SetBookmark(long offset)
            => _editor.SetBookmark(offset);

        public void RemoveBookmark(long offset)
            => _editor.RemoveBookmark(offset);

        public void AddCustomBackgroundBlock(CustomBackgroundBlock block)
            => _editor.AddCustomBackgroundBlock(block);

        public void ClearCustomBackgroundBlocks()
            => _editor.ClearCustomBackgroundBlock();

        // ── Cleanup ──────────────────────────────────────────────────────

        /// <summary>Unwire event handlers.</summary>
        public void Dispose()
        {
            _editor.ByteModified -= OnByteModified;
        }
    }
}
