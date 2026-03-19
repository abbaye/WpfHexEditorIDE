// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ToolboxItem.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Represents a single draggable control entry in the XAML Toolbox panel.
//     Holds the display name, category, Segoe MDL2 Assets icon glyph,
//     and the default XAML snippet inserted when dropped onto the canvas.
//
// Architecture Notes:
//     Immutable record — all data fixed at registration time.
// ==========================================================

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// Describes a control available in the XAML Toolbox palette.
/// </summary>
/// <param name="Name">Display name shown in the toolbox list.</param>
/// <param name="Category">Toolbox category (e.g. "Layout", "Buttons").</param>
/// <param name="IconGlyph">Single character from Segoe MDL2 Assets (or empty).</param>
/// <param name="DefaultXaml">
/// XAML snippet inserted when the item is dropped onto the design canvas.
/// Should be a self-contained element with sensible default attributes.
/// </param>
public sealed record ToolboxItem(
    string Name,
    string Category,
    string IconGlyph,
    string DefaultXaml)
{
    /// <summary>Human-readable key used for filtering and drag-drop data.</summary>
    public string Key => $"{Category}/{Name}";
}
