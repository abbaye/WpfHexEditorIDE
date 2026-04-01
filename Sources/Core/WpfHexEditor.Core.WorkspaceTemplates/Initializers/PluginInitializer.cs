// ==========================================================
// Project: WpfHexEditor.Core.WorkspaceTemplates
// File: Initializers/PluginInitializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Records the set of plugins required by a workspace template in the
//     project's whproj file so the IDE can load and connect them on open.
//
// Architecture Notes:
//     Pattern: Strategy (IInitializer)
//     Writes a plugins.json sidecar; IDE reads it in PluginHost startup.
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.Core.WorkspaceTemplates.Initializers;

/// <summary>
/// Writes a <c>plugins.json</c> sidecar listing the plugin IDs required by the
/// workspace template so the IDE can load them when the project is opened.
/// </summary>
public sealed class PluginInitializer
{
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes <c>&lt;projectDirectory&gt;\plugins.json</c> with
    /// the plugin IDs declared by the template.
    /// </summary>
    public async Task InitializeAsync(
        string                  projectDirectory,
        IReadOnlyList<string>   requiredPluginIds,
        CancellationToken       ct = default)
    {
        if (requiredPluginIds.Count == 0) return;

        var payload = new PluginManifestList(requiredPluginIds);
        var json    = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var dest    = Path.Combine(projectDirectory, "plugins.json");

        await File.WriteAllTextAsync(dest, json, ct);
    }
}

// -----------------------------------------------------------------------
// Payload model
// -----------------------------------------------------------------------

/// <summary>Serialised list of required plugin IDs for a workspace.</summary>
internal sealed record PluginManifestList(IReadOnlyList<string> RequiredPlugins);
