// ==========================================================
// Project: WpfHexEditor.Core
// File: SyntaxDefinition.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-04-27
// Description:
//     Data model for the "syntaxDefinition" top-level block in .whfmt files.
//     Used exclusively by code-editor .whfmt files (preferredEditor: code-editor)
//     to declare syntax coloring rules, folding strategy, formatting defaults,
//     and preview snippets for a source-code language.
//
// Architecture Notes:
//     Deserialized from JSON via System.Text.Json. Complex sub-objects
//     (rules, foldingRules, formattingRules, etc.) are captured as JsonElement
//     for forward-compatibility — the CodeEditor layer owns their interpretation.
//     No WPF dependencies.
//
// ==========================================================

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Source-code language definition embedded in a .whfmt file.
    /// Declares syntax tokenization, folding, formatting, and preview data
    /// for code-editor format files (preferredEditor: code-editor).
    /// </summary>
    public class SyntaxDefinition
    {
        /// <summary>
        /// Unique language identifier (e.g., "csharp", "python").
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Display name (e.g., "C# Source File").
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// File extensions handled by this language (e.g., [".cs", ".csx"]).
        /// </summary>
        [JsonPropertyName("extensions")]
        public List<string> Extensions { get; set; } = new List<string>();

        /// <summary>
        /// Prefix shown before diagnostic messages in the Error List (e.g., "CS", "PY").
        /// </summary>
        [JsonPropertyName("diagnosticPrefix")]
        public string DiagnosticPrefix { get; set; }

        /// <summary>
        /// Single-line comment token (e.g., "//", "#", "--").
        /// </summary>
        [JsonPropertyName("lineCommentPrefix")]
        public string LineCommentPrefix { get; set; }

        /// <summary>
        /// Block comment open token (e.g., "/*").
        /// </summary>
        [JsonPropertyName("blockCommentStart")]
        public string BlockCommentStart { get; set; }

        /// <summary>
        /// Block comment close token (e.g., "*/").
        /// </summary>
        [JsonPropertyName("blockCommentEnd")]
        public string BlockCommentEnd { get; set; }

        /// <summary>
        /// Column positions where vertical ruler lines are drawn.
        /// </summary>
        [JsonPropertyName("columnRulers")]
        public List<int> ColumnRulers { get; set; } = new List<int>();

        /// <summary>
        /// Representative multi-line code snippet shown in the Formatting options live preview panel.
        /// </summary>
        [JsonPropertyName("previewSnippet")]
        public string PreviewSnippet { get; set; }

        /// <summary>
        /// Language IDs to inherit tokenization rules from (resolved by LanguageRegistry).
        /// </summary>
        [JsonPropertyName("includes")]
        public List<string> Includes { get; set; } = new List<string>();

        /// <summary>
        /// Complex sub-objects (rules, foldingRules, breakpointRules, formattingRules,
        /// bracketPairs, previewSamples, ideMetadata, scriptGlobals) captured verbatim.
        /// Consumed and interpreted by the CodeEditor layer (LanguageRegistry, etc.).
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; }
    }
}
