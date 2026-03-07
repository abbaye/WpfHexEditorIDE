// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Languages/LanguageRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Singleton registry that maps file extensions to LanguageDefinitions.
//     Priority for resolution: project-default > user-imported > builtin.
//     Built-in languages (JSON, TBL, etc.) are registered at app startup.
//     User-defined .whlang files are registered via RegisterFromFile().
//
// Architecture Notes:
//     Registry / Service Locator Pattern — single global lookup point.
//     Priority chain: project-default dict > user dict > builtin dict.
//     Thread-safe reads via lock(s_lock); registrations at startup only.
// ==========================================================

using System.IO;
using System.Linq;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Languages;

/// <summary>
/// Singleton that maps file extensions (and optional project defaults) to
/// <see cref="LanguageDefinition"/> instances.
/// </summary>
public sealed class LanguageRegistry
{
    // -- Singleton ------------------------------------------------------------

    public static LanguageRegistry Instance { get; } = new();

    private LanguageRegistry() { }

    // -- Internal state -------------------------------------------------------

    private readonly object _lock = new();

    // Extension → language (lower-case extension keys, e.g. ".json").
    private readonly Dictionary<string, LanguageDefinition> _builtin = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LanguageDefinition> _user    = new(StringComparer.OrdinalIgnoreCase);

    // Project-level default overrides: projectId → (extension → language).
    private readonly Dictionary<string, Dictionary<string, LanguageDefinition>> _projectDefaults
        = new(StringComparer.Ordinal);

    // -- Registration ---------------------------------------------------------

    /// <summary>
    /// Registers a built-in language definition.
    /// Should only be called at application startup before any files are opened.
    /// </summary>
    public void RegisterBuiltin(LanguageDefinition definition)
    {
        lock (_lock)
        {
            foreach (var ext in definition.Extensions)
                _builtin[ext] = definition;
        }
    }

    /// <summary>
    /// Loads and registers a user-defined <c>.whlang</c> file.
    /// Overwrites any previous user registration for the same extensions.
    /// If the definition has <c>IsDefault = true</c> and a <paramref name="projectId"/>
    /// is supplied, also calls <see cref="SetProjectDefault"/> automatically.
    /// </summary>
    /// <param name="whlangPath">Absolute path to the <c>.whlang</c> file.</param>
    /// <param name="projectId">
    /// Optional project context. When provided and the file is marked <c>isDefault: true</c>,
    /// the language is automatically promoted to the project-level default for all its extensions.
    /// </param>
    public void RegisterFromFile(string whlangPath, string? projectId = null)
    {
        var definition = LanguageDefinitionSerializer.Load(whlangPath);
        lock (_lock)
        {
            foreach (var ext in definition.Extensions)
                _user[ext] = definition;
        }

        // Honour the IsDefault flag by promoting to project-level default when a context is available
        if (definition.IsDefault && projectId is not null)
            SetProjectDefault(projectId, definition);
    }

    /// <summary>
    /// Sets a project-level language default for a specific extension.
    /// Overrides both user and built-in registrations for that project.
    /// </summary>
    public void SetProjectDefault(string projectId, LanguageDefinition definition)
    {
        lock (_lock)
        {
            if (!_projectDefaults.TryGetValue(projectId, out var map))
                _projectDefaults[projectId] = map = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var ext in definition.Extensions)
                map[ext] = definition;
        }
    }

    /// <summary>
    /// Removes all project-level overrides for <paramref name="projectId"/>.
    /// </summary>
    public void ClearProjectDefaults(string projectId)
    {
        lock (_lock)
            _projectDefaults.Remove(projectId);
    }

    // -- Lookup ----------------------------------------------------------------

    /// <summary>
    /// Returns the best <see cref="LanguageDefinition"/> for <paramref name="filePath"/>
    /// considering: project-default → user → builtin → null.
    /// </summary>
    /// <param name="filePath">Absolute path of the file being opened.</param>
    /// <param name="project">Optional project context for project-level defaults.</param>
    public LanguageDefinition? GetLanguageForFile(string filePath, IProject? project = null)
    {
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return null;

        lock (_lock)
        {
            // 1. Project-level default.
            if (project is not null &&
                _projectDefaults.TryGetValue(project.Id, out var projMap) &&
                projMap.TryGetValue(ext, out var projLang))
                return projLang;

            // 2. User-imported.
            if (_user.TryGetValue(ext, out var userLang))
                return userLang;

            // 3. Built-in.
            if (_builtin.TryGetValue(ext, out var builtinLang))
                return builtinLang;
        }

        return null;
    }

    /// <summary>
    /// Returns all currently registered language definitions (builtin + user),
    /// de-duplicated by <see cref="LanguageDefinition.Id"/>.
    /// </summary>
    public IReadOnlyList<LanguageDefinition> AllLanguages()
    {
        lock (_lock)
        {
            var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<LanguageDefinition>();

            foreach (var lang in _user.Values)
                if (seen.Add(lang.Id)) result.Add(lang);

            foreach (var lang in _builtin.Values)
                if (seen.Add(lang.Id)) result.Add(lang);

            return result;
        }
    }

    /// <summary>
    /// Looks up a language by its unique <see cref="LanguageDefinition.Id"/>.
    /// Returns <see langword="null"/> when not found.
    /// </summary>
    public LanguageDefinition? FindById(string id)
    {
        lock (_lock)
        {
            return _user.Values.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? _builtin.Values.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
