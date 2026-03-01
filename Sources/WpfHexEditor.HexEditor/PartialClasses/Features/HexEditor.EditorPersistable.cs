//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using WpfHexEditor.Core.Models;
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
        /// Returns <see langword="null"/> when the editor buffer is clean (unmodified).
        /// Full byte-level modification patch serialisation is deferred to a future sprint.
        /// </remarks>
        public byte[]? GetUnsavedModifications() => IsModified ? [] : null;

        /// <inheritdoc />
        public void ApplyUnsavedModifications(byte[] data)
        {
            // Placeholder — no-op until GetUnsavedModifications is fully implemented.
        }
    }
}
