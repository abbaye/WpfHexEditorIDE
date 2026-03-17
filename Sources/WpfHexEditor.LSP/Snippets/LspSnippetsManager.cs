// ==========================================================
// Project: WpfHexEditor.LSP
// File: Snippets/LspSnippetsManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Aggregates snippet definitions from .whlang built-in templates and
//     plugin-contributed ISnippetProvider instances.
//     Exposes GetSnippets(languageId, trigger) for IntelliSense lookup.
//
// Architecture Notes:
//     Pattern: Aggregator
//     - Built-in snippets sourced from LanguageDefinition.SnippetTemplates.
//     - Plugin snippets sourced from EditorPluginIntegration (ISnippetProvider).
//     - Priority: UserCreated language > Imported > BuiltIn (same as LanguagePriority).
// ==========================================================

using WpfHexEditor.Editor.CodeEditor.Services;
using WpfHexEditor.LSP.Models;

namespace WpfHexEditor.LSP.Snippets;

/// <summary>
/// Provides snippets from .whlang definitions and plugin-contributed providers.
/// </summary>
public sealed class LspSnippetsManager
{
    private readonly LanguageDefinitionManager _languages;
    private readonly EditorPluginIntegration   _pluginIntegration;

    public LspSnippetsManager(
        LanguageDefinitionManager languages,
        EditorPluginIntegration   pluginIntegration)
    {
        _languages         = languages         ?? throw new ArgumentNullException(nameof(languages));
        _pluginIntegration = pluginIntegration ?? throw new ArgumentNullException(nameof(pluginIntegration));
    }

    /// <summary>
    /// Returns snippets that match <paramref name="triggerPrefix"/> for <paramref name="languageId"/>.
    /// </summary>
    public IReadOnlyList<(string Trigger, string Body, string Description)> GetSnippets(
        string languageId, string triggerPrefix)
    {
        var result = new List<(string, string, string)>();

        // Built-in snippets from .whlang definition.
        var lang = _languages.GetById(languageId);
        if (lang is not null)
        {
            foreach (var (trigger, body, desc) in lang.SnippetTemplates)
                if (trigger.StartsWith(triggerPrefix, StringComparison.OrdinalIgnoreCase))
                    result.Add((trigger, body, desc));
        }

        // Plugin-contributed snippets.
        foreach (var snippet in _pluginIntegration.GetSnippets(languageId))
            if (snippet.Trigger.StartsWith(triggerPrefix, StringComparison.OrdinalIgnoreCase))
                result.Add((snippet.Trigger, snippet.Body, snippet.Description));

        return result;
    }
}
