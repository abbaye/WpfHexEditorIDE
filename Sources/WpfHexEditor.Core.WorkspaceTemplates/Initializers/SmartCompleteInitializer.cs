// ==========================================================
// Project: WpfHexEditor.Core.WorkspaceTemplates
// File: Initializers/SmartCompleteInitializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Activates BoostedSmartCompleteManager on the workspace after scaffolding.
//     Writes an SmartComplete.json config file to the project directory so the
//     IDE loads SmartComplete settings on workspace open.
//
// Architecture Notes:
//     Pattern: Strategy (IInitializer)
//     Config file is lightweight JSON — IDE reads it via AppSettings on load.
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.Core.WorkspaceTemplates.Initializers;

/// <summary>
/// Writes an <c>SmartComplete.json</c> configuration to the project directory
/// that activates workspace-aware SmartComplete on first open.
/// </summary>
public sealed class SmartCompleteInitializer
{
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes <c>&lt;projectDirectory&gt;\SmartComplete.json</c> with default
    /// SmartComplete settings from <paramref name="config"/>.
    /// </summary>
    public async Task InitializeAsync(
        string                  projectDirectory,
        SmartCompleteConfig?     config = null,
        CancellationToken       ct     = default)
    {
        config ??= new SmartCompleteConfig();
        var dest = Path.Combine(projectDirectory, "SmartComplete.json");

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(dest, json, ct);
    }
}

// -----------------------------------------------------------------------
// Config model
// -----------------------------------------------------------------------

/// <summary>SmartComplete settings written to the workspace config file.</summary>
public sealed class SmartCompleteConfig
{
    public bool Enabled           { get; set; } = true;
    public bool WorkspaceAware    { get; set; } = true;
    public bool AutoImport        { get; set; } = true;
    public int  MaxSuggestions    { get; set; } = 50;
    public int  MinTriggerLength  { get; set; } = 1;
}
