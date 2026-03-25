// ==========================================================
// Project: WpfHexEditor.Core.WorkspaceTemplates
// File: Initializers/LanguageInitializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Copies the .whlang definition file(s) declared in a project template
//     into the scaffolded project directory and marks the first one as default.
//
// Architecture Notes:
//     Pattern: Strategy (IInitializer)
//     Executed by ProjectScaffolder after the base directory structure is created.
// ==========================================================

using System.IO;

namespace WpfHexEditor.Core.WorkspaceTemplates.Initializers;

/// <summary>
/// Copies template .whlang language definition files into the project
/// and tags the first one as the default language for SmartComplete.
/// </summary>
public sealed class LanguageInitializer
{
    private readonly string _templateDirectory;

    public LanguageInitializer(string templateDirectory)
        => _templateDirectory = templateDirectory
            ?? throw new ArgumentNullException(nameof(templateDirectory));

    // -----------------------------------------------------------------------

    /// <summary>
    /// Copies all .whlang files from the template directory into
    /// <paramref name="projectDirectory"/>.
    /// The first file found is written with a <c>default: true</c> header
    /// so LanguageDefinitionManager picks it up as the workspace default.
    /// </summary>
    public async Task InitializeAsync(string projectDirectory, CancellationToken ct = default)
    {
        var langFiles = Directory.GetFiles(_templateDirectory, "*.whlang");
        bool first = true;

        foreach (var src in langFiles)
        {
            ct.ThrowIfCancellationRequested();

            var dest = Path.Combine(projectDirectory, Path.GetFileName(src));
            var content = await File.ReadAllTextAsync(src, ct);

            if (first)
            {
                // Inject or replace the "default:" line so LanguageDefinitionManager
                // treats this language as the workspace default.
                content = InjectDefaultTag(content);
                first = false;
            }

            await File.WriteAllTextAsync(dest, content, ct);
        }
    }

    // -----------------------------------------------------------------------

    private static string InjectDefaultTag(string content)
    {
        const string tag = "default: true";

        // Already tagged — nothing to do.
        if (content.Contains(tag, StringComparison.OrdinalIgnoreCase))
            return content;

        // Prepend the tag as a YAML-style comment/key at the top.
        return $"# {tag}\n{content}";
    }
}
