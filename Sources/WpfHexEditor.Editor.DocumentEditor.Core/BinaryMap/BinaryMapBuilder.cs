// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: BinaryMap/BinaryMapBuilder.cs
// Description:
//     Helper used by parsers to populate a BinaryMap incrementally.
//     For ZIP-based formats (DOCX/ODT), tracks entry-relative offsets and
//     flattens them to absolute ZIP offsets via RegisterZipEntry().
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;

/// <summary>
/// Fluent builder for populating a <see cref="BinaryMap"/> during parsing.
/// </summary>
public sealed class BinaryMapBuilder
{
    private readonly BinaryMap _map = new();

    // ZIP support: maps entry name → absolute offset of its data start in the ZIP file.
    private readonly Dictionary<string, long> _zipEntryDataOffsets = [];

    // ──────────────────────────────── Plain files (RTF) ───────────────────────

    /// <summary>
    /// Registers an offset/length pair for <paramref name="block"/> directly.
    /// Used by RTF parser where offsets are absolute stream positions.
    /// </summary>
    public BinaryMapBuilder Add(DocumentBlock block, long offset, int length)
    {
        _map.Add(block, offset, length);
        return this;
    }

    // ──────────────────────────────── ZIP-based files (DOCX/ODT) ─────────────

    /// <summary>
    /// Registers the absolute start offset of a ZIP entry's data payload.
    /// Must be called before any <see cref="AddZipRelative"/> call for that entry.
    /// </summary>
    /// <param name="entryName">Entry name as it appears in ZipArchive (e.g. "word/document.xml").</param>
    /// <param name="absoluteDataOffset">
    /// Absolute byte position in the ZIP file where this entry's (decompressed) data begins.
    /// Computed as: local file header start + 30 + file-name length + extra-field length.
    /// </param>
    public BinaryMapBuilder RegisterZipEntry(string entryName, long absoluteDataOffset)
    {
        _zipEntryDataOffsets[entryName] = absoluteDataOffset;
        return this;
    }

    /// <summary>
    /// Adds a block whose offset is relative to the start of a ZIP entry's data.
    /// Flattens to an absolute offset using the registered entry offset.
    /// </summary>
    public BinaryMapBuilder AddZipRelative(string entryName, DocumentBlock block,
                                           long relativeOffset, int length)
    {
        if (!_zipEntryDataOffsets.TryGetValue(entryName, out var baseOffset))
            baseOffset = 0; // fallback: treat as absolute (best-effort)

        _map.Add(block, baseOffset + relativeOffset, length);
        return this;
    }

    /// <summary>Seals the map and returns it for merging into DocumentModel.</summary>
    public BinaryMap Build()
    {
        _map.Seal();
        return _map;
    }
}
