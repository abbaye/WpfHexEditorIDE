//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Contracts;

/// <summary>
/// Well-known format categories available in the embedded catalog.
/// Use with <see cref="IEmbeddedFormatCatalog.GetByCategory(FormatCategory)"/> for
/// compile-time safety and IntelliSense discoverability.
/// </summary>
public enum FormatCategory
{
    Other,
    Archives,
    Audio,
    CAD,
    Certificates,
    Crypto,
    Data,
    Database,
    Disk,
    Documents,
    Executables,
    Firmware,
    Fonts,
    GIS,
    Game,
    Images,
    MachineLearning,
    Medical,
    Network,
    Programming,
    RomHacking,
    Science,
    Subtitles,
    Synalysis,
    System,
    Text,
    Video,
    // ReSharper disable once InconsistentNaming
    _3D,
}

/// <summary>
/// Well-known JSON schema names bundled in the embedded catalog assembly.
/// Use with <see cref="IEmbeddedFormatCatalog.GetSchemaJson(SchemaName)"/> for
/// compile-time safety and IntelliSense discoverability.
/// </summary>
public enum SchemaName
{
    /// <summary>The .whfmt file format schema — use to validate your own format definitions.</summary>
    Whfmt,
    /// <summary>The .whcd class-diagram visual state schema.</summary>
    Whcd,
    /// <summary>The .whdbg debug launch configuration schema.</summary>
    Whdbg,
    /// <summary>The .whidews workspace archive schema.</summary>
    Whidews,
    /// <summary>The .whscd solution-wide class diagram schema.</summary>
    Whscd,
}

/// <summary>A single magic-byte signature from a .whfmt detection block.</summary>
public sealed record FormatSignature(
    /// <summary>Hex string of the expected bytes, e.g. "504B0304".</summary>
    string Value,
    /// <summary>Byte offset in the file where the signature appears.</summary>
    int Offset,
    /// <summary>Match confidence weight (0.0–1.0).</summary>
    double Weight);

/// <summary>
/// Lightweight summary of a single embedded format definition.
/// </summary>
public sealed record EmbeddedFormatEntry(
    /// <summary>
    /// Assembly manifest resource key (used to load the full JSON).
    /// </summary>
    string ResourceKey,
    /// <summary>
    /// Human-readable format name, e.g. "ZIP Archive".
    /// </summary>
    string Name,
    /// <summary>
    /// Logical category, e.g. "Archives", "Images".
    /// </summary>
    string Category,
    /// <summary>
    /// Short description of the format.
    /// </summary>
    string Description,
    /// <summary>
    /// File extensions associated with this format, e.g. [".zip", ".jar"].
    /// </summary>
    IReadOnlyList<string> Extensions,
    /// <summary>
    /// 0-100 completeness score from the format's QualityMetrics.
    /// </summary>
    int QualityScore,
    /// <summary>
    /// Format specification version, e.g. "2.0". Empty string if not specified.
    /// </summary>
    string Version,
    /// <summary>
    /// Author or authoring organization. Empty string if not specified.
    /// </summary>
    string Author,
    /// <summary>
    /// Target platform extracted from <c>TechnicalDetails.Platform</c> in the
    /// .whfmt file, e.g. "Nintendo Entertainment System", "SNES".
    /// Empty string when the format is not platform-specific.
    /// </summary>
    string Platform,
    /// <summary>
    /// Preferred editor factory ID declared in the <c>preferredEditor</c> field of the
    /// .whfmt file. Null when not declared.
    /// Typical values: "code-editor", "structure-editor", "hex-editor".
    /// </summary>
    string? PreferredEditor,
    /// <summary>
    /// Whether the format is text-based, from <c>detection.isTextFormat</c>.
    /// Used as a fallback to derive a preferred editor when <see cref="PreferredEditor"/>
    /// is null: <c>true</c> → "code-editor".
    /// </summary>
    bool IsTextFormat,
    /// <summary>
    /// Whether the .whfmt file contains a <c>syntaxDefinition</c> block that can be
    /// parsed into a LanguageDefinition.
    /// </summary>
    bool HasSyntaxDefinition = false,
    /// <summary>
    /// Preferred diff algorithm declared in the .whfmt root field <c>"diffMode"</c>.
    /// Values: <c>"text"</c>, <c>"semantic"</c>, <c>"binary"</c>. Null when absent.
    /// </summary>
    string? DiffMode = null,
    /// <summary>MIME types declared in the .whfmt file (e.g. ["application/zip"]).</summary>
    IReadOnlyList<string>? MimeTypes = null,
    /// <summary>Magic byte signatures for binary detection. Each entry: hex value + byte offset.</summary>
    IReadOnlyList<FormatSignature>? Signatures = null);

/// <summary>
/// Read-only catalog of the embedded format definitions shipped with the assembly.
/// <para>
/// Implemented by <c>EmbeddedFormatCatalog</c> in <c>WpfHexEditor.Core.Definitions</c>.
/// Obtain the singleton via the static property on that class.
/// </para>
/// </summary>
public interface IEmbeddedFormatCatalog
{
    /// <summary>
    /// Returns all embedded format entries (lazy-loaded on first call).
    /// </summary>
    IReadOnlySet<EmbeddedFormatEntry> GetAll();

    /// <summary>
    /// Returns all distinct category names sorted alphabetically.
    /// </summary>
    IReadOnlySet<string> GetCategories();

    /// <summary>
    /// Returns the full JSON content for the given resource key.
    /// </summary>
    string GetJson(string resourceKey);

    /// <summary>
    /// Returns the first entry whose <see cref="EmbeddedFormatEntry.Extensions"/> list contains
    /// <paramref name="extension"/> (case-insensitive, leading dot optional).
    /// Returns <c>null</c> if no matching format is registered.
    /// </summary>
    EmbeddedFormatEntry? GetByExtension(string extension);

    /// <summary>
    /// Returns the set of editor factory IDs that are semantically compatible with
    /// <paramref name="filePath"/> based on its whfmt format entry.
    /// <para>
    /// <c>"hex-editor"</c> is always included — the hex editor is a universal fallback.
    /// Returns an empty list when the format is not registered in the catalog;
    /// callers should fall back to <see cref="IEditorFactory.CanOpen"/> in that case.
    /// </para>
    /// </summary>
    IReadOnlyList<string> GetCompatibleEditorIds(string filePath);

    /// <summary>
    /// Returns all entries in the given category (case-insensitive).
    /// Returns an empty list when the category is not registered.
    /// </summary>
    IReadOnlyList<EmbeddedFormatEntry> GetByCategory(string category);

    /// <summary>
    /// Detects the file format by matching magic-byte signatures against the
    /// provided file header bytes. Returns the best-scoring match, or null.
    /// <para>Pass at least the first 16 bytes for reliable detection;
    /// 512 bytes recommended for formats with late signatures.</para>
    /// </summary>
    EmbeddedFormatEntry? DetectFromBytes(ReadOnlySpan<byte> header);

    /// <summary>
    /// Returns the first entry whose <see cref="EmbeddedFormatEntry.MimeTypes"/> list
    /// contains <paramref name="mimeType"/> (case-insensitive).
    /// Returns null when not found.
    /// </summary>
    EmbeddedFormatEntry? GetByMimeType(string mimeType);

    /// <summary>
    /// Returns the embedded JSON schema for the given schema name (e.g. "whfmt", "whcd").
    /// Returns null when the schema is not found.
    /// </summary>
    string? GetSchemaJson(string schemaName);

    /// <summary>
    /// Returns all entries in the given category.
    /// Prefer this overload for compile-time safety and IntelliSense discoverability.
    /// </summary>
    IReadOnlyList<EmbeddedFormatEntry> GetByCategory(FormatCategory category)
        => GetByCategory(category == FormatCategory._3D ? "3D" : category.ToString());

    /// <summary>
    /// Returns the embedded JSON schema for the given well-known schema name.
    /// Prefer this overload for compile-time safety and IntelliSense discoverability.
    /// </summary>
    string? GetSchemaJson(SchemaName schema)
        => GetSchemaJson(schema.ToString().ToLowerInvariant());
}
