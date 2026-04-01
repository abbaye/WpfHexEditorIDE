// ==========================================================
// Project: WpfHexEditor.SDK
// File: Models/PluginActivationConfig.cs
// Created: 2026-03-15
// Description:
//     Declares the lazy-activation triggers for a plugin.
//     Embedded in PluginManifest.Activation (nullable — absence means startup load).
//
// Architecture Notes:
//     Pure JSON-serializable model, no WPF or side-effects.
// ==========================================================

using System.Text.Json.Serialization;

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Declares when a plugin should be activated (lazy-loaded).
/// If <c>OnStartup</c> is true (or this config is absent from the manifest),
/// the plugin loads immediately at IDE startup like any classic plugin.
/// </summary>
public sealed class PluginActivationConfig
{
    /// <summary>
    /// File extensions (with dot) that trigger this plugin when a matching file is opened.
    /// Example: [".exe", ".dll", ".sys"]
    /// </summary>
    [JsonPropertyName("fileExtensions")]
    public List<string> FileExtensions { get; set; } = [];

    /// <summary>
    /// IDE command IDs that trigger this plugin when executed.
    /// Example: ["analyze.pe", "view.assembly"]
    /// </summary>
    [JsonPropertyName("commands")]
    public List<string> Commands { get; set; } = [];

    /// <summary>
    /// When true, the plugin loads immediately at IDE startup (same as not having an activation config).
    /// Defaults to false — plugin is dormant until a trigger fires.
    /// </summary>
    [JsonPropertyName("onStartup")]
    public bool OnStartup { get; set; } = false;

    /// <summary>Returns true if this config requires eager loading at startup.</summary>
    [JsonIgnore]
    public bool IsStartupLoad => OnStartup || (FileExtensions.Count == 0 && Commands.Count == 0);
}
