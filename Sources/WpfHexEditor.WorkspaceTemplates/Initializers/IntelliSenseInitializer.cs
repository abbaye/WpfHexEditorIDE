// ==========================================================
// Project: WpfHexEditor.WorkspaceTemplates
// File: Initializers/IntelliSenseInitializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Activates BoostedIntelliSenseManager on the workspace after scaffolding.
//     Writes an intellisense.json config file to the project directory so the
//     IDE loads IntelliSense settings on workspace open.
//
// Architecture Notes:
//     Pattern: Strategy (IInitializer)
//     Config file is lightweight JSON — IDE reads it via AppSettings on load.
// ==========================================================

using System.Text.Json;

namespace WpfHexEditor.WorkspaceTemplates.Initializers;

/// <summary>
/// Writes an <c>intellisense.json</c> configuration to the project directory
/// that activates workspace-aware IntelliSense on first open.
/// </summary>
public sealed class IntelliSenseInitializer
{
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes <c>&lt;projectDirectory&gt;\intellisense.json</c> with default
    /// IntelliSense settings from <paramref name="config"/>.
    /// </summary>
    public async Task InitializeAsync(
        string                  projectDirectory,
        IntelliSenseConfig?     config = null,
        CancellationToken       ct     = default)
    {
        config ??= new IntelliSenseConfig();
        var dest = Path.Combine(projectDirectory, "intellisense.json");

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(dest, json, ct);
    }
}

// -----------------------------------------------------------------------
// Config model
// -----------------------------------------------------------------------

/// <summary>IntelliSense settings written to the workspace config file.</summary>
public sealed class IntelliSenseConfig
{
    public bool Enabled           { get; set; } = true;
    public bool WorkspaceAware    { get; set; } = true;
    public bool AutoImport        { get; set; } = true;
    public int  MaxSuggestions    { get; set; } = 50;
    public int  MinTriggerLength  { get; set; } = 1;
}
