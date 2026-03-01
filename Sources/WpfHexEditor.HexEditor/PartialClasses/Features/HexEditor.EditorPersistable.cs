//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using WpfHexEditor.Core.Models;
using WpfHexEditor.Core.RomHacking;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — IEditorPersistable implementation.
    /// Allows the project system to serialise/restore editor state
    /// (bytes-per-line, edit mode, selection, encoding …) per project item
    /// so the user gets back the exact view they left.
    /// </summary>
    public partial class HexEditor : IEditorPersistable
    {
        // ── IEditorPersistable ─────────────────────────────────────────────

        /// <inheritdoc />
        public EditorConfigDto GetEditorConfig()
        {
            return new EditorConfigDto
            {
                BytesPerLine    = BytePerLine,
                EditMode        = EditMode.ToString(),
                SelectionStart  = SelectionStart,
                SelectionLength = SelectionLength,
                Encoding        = CustomEncoding?.WebName,
                // ScrollOffset: HexEditor exposes no public scroll-line accessor;
                // stored as 0 and scroll restoration is skipped for now.
                ScrollOffset    = 0,
            };
        }

        /// <inheritdoc />
        public void ApplyEditorConfig(EditorConfigDto config)
        {
            if (config == null) return;

            if (config.BytesPerLine > 0)
                BytePerLine = config.BytesPerLine;

            if (config.EditMode != null &&
                Enum.TryParse<EditMode>(config.EditMode, out var em))
                EditMode = em;

            if (config.SelectionStart >= 0)
            {
                SelectionStart = config.SelectionStart;
                // SelectionStop = SelectionStart + length - 1 (length ≥ 1)
                var length = Math.Max(1, config.SelectionLength);
                SelectionStop  = config.SelectionStart + length - 1;
            }

            if (config.Encoding != null)
            {
                try
                {
                    CustomEncoding = System.Text.Encoding.GetEncoding(config.Encoding);
                }
                catch
                {
                    // Encoding name not recognised — keep default
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Serialises all in-memory byte-level changes as a compact IPS patch.
        /// Returns <see langword="null"/> when the buffer is clean.
        /// </remarks>
        public byte[]? GetUnsavedModifications()
        {
            if (!IsModified || !IsFileOrStreamLoaded)
                return null;

            try
            {
                var original = GetAllBytes(copyChange: false);
                var modified = GetAllBytes(copyChange: true);
                return IPSPatcher.CreatePatch(original, modified);
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Re-applies an IPS patch (previously returned by <see cref="GetUnsavedModifications"/>)
        /// on top of the currently loaded file bytes.
        /// </remarks>
        public void ApplyUnsavedModifications(byte[] data)
        {
            if (data == null || data.Length == 0 || !IsFileOrStreamLoaded)
                return;

            try
            {
                var baseData = GetAllBytes(copyChange: false);
                var result   = IPSPatcher.ApplyPatchFromBytes(ref baseData, data);
                if (result.Success)
                    OpenMemory(baseData);
            }
            catch
            {
                // Silently ignore — the editor stays on the clean file
            }
        }

        // ── IEditorPersistable — Bookmarks ────────────────────────────────────
        // Explicit interface implementation to avoid collision with the existing
        // public long[] GetBookmarks() method in HexEditor.Bookmarks.cs.

        /// <inheritdoc />
        IReadOnlyList<BookmarkDto>? IEditorPersistable.GetBookmarks()
        {
            if (_bookmarks.Count == 0)
                return null;

            var result = new List<BookmarkDto>(_bookmarks.Count);
            foreach (var offset in _bookmarks)
                result.Add(new BookmarkDto { Offset = offset, Length = 1, Label = "" });
            return result;
        }

        /// <inheritdoc />
        void IEditorPersistable.ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks)
        {
            if (bookmarks == null) return;
            foreach (var dto in bookmarks)
                SetBookmark(dto.Offset);
        }
    }
}
