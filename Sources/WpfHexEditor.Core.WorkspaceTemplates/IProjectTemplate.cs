// ==========================================================
// Project: WpfHexEditor.Core.WorkspaceTemplates
// File: IProjectTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Contract for a project template used in the New Project dialog.
//     Implementations provided by TemplateManager from embedded JSON
//     or user-defined files in %AppData%\WpfHexEditor\Templates\.
// ==========================================================

namespace WpfHexEditor.Core.WorkspaceTemplates;

/// <summary>
/// A workspace/project template that can be instantiated by <see cref="ProjectScaffolder"/>.
/// </summary>
public interface IProjectTemplate
{
    /// <summary>Unique template identifier (e.g. <c>"blank"</c>, <c>"sdk-plugin"</c>).</summary>
    string Id { get; }

    /// <summary>Human-readable name shown in the New Project dialog.</summary>
    string Name { get; }

    /// <summary>Short description shown below the template name.</summary>
    string Description { get; }

    /// <summary>Category label used for grouping (e.g. <c>"General"</c>, <c>"Plugin Development"</c>).</summary>
    string Category { get; }

    /// <summary>Default language (e.g. <c>"C#"</c>).</summary>
    string DefaultLanguage { get; }

    /// <summary>Plugin IDs that should be activated when the workspace is opened.</summary>
    IReadOnlyList<string> IncludedPlugins { get; }

    /// <summary>Files to scaffold, relative to the project root.</summary>
    IReadOnlyList<TemplateFile> Files { get; }
}

/// <summary>A single file generated during scaffolding.</summary>
public sealed record TemplateFile(
    /// <summary>Destination path relative to project root.</summary>
    string RelativePath,
    /// <summary>File content template (supports {{ProjectName}}, {{RootNamespace}} tokens).</summary>
    string Content);
