///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Core.Localization
// File        : LocalizedResourceDictionary.cs
// Description : Generic WPF ResourceDictionary that loads strings from one
//               or more ResourceManagers and updates them in-place when the
//               application culture changes — no app restart required.
//
// Architecture:
//   • Each NuGet UI package instantiates one LocalizedResourceDictionary,
//     passes its own ResourceManager via RegisterResourceManager().
//   • The common strings (CommonResources) are pre-registered by default.
//   • On culture change all registered dictionaries update simultaneously
//     via the static CultureChanged event, keeping all UI in sync.
//   • Fallback chain per key: requested culture → neutral culture → invariant.
//     Example: fr-CA → fr → en (invariant).
///////////////////////////////////////////////////////////////

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Windows;
using WpfHexEditor.Core.Localization.Properties;

namespace WpfHexEditor.Core.Localization.Services;

/// <summary>
/// A <see cref="ResourceDictionary"/> that mirrors one or more
/// <see cref="ResourceManager"/> instances into WPF dynamic resources,
/// enabling instant runtime language switching.
/// </summary>
public class LocalizedResourceDictionary : ResourceDictionary
{
    // ─── Static culture state ────────────────────────────────────────────────

    private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

    /// <summary>Fired after all registered dictionaries have been refreshed.</summary>
    public static event EventHandler<CultureChangedEventArgs>? CultureChanged;

    /// <summary>Gets the culture currently applied to all localized dictionaries.</summary>
    public static CultureInfo CurrentCulture => _currentCulture;

    /// <summary>
    /// Switches the active culture and refreshes every registered
    /// <see cref="LocalizedResourceDictionary"/> instance.
    /// </summary>
    public static void ChangeCulture(CultureInfo newCulture)
    {
        ArgumentNullException.ThrowIfNull(newCulture);

        var previous = _currentCulture;
        _currentCulture = newCulture;

        CultureInfo.CurrentUICulture = newCulture;
        CultureInfo.DefaultThreadCurrentUICulture = newCulture;

        CultureChanged?.Invoke(null, new CultureChangedEventArgs(previous, newCulture));
    }

    // ─── Instance resource managers ──────────────────────────────────────────

    private readonly List<ResourceManager> _managers = [];

    /// <summary>
    /// Initialises the dictionary with the built-in common strings
    /// (<see cref="CommonResources"/>) pre-registered.
    /// </summary>
    public LocalizedResourceDictionary()
    {
        RegisterResourceManager(CommonResources.ResourceManager);
        CultureChanged += OnCultureChanged;
        // Subclasses: call RegisterResourceManager() then LoadResources() in their own ctor.
        // Direct instantiation (<loc:LocalizedResourceDictionary/>) loads CommonResources only.
        LoadResources();
    }

    /// <summary>
    /// Registers an additional <see cref="ResourceManager"/> whose keys
    /// are merged into this dictionary, then reloads all resources.
    /// Subclasses must call this for each of their managers and then call
    /// <see cref="LoadResources"/> once all managers are registered.
    /// </summary>
    public void RegisterResourceManager(ResourceManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        if (!_managers.Contains(manager))
            _managers.Add(manager);
    }

    // ─── Resource loading ────────────────────────────────────────────────────

    private void OnCultureChanged(object? sender, CultureChangedEventArgs e)
        => LoadResources();

    /// <summary>
    /// Loads (or reloads) all resources from all registered managers for the current culture.
    /// Subclasses must call this once after all RegisterResourceManager calls.
    /// Keys from later-registered managers override keys from earlier ones.
    /// </summary>
    public void LoadResources()
    {
        foreach (var manager in _managers)
            LoadFromManager(manager);
    }

    private void LoadFromManager(ResourceManager manager)
    {
        // Enumerate keys from the neutral (en-US) resource set embedded in the main assembly.
        // InvariantCulture alone does not work when NeutralLanguage=en-US because the runtime
        // stores neutral resources under "en-US", not InvariantCulture. We try both explicitly.
        System.Resources.ResourceSet? baseSet = null;
        var probeCultures = new[]
        {
            new CultureInfo("en-US"),
            CultureInfo.InvariantCulture,
            _currentCulture,
        };
        foreach (var probe in probeCultures)
        {
            try
            {
                baseSet = manager.GetResourceSet(probe, createIfNotExists: true, tryParents: false);
            }
            catch (MissingManifestResourceException) { }
            catch (Exception) { }
            if (baseSet is not null) break;
        }

        if (baseSet is null)
            return;

        // Resolve the best culture this specific manager can actually serve.
        // Walking up from _currentCulture (e.g. es-MX → es → en-US) and stopping at
        // the first culture for which a satellite exists prevents a plugin that ships
        // an "es" satellite from displaying Spanish when CommonResources only has en-US
        // (i.e. when the IDE itself would show English for that OS locale).
        var effectiveCulture = ResolveManagerCulture(manager, _currentCulture);

        foreach (System.Collections.DictionaryEntry entry in baseSet)
        {
            if (entry.Key is not string key)
                continue;

            string? value;
            try { value = manager.GetString(key, effectiveCulture); }
            catch (MissingManifestResourceException) { value = null; }
            catch (Exception) { value = null; }

            this[key] = value ?? entry.Value?.ToString() ?? string.Empty;
        }
    }

    // ─── Culture resolution ──────────────────────────────────────────────────

    // Cache: maps (manager identity, requested culture name) → effective culture name.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int, string), string>
        _cultureCache = new();

    /// <summary>
    /// Finds the culture CommonResources would actually serve for <paramref name="requested"/>,
    /// mirroring the .NET satellite probe order: exact → neutral parent → regional siblings.
    /// Every plugin dictionary uses this same effective culture, keeping all assemblies in sync.
    /// Example: es-MX → no es-MX sat → no es sat → finds es-ES → return es-ES.
    /// </summary>
    private static CultureInfo ResolveManagerCulture(ResourceManager manager, CultureInfo requested)
    {
        var cacheKey = (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(manager), requested.Name);
        if (_cultureCache.TryGetValue(cacheKey, out var cached))
            return new CultureInfo(cached);

        var commonRm = CommonResources.ResourceManager;

        // Build probe list: exact, neutral parent, then all regional siblings sharing
        // the same neutral parent (e.g. for es-MX also try es-ES, es-419, etc.).
        var probes = new System.Collections.Generic.List<CultureInfo> { requested };

        var neutral = requested.Parent;
        if (!neutral.Equals(CultureInfo.InvariantCulture))
        {
            probes.Add(neutral);

            // Regional siblings: any satellite of CommonResources whose parent == neutral.
            foreach (var sibling in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                if (sibling.Parent.Name.Equals(neutral.Name, StringComparison.OrdinalIgnoreCase)
                    && !sibling.Name.Equals(requested.Name, StringComparison.OrdinalIgnoreCase))
                    probes.Add(sibling);
            }
        }

        foreach (var probe in probes)
        {
            bool commonHas = false;
            try { commonHas = commonRm.GetResourceSet(probe, createIfNotExists: true, tryParents: false) is not null; }
            catch { }

            if (commonHas)
            {
                _cultureCache[cacheKey] = probe.Name;
                return probe;
            }
        }

        // No common satellite found → use en-US (the IDE's neutral language).
        var fallback = new CultureInfo("en-US");
        _cultureCache[cacheKey] = fallback.Name;
        return fallback;
    }
}
