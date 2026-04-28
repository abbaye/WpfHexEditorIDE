//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core.Contracts;

namespace WpfHexEditor.Core.Definitions.Query;

/// <summary>
/// Fluent query builder for <see cref="IEmbeddedFormatCatalog"/>.
/// <para>
/// Obtain an instance via the <see cref="CatalogQueryExtensions.Query"/> extension method:
/// <code>
/// var results = EmbeddedFormatCatalog.Instance
///     .Query()
///     .InCategory(FormatCategory.Disk)
///     .WithMinQuality(80)
///     .HasMagicBytes()
///     .OrderByQuality()
///     .Execute();
/// </code>
/// </para>
/// </summary>
public sealed class CatalogQuery
{
    private readonly IEmbeddedFormatCatalog _catalog;
    private readonly List<Func<EmbeddedFormatEntry, bool>> _predicates = [];
    private Func<IEnumerable<EmbeddedFormatEntry>, IEnumerable<EmbeddedFormatEntry>>? _order;

    internal CatalogQuery(IEmbeddedFormatCatalog catalog) => _catalog = catalog;

    // ------------------------------------------------------------------
    // Category filters
    // ------------------------------------------------------------------

    /// <summary>Restricts results to a single category (enum, compile-time safe).</summary>
    public CatalogQuery InCategory(FormatCategory category)
    {
        var categoryStr = category == FormatCategory._3D ? "3D" : category.ToString();
        return Where(e => e.Category.Equals(categoryStr, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Restricts results to a single category (string).</summary>
    public CatalogQuery InCategory(string category)
        => Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------
    // Quality filters
    // ------------------------------------------------------------------

    /// <summary>Keeps only entries with <see cref="EmbeddedFormatEntry.QualityScore"/> ≥ <paramref name="minimum"/>.</summary>
    public CatalogQuery WithMinQuality(int minimum)
        => Where(e => e.QualityScore >= minimum);

    /// <summary>Keeps only entries flagged as priority formats (QualityScore ≥ 85).</summary>
    public CatalogQuery PriorityOnly()
        => WithMinQuality(85);

    // ------------------------------------------------------------------
    // Detection filters
    // ------------------------------------------------------------------

    /// <summary>Keeps only entries that have at least one magic-byte signature.</summary>
    public CatalogQuery HasMagicBytes()
        => Where(e => e.Signatures is { Count: > 0 });

    /// <summary>Keeps only entries that match a given file extension.</summary>
    public CatalogQuery WithExtension(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : '.' + extension;
        return Where(e => e.Extensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Keeps only text-based formats.</summary>
    public CatalogQuery TextFormatsOnly()
        => Where(e => e.IsTextFormat);

    /// <summary>Keeps only binary formats.</summary>
    public CatalogQuery BinaryFormatsOnly()
        => Where(e => !e.IsTextFormat);

    // ------------------------------------------------------------------
    // Metadata filters
    // ------------------------------------------------------------------

    /// <summary>Keeps only entries that include a <c>syntaxDefinition</c> block.</summary>
    public CatalogQuery HasSyntaxDefinition()
        => Where(e => e.HasSyntaxDefinition);

    /// <summary>
    /// Keeps only entries whose preferred editor matches <paramref name="editorId"/>
    /// (e.g. <c>"code-editor"</c>, <c>"hex-editor"</c>).
    /// </summary>
    public CatalogQuery WithPreferredEditor(string editorId)
        => Where(e => e.PreferredEditor?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>Keeps only entries that declare one or more MIME types.</summary>
    public CatalogQuery HasMimeType()
        => Where(e => e.MimeTypes is { Count: > 0 });

    /// <summary>Keeps only entries that target a specific platform (e.g. <c>"Nintendo"</c>).</summary>
    public CatalogQuery ForPlatform(string platform)
        => Where(e => e.Platform.Contains(platform, StringComparison.OrdinalIgnoreCase));

    /// <summary>Keeps only entries that declare a non-empty platform.</summary>
    public CatalogQuery HasPlatform()
        => Where(e => !string.IsNullOrWhiteSpace(e.Platform));

    /// <summary>
    /// Keeps only entries whose preferred diff mode matches
    /// (e.g. <c>"text"</c>, <c>"binary"</c>, <c>"semantic"</c>).
    /// </summary>
    public CatalogQuery WithDiffMode(string diffMode)
        => Where(e => e.DiffMode?.Equals(diffMode, StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>
    /// Keeps only entries whose <c>formatId</c> matches <paramref name="formatId"/>
    /// (case-insensitive, e.g. <c>"ZIP"</c>, <c>"APFS"</c>).
    /// <para>
    /// Note: the entry name is derived from the resource key stem (the whfmt filename).
    /// </para>
    /// </summary>
    public CatalogQuery WithName(string name)
        => Where(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------
    // Full-text search
    // ------------------------------------------------------------------

    /// <summary>
    /// Keeps only entries whose name or description contains <paramref name="term"/>
    /// (case-insensitive).
    /// </summary>
    public CatalogQuery Containing(string term)
        => Where(e =>
            e.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            e.Description.Contains(term, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------
    // Ordering
    // ------------------------------------------------------------------

    /// <summary>Orders results by quality score descending (highest first).</summary>
    public CatalogQuery OrderByQuality()
    {
        _order = entries => entries.OrderByDescending(e => e.QualityScore);
        return this;
    }

    /// <summary>Orders results alphabetically by name.</summary>
    public CatalogQuery OrderByName()
    {
        _order = entries => entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
        return this;
    }

    /// <summary>Orders results by category then name.</summary>
    public CatalogQuery OrderByCategoryThenName()
    {
        _order = entries => entries
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
        return this;
    }

    // ------------------------------------------------------------------
    // Custom predicate
    // ------------------------------------------------------------------

    /// <summary>Appends a custom filter predicate.</summary>
    public CatalogQuery Where(Func<EmbeddedFormatEntry, bool> predicate)
    {
        _predicates.Add(predicate);
        return this;
    }

    // ------------------------------------------------------------------
    // Terminal operations
    // ------------------------------------------------------------------

    /// <summary>
    /// Executes the query and returns the matching entries.
    /// </summary>
    public IReadOnlyList<EmbeddedFormatEntry> Execute()
        => BuildQuery().ToList();

    /// <summary>
    /// Executes the query and returns only the first match, or <see langword="null"/>.
    /// </summary>
    public EmbeddedFormatEntry? First()
        => BuildQuery().FirstOrDefault();

    /// <summary>Returns the count of entries that match the query.</summary>
    public int Count()
        => BuildQuery().Count();

    /// <summary>Returns <see langword="true"/> when at least one entry matches the query.</summary>
    public bool Any()
        => BuildQuery().Any();

    /// <summary>
    /// Projects each matching entry using <paramref name="selector"/> and returns the results.
    /// </summary>
    public IReadOnlyList<TResult> Select<TResult>(Func<EmbeddedFormatEntry, TResult> selector)
        => BuildQuery().Select(selector).ToList();

    /// <summary>
    /// Builds a <see cref="Dictionary{TKey,TValue}"/> from the matching entries.
    /// </summary>
    /// <param name="keySelector">Produces the dictionary key from each entry.</param>
    /// <param name="elementSelector">Produces the dictionary value. Defaults to the entry itself.</param>
    /// <param name="comparer">Optional key comparer. Defaults to <see cref="StringComparer.OrdinalIgnoreCase"/> when <typeparamref name="TKey"/> is <see langword="string"/>.</param>
    public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
        Func<EmbeddedFormatEntry, TKey> keySelector,
        Func<EmbeddedFormatEntry, TValue> elementSelector,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var effectiveComparer = comparer
            ?? (typeof(TKey) == typeof(string)
                ? (IEqualityComparer<TKey>)(object)StringComparer.OrdinalIgnoreCase
                : EqualityComparer<TKey>.Default);

        var dict = new Dictionary<TKey, TValue>(effectiveComparer);
        foreach (var entry in BuildQuery())
        {
            var key = keySelector(entry);
            dict.TryAdd(key, elementSelector(entry));
        }
        return dict;
    }

    /// <summary>
    /// Builds a flat extension→entry dictionary from the matching entries.
    /// When multiple entries share the same extension, the first encountered wins.
    /// </summary>
    public Dictionary<string, EmbeddedFormatEntry> ToExtensionDictionary()
    {
        var dict = new Dictionary<string, EmbeddedFormatEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in BuildQuery())
            foreach (var ext in entry.Extensions)
            {
                var key = ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
                dict.TryAdd(key, entry);
            }
        return dict;
    }

    /// <summary>
    /// Builds an extension→<typeparamref name="TValue"/> dictionary using a value selector.
    /// Useful for building routing maps (e.g. extension → preferred editor).
    /// When multiple entries share the same extension, the first encountered wins.
    /// </summary>
    /// <example>
    /// <code>
    /// var editorMap = catalog.Query()
    ///     .Where(e => e.PreferredEditor is not null)
    ///     .ToExtensionDictionary(e => e.PreferredEditor!);
    /// </code>
    /// </example>
    public Dictionary<string, TValue> ToExtensionDictionary<TValue>(
        Func<EmbeddedFormatEntry, TValue> valueSelector)
    {
        var dict = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in BuildQuery())
            foreach (var ext in entry.Extensions)
            {
                var key = ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
                dict.TryAdd(key, valueSelector(entry));
            }
        return dict;
    }

    /// <summary>
    /// Groups the matching entries by category.
    /// Returns a dictionary keyed by category name (case-insensitive).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<EmbeddedFormatEntry>> GroupByCategory()
    {
        var result = new Dictionary<string, IReadOnlyList<EmbeddedFormatEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in BuildQuery()
            .GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            result[group.Key] = group.ToList();
        }
        return result;
    }

    // ------------------------------------------------------------------
    // Private — single pipeline builder
    // ------------------------------------------------------------------

    private IEnumerable<EmbeddedFormatEntry> BuildQuery()
    {
        IEnumerable<EmbeddedFormatEntry> query = _catalog.GetAll();

        foreach (var predicate in _predicates)
            query = query.Where(predicate);

        if (_order is not null)
            query = _order(query);

        return query;
    }
}

/// <summary>
/// Extension methods that attach <see cref="CatalogQuery"/> to <see cref="IEmbeddedFormatCatalog"/>.
/// </summary>
public static class CatalogQueryExtensions
{
    /// <summary>
    /// Begins a fluent query over the catalog.
    /// <code>
    /// var formats = catalog.Query()
    ///     .InCategory(FormatCategory.Images)
    ///     .WithMinQuality(70)
    ///     .OrderByName()
    ///     .Execute();
    /// </code>
    /// </summary>
    public static CatalogQuery Query(this IEmbeddedFormatCatalog catalog)
        => new(catalog);
}
