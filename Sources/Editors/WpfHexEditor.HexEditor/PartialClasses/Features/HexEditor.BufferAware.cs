// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.BufferAware.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Partial class implementing IBufferAwareEditor so the HexEditor
//     participates in the shared DocumentBuffer infrastructure (ADR-DOC-02).
//
// Architecture Notes:
//     Pattern: Opt-in buffer sync (ADR-DOC-01)
//     - Only files ≤ MaxBufferSyncBytes (10 MB) are synced as text to avoid
//       pushing multi-megabyte binary content on every keystroke.
//     - Writes to the buffer are debounced (300 ms) via DispatcherTimer so that
//       rapid byte edits produce one buffer update, not one per keypress.
//     - _suppressBufferSync guard prevents feedback loops (same pattern as
//       CodeEditor and TextEditor).
//     - FileOpened re-attaches the buffer content on file switch so a reused
//       tab always reflects the current file.
// ==========================================================

using System;
using System.IO;
using System.Text;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Documents;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — IBufferAwareEditor implementation.
    /// Opt-in bridge between the hex engine and the shared <see cref="IDocumentBuffer"/>.
    /// </summary>
    public partial class HexEditor : IBufferAwareEditor
    {
        // ── Maximum file size for text sync (10 MB) ──────────────────────────
        private const long MaxBufferSyncBytes = 10 * 1024 * 1024;

        // ── Buffer state ─────────────────────────────────────────────────────
        private IDocumentBuffer? _buffer;
        private bool             _suppressBufferSync;

        // ── Debounce timer (300 ms idle → push to buffer) ────────────────────
        private DispatcherTimer? _bufferSyncTimer;

        // ── IBufferAwareEditor ───────────────────────────────────────────────

        /// <inheritdoc />
        void IBufferAwareEditor.AttachBuffer(IDocumentBuffer buffer)
        {
            if (_buffer is not null)
                ((IBufferAwareEditor)this).DetachBuffer();

            _buffer = buffer;

            // Initialise the debounce timer.
            _bufferSyncTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(300),
            };
            _bufferSyncTimer.Tick += OnBufferSyncTimerTick;

            // Subscribe to byte-level edits and file-open events.
            ByteModified += OnByteModifiedForBuffer;
            FileOpened   += OnFileOpenedForBuffer;

            // Subscribe to external buffer changes (e.g. CodeEditor editing same file).
            _buffer.Changed += OnBufferChangedExternally;

            // Push current content into the buffer immediately.
            PushContentToBuffer();
        }

        /// <inheritdoc />
        void IBufferAwareEditor.DetachBuffer()
        {
            _bufferSyncTimer?.Stop();
            if (_bufferSyncTimer is not null)
            {
                _bufferSyncTimer.Tick -= OnBufferSyncTimerTick;
                _bufferSyncTimer = null;
            }

            ByteModified -= OnByteModifiedForBuffer;
            FileOpened   -= OnFileOpenedForBuffer;

            if (_buffer is not null)
            {
                _buffer.Changed -= OnBufferChangedExternally;
                _buffer = null;
            }
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnByteModifiedForBuffer(object? sender, Core.Events.ByteModifiedEventArgs e)
        {
            if (_suppressBufferSync) return;
            // Restart the debounce window.
            _bufferSyncTimer?.Stop();
            _bufferSyncTimer?.Start();
        }

        private void OnFileOpenedForBuffer(object? sender, EventArgs e)
        {
            if (_suppressBufferSync) return;
            // File changed while buffer is attached — push new content immediately.
            _bufferSyncTimer?.Stop();
            PushContentToBuffer();
        }

        private void OnBufferSyncTimerTick(object? sender, EventArgs e)
        {
            _bufferSyncTimer?.Stop();
            PushContentToBuffer();
        }

        /// <summary>
        /// Receives external changes (e.g. from CodeEditor on the same file).
        /// Reloads the hex editor from the new text bytes.
        /// </summary>
        private void OnBufferChangedExternally(object? sender, DocumentBufferChangedEventArgs e)
        {
            // Ignore changes we originated ourselves.
            if (ReferenceEquals(e.Source, this)) return;
            if (_suppressBufferSync) return;
            if (string.IsNullOrEmpty(e.NewText)) return;

            _suppressBufferSync = true;
            try
            {
                var newBytes = Encoding.UTF8.GetBytes(e.NewText);
                var ms       = new MemoryStream(newBytes, writable: false);
                // OpenStream replaces the backing provider with the memory stream content.
                OpenStream(ms);
            }
            finally
            {
                _suppressBufferSync = false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Reads the current backing stream and pushes its UTF-8 text representation
        /// to the shared buffer.  Files larger than <see cref="MaxBufferSyncBytes"/>
        /// are skipped to avoid copying multi-megabyte binaries unnecessarily.
        /// </summary>
        private void PushContentToBuffer()
        {
            if (_buffer is null) return;
            if (_suppressBufferSync) return;

            var stream = Stream;
            if (stream is null || !stream.CanRead) return;

            // Skip very large files.
            long length;
            try { length = stream.Length; }
            catch { return; }
            if (length > MaxBufferSyncBytes) return;

            string text;
            try
            {
                var savedPos = stream.CanSeek ? stream.Position : -1L;
                if (stream.CanSeek) stream.Position = 0;

                using var reader = new StreamReader(stream,
                    encoding: Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 4096,
                    leaveOpen: true);
                text = reader.ReadToEnd();

                if (stream.CanSeek && savedPos >= 0)
                    stream.Position = savedPos;
            }
            catch
            {
                return;
            }

            _suppressBufferSync = true;
            try
            {
                _buffer.SetText(text, source: this);
            }
            finally
            {
                _suppressBufferSync = false;
            }
        }
    }
}
