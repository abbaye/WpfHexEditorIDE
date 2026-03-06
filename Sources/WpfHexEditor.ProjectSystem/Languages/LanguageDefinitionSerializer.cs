// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Languages/LanguageDefinitionSerializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     JSON deserialiser for .whlang language definition files.
//     .whlang files are standard UTF-8 JSON documents whose root object
//     maps directly to LanguageDefinitionDto (an internal DTO class).
//     The DTO is then projected to the immutable LanguageDefinition model.
//
// Architecture Notes:
//     Adapter Pattern — converts JSON DTO → domain model.
//     Uses System.Text.Json with camelCase naming policy for compact files.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.ProjectSystem.Languages;

/// <summary>
/// Reads a <see cref="LanguageDefinition"/> from a <c>.whlang</c> JSON file.
/// </summary>
public static class LanguageDefinitionSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Deserialises a <c>.whlang</c> file and returns the corresponding
    /// <see cref="LanguageDefinition"/>.
    /// </summary>
    /// <param name="filePath">Absolute path to a <c>.whlang</c> file.</param>
    /// <returns>The parsed language definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the file is malformed or missing required fields.</exception>
    public static LanguageDefinition Load(string filePath)
    {
        var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        return Parse(json);
    }

    /// <summary>
    /// Parses a JSON string and returns the corresponding <see cref="LanguageDefinition"/>.
    /// </summary>
    public static LanguageDefinition Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<LanguageDefinitionDto>(json, s_options)
            ?? throw new InvalidOperationException("Failed to deserialise language definition: JSON root is null.");

        if (string.IsNullOrWhiteSpace(dto.Id))
            throw new InvalidOperationException("Language definition is missing required field 'id'.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Language definition is missing required field 'name'.");

        return new LanguageDefinition
        {
            Id          = dto.Id,
            Name        = dto.Name,
            Extensions  = dto.Extensions ?? [],
            SyntaxRules = dto.SyntaxRules?.Select(r => new SyntaxRule
            {
                Pattern = r.Pattern ?? string.Empty,
                Kind    = r.Kind
            }).ToArray() ?? [],
            Snippets = dto.Snippets?.Select(s => new SnippetDefinition
            {
                Trigger     = s.Trigger ?? string.Empty,
                Body        = s.Body    ?? string.Empty,
                Description = s.Description ?? string.Empty
            }).ToArray() ?? [],
            FoldingStrategy  = dto.FoldingStrategy,
            LineCommentPrefix = dto.LineCommentPrefix
        };
    }

    // ── Internal DTO ─────────────────────────────────────────────────────────

    private sealed class LanguageDefinitionDto
    {
        public string?               Id                { get; set; }
        public string?               Name              { get; set; }
        public string[]?             Extensions        { get; set; }
        public SyntaxRuleDto[]?      SyntaxRules       { get; set; }
        public SnippetDefinitionDto[]? Snippets         { get; set; }
        public FoldingStrategyKind   FoldingStrategy   { get; set; } = FoldingStrategyKind.Brace;
        public string?               LineCommentPrefix { get; set; }
    }

    private sealed class SyntaxRuleDto
    {
        public string?         Pattern { get; set; }
        public SyntaxTokenKind Kind    { get; set; }
    }

    private sealed class SnippetDefinitionDto
    {
        public string? Trigger     { get; set; }
        public string? Body        { get; set; }
        public string? Description { get; set; }
    }
}
