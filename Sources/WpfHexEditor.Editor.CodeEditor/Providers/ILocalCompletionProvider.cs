// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Providers/ILocalCompletionProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Language-scoped completion provider interface.
//     Registered via EditorPluginIntegration and called by SmartCompletePopup
//     alongside LSP and CodeSmartCompleteProvider to supply context-aware
//     suggestions (e.g. script globals, custom keywords, runtime symbols).
//
// Architecture Notes:
//     Strategy Pattern — EditorPluginIntegration aggregates all registered
//     ILocalCompletionProvider instances per language and merges results.
//     Parallel to ISnippetProvider/IDiagnosticRule registration pattern.
// ==========================================================

using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Providers;

/// <summary>
/// Provides language-scoped completion suggestions called by <see cref="Controls.SmartCompletePopup"/>
/// after LSP and <see cref="Services.CodeSmartCompleteProvider"/>.
/// Register via <see cref="Services.EditorPluginIntegration.RegisterCompletionProvider"/>.
/// </summary>
public interface ILocalCompletionProvider
{
    /// <summary>
    /// Language identifier this provider targets (e.g. <c>"csharp-script"</c>).
    /// Use <c>"*"</c> to apply to all languages.
    /// </summary>
    string LanguageId { get; }

    /// <summary>
    /// Returns completion suggestions for the given context.
    /// Called on the UI thread — implementations must be fast (no I/O, no async).
    /// </summary>
    IReadOnlyList<SmartCompleteSuggestion> GetCompletions(
        SmartCompleteContext context,
        LanguageDefinition?  language);
}
