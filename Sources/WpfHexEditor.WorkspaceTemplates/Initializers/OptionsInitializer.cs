// ==========================================================
// Project: WpfHexEditor.WorkspaceTemplates
// File: Initializers/OptionsInitializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Writes the default IDE options from a template into the project's
//     options.json so settings are applied on first workspace open.
//
// Architecture Notes:
//     Pattern: Strategy (IInitializer)
//     Serialises the OptionsDocument provided by the template.
//     IDE merges workspace options with global settings on load.
// ==========================================================

using System.Text.Json;

namespace WpfHexEditor.WorkspaceTemplates.Initializers;

/// <summary>
/// Writes an <c>options.json</c> file into the project directory,
/// seeding IDE settings from the template's <see cref="WorkspaceOptionsDocument"/>.
/// </summary>
public sealed class OptionsInitializer
{
    // -----------------------------------------------------------------------

    /// <summary>
    /// Serialises <paramref name="document"/> to
    /// <c>&lt;projectDirectory&gt;\options.json</c>.
    /// When <paramref name="document"/> is null, writes an empty default.
    /// </summary>
    public async Task InitializeAsync(
        string                   projectDirectory,
        WorkspaceOptionsDocument? document  = null,
        CancellationToken         ct        = default)
    {
        document ??= new WorkspaceOptionsDocument();
        var dest = Path.Combine(projectDirectory, "options.json");

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(dest, json, ct);
    }
}

// -----------------------------------------------------------------------
// Options document model
// -----------------------------------------------------------------------

/// <summary>Workspace-scoped IDE options written by the template scaffolder.</summary>
public sealed class WorkspaceOptionsDocument
{
    public string? DefaultLanguageId { get; set; }
    public bool    EnableIntelliSense { get; set; } = true;
    public bool    EnableFolding      { get; set; } = true;
    public int     TabSize            { get; set; } = 4;
    public bool    UseSpaces          { get; set; } = true;
    public string? ThemeId            { get; set; }
}
