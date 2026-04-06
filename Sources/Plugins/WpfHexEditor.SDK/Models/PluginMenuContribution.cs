// ==========================================================
// Project: WpfHexEditor.SDK
// File: Models/PluginMenuContribution.cs
// Description:
//     Declares a single menu/command contribution that a dormant plugin
//     pre-registers so it is discoverable via menus and Command Palette
//     before the plugin is actually loaded.
//
// Architecture Notes:
//     Declared in manifest.json under "menuContributions".
//     The PluginHost reads these for Dormant plugins and registers stub
//     menu items and commands. On activation the stubs are replaced by
//     the plugin's own registrations.
// ==========================================================

using System.Text.Json.Serialization;

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// A single menu / command palette entry that a dormant plugin declares
/// in its manifest so the IDE can surface it before the plugin loads.
/// </summary>
public sealed class PluginMenuContribution
{
    /// <summary>IDE command ID (e.g. "View.ArchiveExplorer").</summary>
    [JsonPropertyName("commandId")]
    public string CommandId { get; set; } = string.Empty;

    /// <summary>Display label in menus and Command Palette (use underscore for access key).</summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Slash-separated parent menu path (e.g. "View", "Tools").
    /// Defaults to "View".
    /// </summary>
    [JsonPropertyName("parentPath")]
    public string ParentPath { get; set; } = "View";

    /// <summary>
    /// Optional group name for separator clustering (e.g. "Panels", "FileTools").
    /// </summary>
    [JsonPropertyName("group")]
    public string? Group { get; set; }

    /// <summary>
    /// Functional category for View menu dynamic organization
    /// (e.g. "Analysis", "Data &amp; Files", "Editors &amp; Code").
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    /// <summary>Optional Segoe MDL2 Assets glyph character (e.g. "\uE7C3").</summary>
    [JsonPropertyName("iconGlyph")]
    public string? IconGlyph { get; set; }

    /// <summary>Optional default keyboard shortcut gesture (e.g. "Ctrl+Shift+A").</summary>
    [JsonPropertyName("shortcut")]
    public string? Shortcut { get; set; }
}
