// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: DocumentStructureProviderResolver.cs
// Created: 2026-04-05
// Description:
//     Manages registered IDocumentStructureProvider instances.
//     Resolves the highest-priority provider for a given document context.
//
// Architecture Notes:
//     Providers are sorted by Priority descending on registration.
//     Resolve returns the first CanProvide match. ResolveAll returns all matches.
// ==========================================================

using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.DocumentStructure;

/// <summary>
/// Registry and resolver for <see cref="IDocumentStructureProvider"/> instances.
/// Picks the highest-priority provider that can handle a given document.
/// </summary>
public sealed class DocumentStructureProviderResolver
{
    private readonly List<IDocumentStructureProvider> _providers = [];

    /// <summary>Registers a provider, maintaining descending Priority order.</summary>
    public void Register(IDocumentStructureProvider provider)
    {
        var index = 0;
        while (index < _providers.Count && _providers[index].Priority >= provider.Priority)
            index++;
        _providers.Insert(index, provider);
    }

    /// <summary>
    /// Returns the highest-priority provider that can handle the document, or null.
    /// </summary>
    public IDocumentStructureProvider? Resolve(string? filePath, string? documentType, string? language)
        => _providers.FirstOrDefault(p => p.CanProvide(filePath, documentType, language));

    /// <summary>
    /// Returns all providers that can handle the document, in priority order.
    /// </summary>
    public IReadOnlyList<IDocumentStructureProvider> ResolveAll(string? filePath, string? documentType, string? language)
        => _providers.Where(p => p.CanProvide(filePath, documentType, language)).ToList();
}
