// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Snippets/ISnippetProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Contract for plugin-contributed snippet collections.
//     Plugins implement this interface and register it via
//     EditorPluginIntegration to inject language-specific snippets.
//
// Architecture Notes:
//     Strategy Pattern — EditorPluginIntegration aggregates all registered
//     ISnippetProvider instances and merges their snippets into SnippetManager.
// ==========================================================

namespace WpfHexEditor.Editor.CodeEditor.Snippets;

/// <summary>
/// Provides a set of code snippets for a specific language or domain.
/// Implement this interface in a plugin and register it via
/// <see cref="Services.EditorPluginIntegration"/> to extend the editor's snippet library.
/// </summary>
public interface ISnippetProvider
{
    /// <summary>
    /// Language identifier this provider targets (e.g. <c>"csharp"</c>, <c>"json"</c>).
    /// Use <c>"*"</c> to apply to all languages.
    /// </summary>
    string LanguageId { get; }

    /// <summary>Returns the snippets contributed by this provider.</summary>
    IReadOnlyList<Snippet> GetSnippets();
}
