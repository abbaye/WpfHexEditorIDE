// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResourceScannerService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Scans Application.Current.Resources and its merged ResourceDictionaries
//     to collect all resource entries for display in the Resource Browser panel.
//     Each entry is tagged with its scope (Application, Theme, etc.).
//
// Architecture Notes:
//     Pure service — stateless scan.
//     Recursive merge-dictionary walk with deduplication.
// ==========================================================

using System.Windows;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Scans WPF ResourceDictionary hierarchies and returns structured entries.
/// </summary>
public sealed class ResourceScannerService
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all resource entries visible in the current application,
    /// tagged by scope (Application / Merged / etc.).
    /// </summary>
    public IReadOnlyList<ResourceEntryViewModel> ScanAll()
    {
        var results = new List<ResourceEntryViewModel>();
        var visited = new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance);

        if (Application.Current?.Resources is ResourceDictionary appRes)
            ScanDictionary(appRes, "Application", results, visited);

        return results;
    }

    /// <summary>
    /// Returns all resources visible from <paramref name="element"/>,
    /// including its logical-tree ancestors and application-level resources.
    /// </summary>
    public IReadOnlyList<ResourceEntryViewModel> ScanElement(FrameworkElement element)
    {
        var results = new List<ResourceEntryViewModel>();
        var visited = new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance);

        ScanDictionary(element.Resources, $"Element ({element.GetType().Name})", results, visited);

        var parent = element.Parent as FrameworkElement;
        while (parent is not null)
        {
            ScanDictionary(parent.Resources, $"Ancestor ({parent.GetType().Name})", results, visited);
            parent = parent.Parent as FrameworkElement;
        }

        if (Application.Current?.Resources is ResourceDictionary appRes)
            ScanDictionary(appRes, "Application", results, visited);

        return results;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void ScanDictionary(
        ResourceDictionary dict,
        string scope,
        List<ResourceEntryViewModel> results,
        HashSet<ResourceDictionary> visited)
    {
        if (!visited.Add(dict)) return;

        // Scan merged dictionaries first (lower priority).
        foreach (var merged in dict.MergedDictionaries)
        {
            var mergedScope = merged.Source is not null
                ? System.IO.Path.GetFileNameWithoutExtension(
                    merged.Source.IsAbsoluteUri ? merged.Source.LocalPath : merged.Source.OriginalString)
                : scope;
            ScanDictionary(merged, mergedScope, results, visited);
        }

        // Scan this dictionary's own entries.
        foreach (var key in dict.Keys)
        {
            try
            {
                var value = dict[key];
                results.Add(new ResourceEntryViewModel(key, value, scope));
            }
            catch { /* skip inaccessible resources */ }
        }
    }
}
