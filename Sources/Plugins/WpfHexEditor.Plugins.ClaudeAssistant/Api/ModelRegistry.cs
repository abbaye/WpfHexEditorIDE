// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ModelRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Registry of all available model providers. Providers register at startup.
// ==========================================================
namespace WpfHexEditor.Plugins.ClaudeAssistant.Api;

public sealed class ModelRegistry
{
    private readonly Dictionary<string, IModelProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IModelProvider> Providers => _providers.Values;

    public void Register(IModelProvider provider)
    {
        _providers[provider.ProviderId] = provider;
    }

    public IModelProvider? GetProvider(string providerId)
    {
        _providers.TryGetValue(providerId, out var provider);
        return provider;
    }

    public IEnumerable<(string ProviderId, string ModelId)> GetAllModels()
    {
        foreach (var p in _providers.Values)
            foreach (var m in p.AvailableModels)
                yield return (p.ProviderId, m);
    }
}
