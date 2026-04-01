// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignTimeXamlPreprocessor.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Extends DesignCanvas.SanitizeForPreview with d: namespace support.
//     Strips d:* attributes from the XAML before parsing, extracts the
//     design-time DataContext type, and provides it for injection after render.
//
// Architecture Notes:
//     Service — pure function, stateless.
//     Called by DesignCanvas before every render when d: namespace detected.
// ==========================================================

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Pre-processes XAML for design-time rendering:
/// strips d: namespace, extracts design DataContext type.
/// </summary>
public sealed class DesignTimeXamlPreprocessor
{
    private readonly DesignTimeDataService _dataService = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="xaml"/> contains any d: namespace usage.
    /// </summary>
    public static bool HasDesignNamespace(string xaml)
        => xaml.Contains("xmlns:d=", StringComparison.Ordinal)
        || xaml.Contains(" d:", StringComparison.Ordinal);

    /// <summary>
    /// Processes <paramref name="xaml"/> for design-time rendering.
    /// Strips d:* attributes and extracts an optional design DataContext.
    /// </summary>
    /// <param name="xaml">Raw XAML source (already base-sanitized).</param>
    /// <param name="designDataContext">
    /// An instance of the type declared in d:DesignInstance, or null.
    /// The caller should set this as the DataContext of the rendered root.
    /// </param>
    /// <returns>XAML with all d:* attributes removed, safe for XamlReader.Parse.</returns>
    public string Process(string xaml, out object? designDataContext)
    {
        designDataContext = _dataService.ExtractDesignDataContext(xaml);
        return DesignTimeDataService.StripDesignNamespace(xaml);
    }
}
