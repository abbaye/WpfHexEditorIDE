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
        LoadResources();
    }

    /// <summary>
    /// Registers an additional <see cref="ResourceManager"/> whose keys
    /// are merged into this dictionary. Call immediately after construction,
    /// before the dictionary is added to <c>MergedDictionaries</c>.
    /// </summary>
    /// <param name="manager">The ResourceManager to add (must not be null).</param>
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
    /// Reloads all resources from all registered managers for the current culture.
    /// Keys from later-registered managers override keys from earlier ones,
    /// allowing module-specific overrides of common strings when needed.
    /// </summary>
    private void LoadResources()
    {
        foreach (var manager in _managers)
            LoadFromManager(manager);
    }

    private void LoadFromManager(ResourceManager manager)
    {
        // Use the invariant culture resource set as the authoritative key list.
        var invariantSet = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true);
        if (invariantSet is null)
            return;

        foreach (System.Collections.DictionaryEntry entry in invariantSet)
        {
            if (entry.Key is not string key)
                continue;

            var value = ResolveWithFallback(manager, key, _currentCulture)
                        ?? entry.Value?.ToString()
                        ?? string.Empty;

            this[key] = value;
        }
    }

    /// <summary>
    /// Resolves a single key with the standard fallback chain:
    /// <paramref name="culture"/> → neutral culture → invariant.
    /// Returns <see langword="null"/> only when invariant also has no value.
    /// </summary>
    private static string? ResolveWithFallback(ResourceManager manager, string key, CultureInfo culture)
    {
        // Walk: specific → neutral → invariant
        CultureInfo? current = culture;
        while (current is not null && current != CultureInfo.InvariantCulture)
        {
            try
            {
                var set = manager.GetResourceSet(current, createIfNotExists: false, tryParents: false);
                var value = set?.GetString(key);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            catch (MissingManifestResourceException) { /* satellite not available */ }

            current = current.Parent == current ? null : current.Parent;
        }

        // Final fallback: invariant (base .resx)
        try { return manager.GetString(key, CultureInfo.InvariantCulture); }
        catch (MissingManifestResourceException) { return null; }
    }
}
