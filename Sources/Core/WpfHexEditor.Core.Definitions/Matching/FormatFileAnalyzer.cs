//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core.Contracts;

namespace WpfHexEditor.Core.Definitions.Matching;

/// <summary>
/// High-level I/O helper that opens a file (or stream) and delegates detection
/// to <see cref="FormatMatcher"/>. Eliminates boilerplate byte-reading in consumer code.
/// </summary>
public static class FormatFileAnalyzer
{
    /// <summary>
    /// Number of header bytes read from disk for magic-byte detection.
    /// 512 bytes is sufficient for all formats in the embedded catalog.
    /// </summary>
    public const int DefaultHeaderSize = 512;

    // ------------------------------------------------------------------
    // Synchronous surface
    // ------------------------------------------------------------------

    /// <summary>
    /// Identifies the format of <paramref name="filePath"/> using extension +
    /// magic-byte detection.
    /// </summary>
    /// <param name="catalog">The catalog to query.</param>
    /// <param name="filePath">Absolute or relative path to the file.</param>
    /// <param name="headerSize">Number of header bytes to read. Default: 512.</param>
    /// <returns>Best match, or <see langword="null"/> if unrecognised.</returns>
    public static FormatMatchResult? Analyze(
        IEmbeddedFormatCatalog catalog,
        string filePath,
        int headerSize = DefaultHeaderSize)
    {
        var header = ReadHeader(filePath, headerSize);
        return FormatMatcher.Match(catalog, filePath, header);
    }

    /// <summary>
    /// Identifies the format from a <see cref="FileInfo"/> instance.
    /// </summary>
    public static FormatMatchResult? Analyze(
        IEmbeddedFormatCatalog catalog,
        FileInfo file,
        int headerSize = DefaultHeaderSize)
        => Analyze(catalog, file.FullName, headerSize);

    /// <summary>
    /// Identifies the format from a <see cref="Stream"/>.
    /// The stream position is reset to its original value after reading.
    /// </summary>
    /// <param name="catalog">The catalog to query.</param>
    /// <param name="stream">Readable stream positioned at (or seeked to) the file start.</param>
    /// <param name="extension">
    /// Optional file extension (with or without leading dot).
    /// Improves accuracy when combined with magic-byte detection.
    /// </param>
    /// <param name="headerSize">Number of bytes to read from the stream.</param>
    public static FormatMatchResult? Analyze(
        IEmbeddedFormatCatalog catalog,
        Stream stream,
        string? extension = null,
        int headerSize = DefaultHeaderSize)
    {
        var header = ReadHeader(stream, headerSize);
        return FormatMatcher.Match(catalog, extension, header);
    }

    /// <summary>
    /// Identifies the format from raw bytes already in memory.
    /// </summary>
    /// <param name="catalog">The catalog to query.</param>
    /// <param name="data">File bytes (or at minimum the first 512 bytes).</param>
    /// <param name="extension">Optional file extension hint.</param>
    public static FormatMatchResult? Analyze(
        IEmbeddedFormatCatalog catalog,
        ReadOnlyMemory<byte> data,
        string? extension = null)
        => FormatMatcher.Match(catalog, extension, data.Span);

    // ------------------------------------------------------------------
    // Asynchronous surface
    // ------------------------------------------------------------------

    /// <summary>
    /// Asynchronously identifies the format of <paramref name="filePath"/>.
    /// </summary>
    public static async Task<FormatMatchResult?> AnalyzeAsync(
        IEmbeddedFormatCatalog catalog,
        string filePath,
        int headerSize = DefaultHeaderSize,
        CancellationToken cancellationToken = default)
    {
        var header = await ReadHeaderAsync(filePath, headerSize, cancellationToken).ConfigureAwait(false);
        return FormatMatcher.Match(catalog, Path.GetExtension(filePath), header.Span);
    }

    /// <summary>
    /// Asynchronously identifies the format from a <see cref="FileInfo"/> instance.
    /// </summary>
    public static Task<FormatMatchResult?> AnalyzeAsync(
        IEmbeddedFormatCatalog catalog,
        FileInfo file,
        int headerSize = DefaultHeaderSize,
        CancellationToken cancellationToken = default)
        => AnalyzeAsync(catalog, file.FullName, headerSize, cancellationToken);

    /// <summary>
    /// Asynchronously identifies the format from a <see cref="Stream"/>.
    /// </summary>
    public static async Task<FormatMatchResult?> AnalyzeAsync(
        IEmbeddedFormatCatalog catalog,
        Stream stream,
        string? extension = null,
        int headerSize = DefaultHeaderSize,
        CancellationToken cancellationToken = default)
    {
        var header = await ReadHeaderAsync(stream, headerSize, cancellationToken).ConfigureAwait(false);
        return FormatMatcher.Match(catalog, extension, header.Span);
    }

    // ------------------------------------------------------------------
    // Batch helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Analyses all files in a directory (optionally recursive) and returns
    /// a result per file. Files that cannot be opened are silently skipped.
    /// </summary>
    /// <param name="catalog">The catalog to query.</param>
    /// <param name="directory">Directory to scan.</param>
    /// <param name="searchPattern">Glob pattern, e.g. <c>"*.*"</c>.</param>
    /// <param name="recursive">When true, recurses into subdirectories.</param>
    /// <param name="headerSize">Header bytes to read per file.</param>
    public static IEnumerable<(string Path, FormatMatchResult? Match)> AnalyzeDirectory(
        IEmbeddedFormatCatalog catalog,
        string directory,
        string searchPattern = "*.*",
        bool recursive = false,
        int headerSize = DefaultHeaderSize)
    {
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var file in Directory.EnumerateFiles(directory, searchPattern, option))
        {
            FormatMatchResult? result = null;
            try { result = Analyze(catalog, file, headerSize); }
            catch { /* skip locked / inaccessible files */ }
            yield return (file, result);
        }
    }

    // ------------------------------------------------------------------
    // Private I/O helpers
    // ------------------------------------------------------------------

    private static ReadOnlySpan<byte> ReadHeader(string filePath, int size)
    {
        var buf = new byte[size];
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        int read = fs.Read(buf, 0, size);
        return buf.AsSpan(0, read);
    }

    private static ReadOnlySpan<byte> ReadHeader(Stream stream, int size)
    {
        long origin = stream.CanSeek ? stream.Position : -1;
        var buf = new byte[size];
        int read = stream.Read(buf, 0, size);
        if (stream.CanSeek && origin >= 0) stream.Seek(origin, SeekOrigin.Begin);
        return buf.AsSpan(0, read);
    }

    private static async Task<ReadOnlyMemory<byte>> ReadHeaderAsync(
        string filePath, int size, CancellationToken ct)
    {
        var buf = new byte[size];
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);
        int read = await fs.ReadAsync(buf.AsMemory(0, size), ct).ConfigureAwait(false);
        return buf.AsMemory(0, read);
    }

    private static async Task<ReadOnlyMemory<byte>> ReadHeaderAsync(
        Stream stream, int size, CancellationToken ct)
    {
        long origin = stream.CanSeek ? stream.Position : -1;
        var buf = new byte[size];
        int read = await stream.ReadAsync(buf.AsMemory(0, size), ct).ConfigureAwait(false);
        if (stream.CanSeek && origin >= 0) stream.Seek(origin, SeekOrigin.Begin);
        return buf.AsMemory(0, read);
    }
}
