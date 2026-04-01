// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Schema/DocumentSchemaReader.cs
// Description:
//     Reads the "documentSchema" top-level section from a .whfmt file.
//     Only deserialises the documentSchema property — the rest of the
//     .whfmt JSON (binary parsing rules, forensic, navigation, etc.)
//     is ignored here and handled by FormatDefinitionService.
// ==========================================================

using System.Text.Json;
using System.Text.Json.Nodes;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Schema;

/// <summary>
/// Reads a <see cref="DocumentSchemaDefinition"/> from a .whfmt file's
/// <c>documentSchema</c> property.
/// </summary>
public static class DocumentSchemaReader
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Parses the <c>documentSchema</c> section from a .whfmt file.
    /// Returns <see langword="null"/> when the file does not contain
    /// a <c>documentSchema</c> key or cannot be parsed.
    /// </summary>
    public static DocumentSchemaDefinition? ReadFromWhfmt(string whfmtPath)
    {
        if (!File.Exists(whfmtPath)) return null;

        try
        {
            var text = File.ReadAllText(whfmtPath);
            var root = JsonNode.Parse(text, null, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip
            });

            var schemaNode = root?["documentSchema"];
            if (schemaNode is null) return null;

            return schemaNode.Deserialize<DocumentSchemaDefinition>(_opts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[DocumentSchemaReader] Failed to read '{whfmtPath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolves the absolute path to a .whfmt file relative to the executing assembly.
    /// Looks in: assembly directory, then parent directories up to 3 levels.
    /// </summary>
    public static string? ResolveWhfmtPath(string fileName)
    {
        var asmDir = AppContext.BaseDirectory;

        // Search common locations
        var candidates = new[]
        {
            Path.Combine(asmDir, fileName),
            Path.Combine(asmDir, "FormatDefinitions", "Documents", fileName),
            Path.Combine(asmDir, "..", "FormatDefinitions", "Documents", fileName),
            Path.Combine(asmDir, "..", "..", "FormatDefinitions", "Documents", fileName)
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full)) return full;
        }

        return null;
    }
}
