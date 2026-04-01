// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignTimeDataService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Processes the d:DataContext and d:DesignInstance attributes present
//     in XAML source, instantiates the referenced type, and provides the
//     resulting object as a DataContext for the design-time render.
//
// Architecture Notes:
//     Service — stateless text analysis + reflection.
//     Uses regex to extract d:DesignInstance Type=... declarations.
//     Activator.CreateInstance used to instantiate design-time types.
//     Falls back gracefully when the type cannot be found.
// ==========================================================

using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Extracts d:DataContext and d:DesignInstance info from XAML
/// and instantiates design-time data objects.
/// </summary>
public sealed class DesignTimeDataService
{
    // ── Regex patterns ────────────────────────────────────────────────────────

    // Matches: d:DataContext="{d:DesignInstance Type={x:Type ns:TypeName}, ...}"
    private static readonly Regex DesignInstanceRegex = new(
        @"d:DataContext=""\{d:DesignInstance\s+Type=\{x:Type\s+(?<ns>[^:}]+:)?(?<typeName>[^}]+)\}",
        RegexOptions.Compiled);

    // Matches: d:DataContext="{d:DesignInstance ns:TypeName, ...}"
    private static readonly Regex DesignInstanceSimpleRegex = new(
        @"d:DataContext=""\{d:DesignInstance\s+(?<ns>[^:""]+:)?(?<typeName>[^,""}\s]+)",
        RegexOptions.Compiled);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the design-time DataContext object from <paramref name="xaml"/>.
    /// Returns null if no d:DataContext is found or the type cannot be instantiated.
    /// </summary>
    public object? ExtractDesignDataContext(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return null;

        string? typeName = null;

        var m = DesignInstanceRegex.Match(xaml);
        if (m.Success)
            typeName = m.Groups["typeName"].Value.Trim();
        else
        {
            m = DesignInstanceSimpleRegex.Match(xaml);
            if (m.Success)
                typeName = m.Groups["typeName"].Value.Trim();
        }

        if (string.IsNullOrWhiteSpace(typeName)) return null;

        return TryInstantiate(typeName);
    }

    /// <summary>
    /// Removes all d: namespace attributes from <paramref name="xaml"/> so
    /// XamlReader.Parse does not fail on them. Returns the cleaned XAML.
    /// </summary>
    public static string StripDesignNamespace(string xaml)
    {
        // Remove xmlns:d declarations.
        xaml = Regex.Replace(xaml, @"\s+xmlns:d=""[^""]*""", string.Empty);

        // Remove d:* attributes.
        xaml = Regex.Replace(xaml, @"\s+d:\w+=""[^""]*""", string.Empty);
        xaml = Regex.Replace(xaml, @"\s+d:\w+='\{[^}]*\}'", string.Empty);

        return xaml;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static object? TryInstantiate(string typeName)
    {
        // Search loaded assemblies for a type matching the local name.
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in asm.GetExportedTypes())
                {
                    if (!type.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (type.GetConstructor(Type.EmptyTypes) is null) continue;

                    return Activator.CreateInstance(type);
                }
            }
            catch { /* skip inaccessible assemblies */ }
        }

        return null;
    }
}
