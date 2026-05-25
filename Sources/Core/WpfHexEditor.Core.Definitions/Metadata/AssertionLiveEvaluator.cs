// ==========================================================
// Project: WpfHexEditor.Core.Definitions
// File: Metadata/AssertionLiveEvaluator.cs
// Description: P12 — populates a WhfmtVariableStore from binary header bytes and
//              delegates assertion evaluation to FormatAssertionEvaluator.EvaluateAll.
//              Bridges the gap between having assertions declared and actually
//              evaluating them against a file open in the IDE.
// Architecture: Pure function — no I/O, no UI dependency. The caller supplies
//               the byte[] header; this layer only does store-population + eval.
// ==========================================================

using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions.Models;

namespace WpfHexEditor.Core.Definitions.Metadata;

/// <summary>
/// Populates a <see cref="WhfmtVariableStore"/> from raw binary header bytes
/// and evaluates the assertions of a format entry against them.
/// </summary>
public static class AssertionLiveEvaluator
{
    /// <summary>Number of bytes read from the file for header-variable population.</summary>
    public const int DefaultHeaderSize = 512;

    /// <summary>
    /// Builds a variable store from <paramref name="header"/>, then evaluates all assertions
    /// declared by <paramref name="entry"/>. Returns one result per assertion in catalog order.
    /// </summary>
    /// <param name="entry">The format entry whose assertions are evaluated.</param>
    /// <param name="catalog">Catalog used to load the assertion rules via <c>GetAssertions()</c>.</param>
    /// <param name="header">First N bytes of the open file (typically 512 B; clamped internally).</param>
    /// <param name="fileSize">Total file size in bytes (exposed as <c>file_size</c> variable).</param>
    public static IReadOnlyList<AssertionResult> Evaluate(
        EmbeddedFormatEntry    entry,
        IEmbeddedFormatCatalog catalog,
        ReadOnlySpan<byte>     header,
        long                   fileSize)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(catalog);

        var store = BuildStore(header, fileSize);
        return FormatAssertionEvaluator.EvaluateAll(entry, catalog, store);
    }

    // ── Store population ────────────────────────────────────────────────────

    private static WhfmtVariableStore BuildStore(ReadOnlySpan<byte> header, long fileSize)
    {
        var store = new WhfmtVariableStore();

        // Universal variables always available in assertions
        store.Set("file_size", fileSize);

        // Magic bytes at common offsets as hex strings (e.g. magic_0 = "504B0304")
        PopulateMagicVariables(store, header);

        // First 16 bytes as individual uint8 variables: byte_0 .. byte_15
        int limit = Math.Min(16, header.Length);
        for (int i = 0; i < limit; i++)
            store.Set($"byte_{i}", (long)header[i]);

        // File size convenience ranges
        store.Set("file_size_kb", fileSize / 1024L);
        store.Set("file_size_mb", fileSize / (1024L * 1024L));

        return store;
    }

    private static void PopulateMagicVariables(WhfmtVariableStore store, ReadOnlySpan<byte> header)
    {
        // Expose magic bytes at offset 0, 4, 8 as hex strings for expressions like:
        //   magic_0 == "504B0304"   (ZIP)
        //   magic_0 == "89504E47"   (PNG)
        int[] offsets = [0, 4, 8];
        foreach (int off in offsets)
        {
            if (off + 4 <= header.Length)
            {
                string hex = $"{header[off]:X2}{header[off+1]:X2}{header[off+2]:X2}{header[off+3]:X2}";
                store.Set($"magic_{off}", hex);
            }
        }

        // Also expose first 2 bytes as magic_word for 2-byte signatures
        if (header.Length >= 2)
            store.Set("magic_word", $"{header[0]:X2}{header[1]:X2}");
    }
}
