//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

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
    bool IsTextFormat);

/// <summary>
/// Read-only catalog of the embedded format definitions shipped with the assembly.
/// <para>
/// Implemented by <c>EmbeddedFormatCatalog</c> in <c>WpfHexEditor.Core</c>.
/// Obtain the singleton via the static property on that class.
/// </para>
/// </summary>
public interface IEmbeddedFormatCatalog
{
    /// <summary>
    /// Returns all embedded format entries (lazy-loaded on first call).
    /// </summary>
    IReadOnlyList<EmbeddedFormatEntry> GetAll();

    /// <summary>
    /// Returns all distinct category names sorted alphabetically.
    /// </summary>
    IReadOnlyList<string> GetCategories();

    /// <summary>
    /// Returns the full JSON content for the given resource key.
    /// </summary>
    string GetJson(string resourceKey);
}
