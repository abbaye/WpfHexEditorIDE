// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: Providers/LspDocumentStructureProvider.cs
// Created: 2026-04-05
// Description:
//     Highest-priority structure provider using LSP DocumentSymbols.
//     Queries the LSP server for the file and converts the result to
//     a DocumentStructureNode tree.
//
// Architecture Notes:
//     Priority 1000. Requires an ILspServerRegistry with a matching server.
//     Falls back gracefully when no LSP server is available (CanProvide=false).
//     Rebuilds hierarchy from ContainerName when server returns flat SymbolInformation.
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.Core.LSP;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.DocumentStructure.Providers;

/// <summary>
/// LSP-based document structure provider (Priority 1000).
/// Uses <see cref="ILspClient.DocumentSymbolsAsync"/> to obtain symbols.
/// </summary>
public sealed class LspDocumentStructureProvider : IDocumentStructureProvider
{
    private readonly ILspServerRegistry? _registry;
    private readonly Func<string?>? _getWorkspacePath;

    public string DisplayName => "LSP Symbols";
    public int Priority => 1000;

    /// <param name="registry">LSP server registry, or null if LSP is unavailable.</param>
    /// <param name="getWorkspacePath">Optional callback to get the workspace root path.</param>
    public LspDocumentStructureProvider(ILspServerRegistry? registry, Func<string?>? getWorkspacePath = null)
    {
        _registry = registry;
        _getWorkspacePath = getWorkspacePath;
    }

    public bool CanProvide(string? filePath, string? documentType, string? language)
    {
        if (_registry is null || string.IsNullOrEmpty(filePath)) return false;
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return false;
        return _registry.FindByExtension(ext) is not null;
    }

    public async Task<DocumentStructureResult?> GetStructureAsync(string filePath, CancellationToken ct = default)
    {
        if (_registry is null) return null;

        var ext = Path.GetExtension(filePath);
        var entry = _registry.FindByExtension(ext);
        if (entry is null) return null;

        ILspClient? client = null;
        try
        {
            client = _registry.CreateClient(entry, _getWorkspacePath?.Invoke());
            var symbols = await client.DocumentSymbolsAsync(filePath, ct).ConfigureAwait(false);
            if (symbols is null || symbols.Count == 0) return null;

            // Build hierarchy from ContainerName grouping
            var nodes = BuildHierarchy(symbols);

            return new DocumentStructureResult
            {
                Nodes = nodes,
                FilePath = filePath,
                Language = entry.LanguageId,
            };
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
        finally
        {
            if (client is not null)
                await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<DocumentStructureNode> BuildHierarchy(IReadOnlyList<LspDocumentSymbol> symbols)
    {
        // Group by ContainerName → children
        var byContainer = new Dictionary<string, List<LspDocumentSymbol>>();
        var topLevel = new List<LspDocumentSymbol>();

        foreach (var sym in symbols)
        {
            if (string.IsNullOrEmpty(sym.ContainerName))
            {
                topLevel.Add(sym);
            }
            else
            {
                if (!byContainer.TryGetValue(sym.ContainerName, out var list))
                {
                    list = [];
                    byContainer[sym.ContainerName] = list;
                }
                list.Add(sym);
            }
        }

        return topLevel.Select(s => ConvertNode(s, byContainer)).ToList();
    }

    private static DocumentStructureNode ConvertNode(
        LspDocumentSymbol symbol,
        Dictionary<string, List<LspDocumentSymbol>> byContainer)
    {
        var children = byContainer.TryGetValue(symbol.Name, out var childSymbols)
            ? childSymbols.Select(c => ConvertNode(c, byContainer)).ToList()
            : (IReadOnlyList<DocumentStructureNode>)[];

        return new DocumentStructureNode
        {
            Name = symbol.Name,
            Kind = NormalizeKind(symbol.Kind),
            StartLine = symbol.StartLine + 1,   // LSP is 0-based, we use 1-based
            StartColumn = symbol.StartColumn + 1,
            EndLine = symbol.EndLine + 1,
            EndColumn = symbol.EndColumn + 1,
            Children = children,
        };
    }

    private static string NormalizeKind(string kind) => kind.ToLowerInvariant() switch
    {
        "file" => "file",
        "module" => "module",
        "namespace" => "namespace",
        "package" => "namespace",
        "class" => "class",
        "method" => "method",
        "property" => "property",
        "field" => "field",
        "constructor" => "constructor",
        "enum" => "enum",
        "interface" => "interface",
        "function" => "function",
        "variable" => "variable",
        "constant" => "constant",
        "string" => "constant",
        "number" => "constant",
        "boolean" => "constant",
        "array" => "array",
        "object" => "object",
        "key" => "key",
        "null" => "constant",
        "enummember" => "enummember",
        "struct" => "struct",
        "event" => "event",
        "operator" => "method",
        "typeparameter" => "typeparameter",
        _ => kind.ToLowerInvariant(),
    };
}
