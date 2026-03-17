// ==========================================================
// Project: WpfHexEditor.WorkspaceTemplates
// File: TemplateManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Central registry for workspace templates.
//     Loads built-in templates from embedded JSON resources and
//     user-defined templates from %AppData%\WpfHexEditor\Templates\.
//     Priority: user-created > imported > built-in.
//
// Architecture Notes:
//     Pattern: Registry — centralises template discovery and lookup.
//     Built-in templates are embedded in the assembly to avoid external
//     file dependencies for the base IDE distribution.
// ==========================================================

using System.Reflection;
using System.Text.Json;

namespace WpfHexEditor.WorkspaceTemplates;

/// <summary>
/// Loads and provides access to all project templates.
/// </summary>
public sealed class TemplateManager
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly List<IProjectTemplate> _templates = [];

    // -----------------------------------------------------------------------
    // Construction + loading
    // -----------------------------------------------------------------------

    public TemplateManager()
    {
        LoadBuiltIn();
        LoadUserDefined();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Returns all available templates, ordered by priority (user > built-in).</summary>
    public IReadOnlyList<IProjectTemplate> GetAll() => _templates;

    /// <summary>Returns all templates in a given category.</summary>
    public IReadOnlyList<IProjectTemplate> GetByCategory(string category)
        => _templates.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                     .ToList();

    /// <summary>Returns the template with the given ID, or null.</summary>
    public IProjectTemplate? GetById(string id)
        => _templates.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Registers a template dynamically (e.g. from a plugin).</summary>
    public void Register(IProjectTemplate template)
    {
        _templates.RemoveAll(t => t.Id.Equals(template.Id, StringComparison.OrdinalIgnoreCase));
        _templates.Insert(0, template);
    }

    // -----------------------------------------------------------------------
    // Loading helpers
    // -----------------------------------------------------------------------

    private void LoadBuiltIn()
    {
        var asm  = Assembly.GetExecutingAssembly();
        var ns   = typeof(TemplateManager).Namespace!;

        foreach (var resourceName in asm.GetManifestResourceNames()
            .Where(n => n.StartsWith($"{ns}.Templates.", StringComparison.OrdinalIgnoreCase)
                     && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null) continue;

            try
            {
                var dto = JsonSerializer.Deserialize<TemplateDto>(stream, _json);
                if (dto is not null) _templates.Add(dto.ToTemplate());
            }
            catch { /* skip malformed embedded resources */ }
        }
    }

    private void LoadUserDefined()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "Templates");

        if (!System.IO.Directory.Exists(dir)) return;

        foreach (var file in System.IO.Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var json = System.IO.File.ReadAllText(file);
                var dto  = JsonSerializer.Deserialize<TemplateDto>(json, _json);
                if (dto is not null) _templates.Insert(0, dto.ToTemplate()); // user-defined first
            }
            catch { /* skip malformed user templates */ }
        }
    }

    // -----------------------------------------------------------------------
    // DTO
    // -----------------------------------------------------------------------

    private sealed class TemplateDto
    {
        public string   Id              { get; set; } = string.Empty;
        public string   Name            { get; set; } = string.Empty;
        public string   Description     { get; set; } = string.Empty;
        public string   Category        { get; set; } = "General";
        public string   DefaultLanguage { get; set; } = "C#";
        public string[] IncludedPlugins { get; set; } = [];
        public FileDtoEntry[] Files     { get; set; } = [];

        public IProjectTemplate ToTemplate() => new ConcreteTemplate(
            Id, Name, Description, Category, DefaultLanguage, IncludedPlugins,
            Files.Select(f => new TemplateFile(f.Path, f.Content)).ToList());

        public sealed class FileDtoEntry
        {
            public string Path    { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }
    }

    // -----------------------------------------------------------------------

    private sealed class ConcreteTemplate(
        string id, string name, string description, string category,
        string defaultLanguage, IReadOnlyList<string> plugins,
        IReadOnlyList<TemplateFile> files) : IProjectTemplate
    {
        public string                   Id              => id;
        public string                   Name            => name;
        public string                   Description     => description;
        public string                   Category        => category;
        public string                   DefaultLanguage => defaultLanguage;
        public IReadOnlyList<string>    IncludedPlugins => plugins;
        public IReadOnlyList<TemplateFile> Files        => files;
    }
}
