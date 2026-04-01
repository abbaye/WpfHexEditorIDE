// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/IGrammarProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     SDK contract that allows third-party plugins to contribute additional
//     UFWB grammar files (.grammar) to the Grammar Explorer panel at runtime.
//     Implement this interface alongside IWpfHexEditorPlugin to register grammars.
//
// Architecture Notes:
//     Pattern: Extension Point
//     The SynalysisGrammarPlugin discovers all IGrammarProvider implementations
//     through the plugin system and calls GetGrammars() + OpenGrammar() during
//     initialization to populate the SynalysisGrammarRepository.
//     Plugin contributions override embedded grammars for the same extension.
// ==========================================================

using System.IO;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Identifies a single grammar contributed by a plugin.
/// </summary>
public sealed record GrammarDescriptor(
    /// <summary>Unique identifier for this grammar, e.g. "MyPlugin.Formats.CustomBin".</summary>
    string GrammarId,

    /// <summary>Human-readable name shown in the Grammar Explorer panel.</summary>
    string Name,

    /// <summary>
    /// File extensions handled by this grammar (with leading dot, lowercased),
    /// e.g. [".cbin", ".dat"].
    /// </summary>
    IReadOnlyList<string> FileExtensions,

    /// <summary>Optional short description shown as a tooltip in the panel.</summary>
    string? Description = null,

    /// <summary>Author name or email. Empty when not specified.</summary>
    string? Author = null);

/// <summary>
/// Implemented by plugins that contribute one or more UFWB grammar files.
/// The SynalysisGrammarPlugin calls this interface during startup to register
/// contributed grammars in the shared <c>SynalysisGrammarRepository</c>.
/// </summary>
public interface IGrammarProvider
{
    /// <summary>Returns metadata for all grammars provided by this plugin.</summary>
    IReadOnlyList<GrammarDescriptor> GetGrammars();

    /// <summary>
    /// Opens a read stream for the grammar identified by <paramref name="grammarId"/>.
    /// The caller disposes the returned stream.
    /// Returns null when the grammar is unavailable.
    /// </summary>
    Stream? OpenGrammar(string grammarId);
}
