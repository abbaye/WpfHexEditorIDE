// ==========================================================
// Project: WpfHexEditor.Editor.TextEditor
// File: Highlighting/EmbeddedLanguageEntry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-22
// Description:
//     Maps a code-fence language identifier (e.g. "csharp")
//     to a file extension so that FencedCodeHighlighter can
//     resolve the correct SyntaxDefinition from the catalog.
// ==========================================================

namespace WpfHexEditor.Editor.TextEditor.Highlighting;

/// <summary>
/// Maps a code-fence language identifier (e.g. <c>"csharp"</c>) to the file
/// extension used to look up the corresponding <see cref="SyntaxDefinition"/>
/// in the <c>SyntaxDefinitionCatalog</c> (e.g. <c>".cs"</c>).
/// </summary>
/// <param name="Id">Fence language tag, case-insensitive (e.g. <c>"csharp"</c>, <c>"cs"</c>).</param>
/// <param name="Extension">File extension including the dot (e.g. <c>".cs"</c>).</param>
public sealed record EmbeddedLanguageEntry(string Id, string Extension);
