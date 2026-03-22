//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using WpfHexEditor.Definitions;
using WpfHexEditor.Editor.TextEditor.Highlighting;

namespace WpfHexEditor.Editor.TextEditor.Services;

/// <summary>
/// Singleton catalog of all available <see cref="SyntaxDefinition"/>s.
/// <para>
/// Sources (in order of loading):
/// <list type="number">
///   <item>Embedded <c>.whfmt</c> resources that carry a <c>syntaxDefinition</c> block
///         (loaded via <c>EmbeddedFormatCatalog</c>).</item>
///   <item>User-defined files in <c>%AppData%\WpfHexEditor\SyntaxDefinitions\*.whlang</c></item>
/// </list>
/// User definitions with the same name as an embedded one override the embedded version.
/// </para>
/// </summary>
public sealed class SyntaxDefinitionCatalog
{
    private static SyntaxDefinitionCatalog? _instance;

    /// <summary>
    /// Singleton accessor. Thread-safe via double-check locking.
    /// </summary>
    public static SyntaxDefinitionCatalog Instance
    {
        get
        {
            if (_instance is null)
                lock (typeof(SyntaxDefinitionCatalog))
                    _instance ??= new SyntaxDefinitionCatalog();
            return _instance;
        }
    }

    private SyntaxDefinitionCatalog() { }

    private IReadOnlyList<SyntaxDefinition>? _all;
    private readonly object _lock = new();

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns all loaded syntax definitions, sorted by display name.
    /// </summary>
    public IReadOnlyList<SyntaxDefinition> GetAll()
    {
        if (_all is not null) return _all;
        lock (_lock)
        {
            if (_all is not null) return _all;
            _all = Load();
        }
        return _all;
    }

    /// <summary>
    /// Finds the best matching definition for a file extension.
    /// </summary>
    /// <param name="ext">File extension with leading dot, e.g. <c>".asm"</c>.</param>
    /// <returns>The first matching definition, or <see langword="null"/>.</returns>
    public SyntaxDefinition? FindByExtension(string? ext)
    {
        if (string.IsNullOrEmpty(ext)) return null;
        ext = ext.ToLowerInvariant();
        return GetAll().FirstOrDefault(d => d.Extensions.Contains(ext));
    }

    /// <summary>
    /// Finds a definition by its display name (case-insensitive).
    /// </summary>
    public SyntaxDefinition? FindByName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return GetAll().FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds the best definition for a full file path.
    /// </summary>
    public SyntaxDefinition? FindForFile(string? filePath)
        => FindByExtension(Path.GetExtension(filePath));

    /// <summary>
    /// Forces re-loading of all definitions (clears cache).
    /// </summary>
    public void Invalidate()
    {
        lock (_lock) { _all = null; }
    }

    // -----------------------------------------------------------------------
    // Loading
    // -----------------------------------------------------------------------

    private static IReadOnlyList<SyntaxDefinition> Load()
    {
        // Use a dictionary keyed by name so that user-defined definitions override embedded ones.
        var byName = new Dictionary<string, SyntaxDefinition>(StringComparer.OrdinalIgnoreCase);

        // 1. Embedded .whfmt resources that contain a syntaxDefinition block.
        var formatCatalog = EmbeddedFormatCatalog.Instance;
        foreach (var entry in formatCatalog.GetAll())
        {
            if (!entry.HasSyntaxDefinition) continue;
            try
            {
                var syntaxJson = formatCatalog.GetSyntaxDefinitionJson(entry.ResourceKey);
                if (syntaxJson is null) continue;

                var def = JsonSyntaxDefinitionParser.ParseFromSyntaxDefinitionBlock(
                    syntaxJson,
                    entry.Name,
                    entry.Category,
                    entry.Extensions,
                    entry.ResourceKey);

                if (def is not null && !string.IsNullOrEmpty(def.Name))
                    byName[def.Name] = def;
            }
            catch
            {
                // Skip malformed or incomplete syntaxDefinition blocks.
            }
        }

        // 2. User directory: %AppData%\WpfHexEditor\SyntaxDefinitions\
        var userDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "SyntaxDefinitions");

        if (Directory.Exists(userDir))
        {
            foreach (var file in Directory.EnumerateFiles(userDir, "*.whlang", SearchOption.AllDirectories))
            {
                var def = JsonSyntaxDefinitionParser.ParseFile(file);
                if (def is not null && !string.IsNullOrEmpty(def.Name))
                    byName[def.Name] = def;   // user overrides embedded
            }
        }

        return [.. byName.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
