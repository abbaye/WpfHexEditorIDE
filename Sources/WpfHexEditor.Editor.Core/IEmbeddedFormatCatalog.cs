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
    string Author);

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
