// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Templates/IPluginTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Contract for all plugin scaffold templates.
//     Each concrete template (Panel, Editor, EventListener, Converter)
//     implements this interface and is surfaced in the New Plugin Wizard.
//
// Architecture Notes:
//     Pattern: Strategy — the wizard selects a concrete template at runtime.
// ==========================================================

namespace WpfHexEditor.PluginDev.Templates;

/// <summary>
/// Contract for a plugin project scaffold template.
/// </summary>
public interface IPluginTemplate
{
    /// <summary>Machine-readable identifier used in wizard persistence.</summary>
    string TemplateId { get; }

    /// <summary>Human-readable name shown in the wizard UI.</summary>
    string DisplayName { get; }

    /// <summary>Short description shown below the template card.</summary>
    string Description { get; }

    /// <summary>Segoe MDL2 icon glyph for the template card.</summary>
    string Icon { get; }

    /// <summary>
    /// Generates all source files inside <paramref name="outputDir"/>.
    /// </summary>
    Task ScaffoldAsync(
        string            outputDir,
        string            pluginName,
        string            authorName,
        CancellationToken ct = default);
}
