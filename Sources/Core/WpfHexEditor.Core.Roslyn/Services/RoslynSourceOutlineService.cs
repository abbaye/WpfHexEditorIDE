// ==========================================================
// Project: WpfHexEditor.Core.Roslyn
// File: Services/RoslynSourceOutlineService.cs
// Description:
//     ISourceOutlineService implementation backed by the Roslyn semantic
//     model. Wraps RoslynSymbolProvider.GetDocumentSymbolsAsync and adapts
//     LspDocumentSymbol[] to SourceOutlineModel for existing consumers
//     (Solution Explorer source members, navigation bar, DocumentStructure
//     provider).
//
//     Returns null when:
//       - the file is not a .cs file, or
//       - the workspace has not loaded a Document for the file (caller
//         should fall back to the regex SourceOutlineEngine).
// Architecture: thread-safe, async; cache keyed by (path, solutionVersion).
// ==========================================================

using System.Collections.Concurrent;
using System.IO;
using WpfHexEditor.Core.Roslyn.Providers;
using WpfHexEditor.Core.SourceAnalysis.Models;
using WpfHexEditor.Core.SourceAnalysis.Services;

namespace WpfHexEditor.Core.Roslyn.Services;

/// <summary>
/// Roslyn-backed implementation of <see cref="ISourceOutlineService"/>.
/// Falls back to <c>null</c> when no Roslyn document is available, so the
/// host can chain a regex-based fallback (Priority &lt; 700).
/// </summary>
public sealed class RoslynSourceOutlineService : ISourceOutlineService
{
    private const int MaxCacheSize = 256;

    private readonly Func<RoslynWorkspaceManager?> _workspaceAccessor;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public RoslynSourceOutlineService(RoslynWorkspaceManager workspace)
        : this(() => workspace)
    {
    }

    /// <summary>
    /// Lazy ctor — the workspace manager is resolved on each call, allowing
    /// the host to register the provider before any Roslyn workspace exists.
    /// Returns null from <see cref="GetOutlineAsync"/> until the workspace
    /// becomes available.
    /// </summary>
    public RoslynSourceOutlineService(Func<RoslynWorkspaceManager?> workspaceAccessor)
    {
        _workspaceAccessor = workspaceAccessor;
    }

    public bool CanOutline(string filePath) =>
        !string.IsNullOrEmpty(filePath)
        && (filePath.EndsWith(".cs",  StringComparison.OrdinalIgnoreCase)
         || filePath.EndsWith(".vb",  StringComparison.OrdinalIgnoreCase));

    public async Task<SourceOutlineModel?> GetOutlineAsync(string filePath, CancellationToken ct = default)
    {
        if (!CanOutline(filePath)) return null;

        var workspace = _workspaceAccessor();
        if (workspace is null) return null;

        var doc = workspace.GetDocument(filePath);
        if (doc is null) return null;     // host should fall back to regex engine

        var version = (await doc.GetTextVersionAsync(ct).ConfigureAwait(false)).ToString();
        if (_cache.TryGetValue(filePath, out var entry) && entry.Version == version)
            return entry.Model;

        var symbols = await RoslynSymbolProvider.GetDocumentSymbolsAsync(doc, ct).ConfigureAwait(false);
        var model   = BuildModel(filePath, symbols);

        _cache[filePath] = new CacheEntry(version, model);
        EvictIfOverCap();
        return model;
    }

    /// <summary>
    /// Drops oldest-added entries when the cache exceeds <see cref="MaxCacheSize"/>.
    /// ConcurrentDictionary doesn't preserve insertion order, so we just trim
    /// until we drop below the cap — fine for an outline cache.
    /// </summary>
    private void EvictIfOverCap()
    {
        if (_cache.Count <= MaxCacheSize) return;
        foreach (var key in _cache.Keys.Take(_cache.Count - MaxCacheSize).ToArray())
            _cache.TryRemove(key, out _);
    }

    public void Invalidate(string filePath) => _cache.TryRemove(filePath, out _);

    // ── Mapping LspDocumentSymbol → SourceOutlineModel ────────────────────────

    private static SourceOutlineModel BuildModel(string filePath, IReadOnlyList<WpfHexEditor.Editor.Core.LSP.LspDocumentSymbol> symbols)
    {
        // Pre-pass: collect all declared type names — used as O(1) lookup to
        // decide whether a member's container is a type or a namespace.
        var typeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in symbols)
            if (IsType(s.Kind, out _)) typeNames.Add(s.Name);

        var typeBuckets = new Dictionary<string, (SourceTypeKind kind, int line, List<SourceMemberModel> members)>(StringComparer.Ordinal);
        var topLevelTypes = new List<string>();

        foreach (var s in symbols)
        {
            if (IsType(s.Kind, out var typeKind))
            {
                if (!typeBuckets.ContainsKey(s.Name))
                {
                    typeBuckets[s.Name] = (typeKind, s.StartLine + 1, new List<SourceMemberModel>());
                    if (string.IsNullOrEmpty(s.ContainerName) || !typeNames.Contains(s.ContainerName))
                        topLevelTypes.Add(s.Name);
                }
            }
            else if (IsMember(s.Kind, out var memberKind))
            {
                if (s.ContainerName is { } parent && typeBuckets.TryGetValue(parent, out var bucket))
                    bucket.members.Add(new SourceMemberModel
                    {
                        Name       = s.Name,
                        Kind       = memberKind,
                        LineNumber = s.StartLine + 1,
                    });
            }
        }

        var types = topLevelTypes
            .Where(typeBuckets.ContainsKey)
            .Select(n => new SourceTypeModel
            {
                Name       = n,
                Kind       = typeBuckets[n].kind,
                LineNumber = typeBuckets[n].line,
                Members    = typeBuckets[n].members,
            })
            .ToList();

        return new SourceOutlineModel
        {
            FilePath = filePath,
            Kind     = SourceFileKind.CSharp, // model has no VB kind; C# is used as the structural representation
            Types    = types,
            ParsedAt = SafeLastWrite(filePath),
        };
    }

    private static DateTime SafeLastWrite(string filePath)
    {
        try { return File.Exists(filePath) ? File.GetLastWriteTimeUtc(filePath) : DateTime.UtcNow; }
        catch { return DateTime.UtcNow; }
    }

    private static bool IsType(string? kind, out SourceTypeKind result)
    {
        switch (kind)
        {
            case "class":     result = SourceTypeKind.Class;     return true;
            case "struct":    result = SourceTypeKind.Struct;    return true;
            case "interface": result = SourceTypeKind.Interface; return true;
            case "enum":      result = SourceTypeKind.Enum;      return true;
            default:          result = SourceTypeKind.Class;     return false;
        }
    }

    private static bool IsMember(string? kind, out SourceMemberKind result)
    {
        switch (kind)
        {
            case "method":      result = SourceMemberKind.Method;      return true;
            case "property":    result = SourceMemberKind.Property;    return true;
            case "field":
            case "constant":    result = SourceMemberKind.Field;       return true;
            case "event":       result = SourceMemberKind.Event;       return true;
            case "constructor": result = SourceMemberKind.Constructor; return true;
            default:            result = SourceMemberKind.Method;      return false;
        }
    }

    private sealed record CacheEntry(string Version, SourceOutlineModel Model);
}
