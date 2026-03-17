// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/EditorPluginIntegration.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Central service allowing plugins to extend the CodeEditor at runtime.
//     Plugins register ISyntaxHighlighter, ISnippetProvider, IDiagnosticRule,
//     and IFoldingStrategy implementations; the CodeEditor queries this service
//     to obtain the active implementations for the current document language.
//
// Architecture Notes:
//     Pattern: Registry + Aggregator
//     - Thread-safe via lock (registrations happen during plugin activation,
//       reads happen on the UI thread — lock is brief and contention-free).
//     - IPluginCapabilityRegistry (SDK) tracks which plugins declare which
//       semantic feature strings; EditorPluginIntegration stores the actual
//       implementation objects.
//     - Keyed by languageId (case-insensitive). "*" matches any language.
//     - CodeEditor calls GetHighlighter(languageId) / GetSnippets(languageId) /
//       GetDiagnosticRules(languageId) / GetFoldingStrategy(languageId) as
//       replacement/augmentation of the built-in implementations.
// ==========================================================

using WpfHexEditor.Editor.CodeEditor.Folding;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Snippets;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Runtime extension point that lets plugins contribute language-specific
/// implementations to the CodeEditor without recompiling the editor itself.
/// Obtain the singleton via <c>IIDEHostContext</c> or constructor-inject in tests.
/// </summary>
public sealed class EditorPluginIntegration
{
    private readonly object _lock = new();

    // Per-language highlighter overrides (last registration wins).
    private readonly Dictionary<string, ISyntaxHighlighter> _highlighters
        = new(StringComparer.OrdinalIgnoreCase);

    // Per-language snippet providers (multiple per language supported).
    private readonly Dictionary<string, List<ISnippetProvider>> _snippetProviders
        = new(StringComparer.OrdinalIgnoreCase);

    // Per-language diagnostic rules (multiple per language supported).
    private readonly Dictionary<string, List<IDiagnosticRule>> _diagnosticRules
        = new(StringComparer.OrdinalIgnoreCase);

    // Per-language folding strategy overrides (last registration wins).
    private readonly Dictionary<string, IFoldingStrategy> _foldingStrategies
        = new(StringComparer.OrdinalIgnoreCase);

    // -----------------------------------------------------------------------
    // Registration API (called by plugins during activation)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers a syntax highlighter for <paramref name="languageId"/>.
    /// If a highlighter is already registered for that language, it is replaced.
    /// </summary>
    public void RegisterHighlighter(string languageId, ISyntaxHighlighter highlighter)
    {
        ArgumentNullException.ThrowIfNull(highlighter);
        lock (_lock)
            _highlighters[languageId] = highlighter;
    }

    /// <summary>
    /// Adds a snippet provider for <paramref name="languageId"/>.
    /// Use <c>"*"</c> to contribute snippets to every language.
    /// </summary>
    public void RegisterSnippetProvider(string languageId, ISnippetProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_lock)
        {
            if (!_snippetProviders.TryGetValue(languageId, out var list))
                _snippetProviders[languageId] = list = [];
            list.Add(provider);
        }
    }

    /// <summary>
    /// Adds a diagnostic rule for <paramref name="languageId"/>.
    /// Use <c>"*"</c> to apply the rule to every language.
    /// </summary>
    public void RegisterDiagnosticRule(string languageId, IDiagnosticRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        lock (_lock)
        {
            if (!_diagnosticRules.TryGetValue(languageId, out var list))
                _diagnosticRules[languageId] = list = [];
            list.Add(rule);
        }
    }

    /// <summary>
    /// Registers a folding strategy for <paramref name="languageId"/>.
    /// If a strategy is already registered for that language, it is replaced.
    /// </summary>
    public void RegisterFoldingStrategy(string languageId, IFoldingStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        lock (_lock)
            _foldingStrategies[languageId] = strategy;
    }

    // -----------------------------------------------------------------------
    // Query API (called by CodeEditor)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the plugin-registered syntax highlighter for <paramref name="languageId"/>,
    /// or <c>null</c> if none is registered (fall back to built-in highlighter).
    /// </summary>
    public ISyntaxHighlighter? GetHighlighter(string languageId)
    {
        lock (_lock)
            return _highlighters.TryGetValue(languageId, out var h) ? h : null;
    }

    /// <summary>
    /// Returns all snippets contributed by plugins for <paramref name="languageId"/>
    /// (includes universal <c>"*"</c> providers).
    /// </summary>
    public IReadOnlyList<Snippet> GetSnippets(string languageId)
    {
        lock (_lock)
        {
            var result = new List<Snippet>();
            AppendFrom(_snippetProviders, "*",        result);
            AppendFrom(_snippetProviders, languageId, result);
            return result;
        }
    }

    /// <summary>
    /// Returns all diagnostic rules that apply to <paramref name="languageId"/>
    /// (includes universal <c>"*"</c> rules).
    /// </summary>
    public IReadOnlyList<IDiagnosticRule> GetDiagnosticRules(string languageId)
    {
        lock (_lock)
        {
            var result = new List<IDiagnosticRule>();
            if (_diagnosticRules.TryGetValue("*",        out var all))  result.AddRange(all);
            if (_diagnosticRules.TryGetValue(languageId, out var lang)) result.AddRange(lang);
            return result;
        }
    }

    /// <summary>
    /// Returns the plugin-registered folding strategy for <paramref name="languageId"/>,
    /// or <c>null</c> if none is registered (fall back to built-in strategy).
    /// </summary>
    public IFoldingStrategy? GetFoldingStrategy(string languageId)
    {
        lock (_lock)
            return _foldingStrategies.TryGetValue(languageId, out var s) ? s : null;
    }

    // -----------------------------------------------------------------------
    // Unregistration (called during plugin deactivation)
    // -----------------------------------------------------------------------

    /// <summary>Removes all contributions registered by a plugin identified by its objects.</summary>
    public void UnregisterHighlighter(string languageId)
    {
        lock (_lock) _highlighters.Remove(languageId);
    }

    /// <summary>Removes a specific <see cref="ISnippetProvider"/> previously registered.</summary>
    public void UnregisterSnippetProvider(string languageId, ISnippetProvider provider)
    {
        lock (_lock)
            if (_snippetProviders.TryGetValue(languageId, out var list))
                list.Remove(provider);
    }

    /// <summary>Removes a specific <see cref="IDiagnosticRule"/> previously registered.</summary>
    public void UnregisterDiagnosticRule(string languageId, IDiagnosticRule rule)
    {
        lock (_lock)
            if (_diagnosticRules.TryGetValue(languageId, out var list))
                list.Remove(rule);
    }

    /// <summary>Removes the folding strategy override for <paramref name="languageId"/>.</summary>
    public void UnregisterFoldingStrategy(string languageId)
    {
        lock (_lock) _foldingStrategies.Remove(languageId);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static void AppendFrom(
        Dictionary<string, List<ISnippetProvider>> dict,
        string key,
        List<Snippet> result)
    {
        if (dict.TryGetValue(key, out var providers))
            foreach (var p in providers)
                result.AddRange(p.GetSnippets());
    }
}
