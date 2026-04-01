// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/IBinaryParserExtension.cs
// Created: 2026-03-15
// Description:
//     Extension point contract for plugins that parse binary file structures.
//     Used by the Structure Overlay panel to discover parsers for the active file.
// ==========================================================

using System.IO;

namespace WpfHexEditor.SDK.ExtensionPoints;

/// <summary>
/// Extension point contract: binary structure parsing.
/// The Structure Overlay panel calls <see cref="TryParse"/> on all contributors
/// to find a parser that understands the active file format.
/// Register in manifest: <c>"extensions": { "BinaryParser": "MyPlugin.MyParserClass" }</c>
/// </summary>
public interface IBinaryParserExtension
{
    /// <summary>Display name for this parser (e.g. "ELF Parser").</summary>
    string ParserName { get; }

    /// <summary>
    /// File extensions this parser supports (with dot, e.g. ".elf", ".so").
    /// Used for quick pre-filtering before calling <see cref="TryParse"/>.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Attempts to parse <paramref name="data"/> as this parser's known format.
    /// Returns null when the data does not match.
    /// </summary>
    ParsedStructure? TryParse(Stream data, string fileExtension);
}

/// <summary>The result of a successful binary parse — a tree of named fields.</summary>
public sealed record ParsedStructure(
    string FormatName,
    IReadOnlyList<ParsedField> Fields);

/// <summary>A parsed named field at a specific offset.</summary>
public sealed record ParsedField(
    string Name,
    long Offset,
    int Length,
    string DisplayValue,
    string? Description = null,
    IReadOnlyList<ParsedField>? Children = null);
