// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: Providers/JsonStructureProvider.cs
// Created: 2026-04-05
// Description:
//     Structure provider for JSON files. Parses the JSON DOM
//     into a hierarchical tree of objects, arrays, and keys.
//
// Architecture Notes:
//     Priority 300. Uses System.Text.Json (no external deps).
//     Max depth limit (default 10) to avoid huge trees on large files.
//     Primitive values shown as Detail text (truncated to 50 chars).
// ==========================================================

using System.IO;
using System.Text.Json;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.DocumentStructure.Providers;

/// <summary>
/// JSON DOM-based structure provider (Priority 300).
/// </summary>
public sealed class JsonStructureProvider : IDocumentStructureProvider
{
    private const int MaxDepth = 10;
    private const int MaxDetailLength = 50;

    public string DisplayName => "JSON Structure";
    public int Priority => 300;

    private static readonly HashSet<string> s_extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".jsonc", ".jsonl", ".geojson", ".whfmt"
    };

    public bool CanProvide(string? filePath, string? documentType, string? language)
    {
        if (!string.IsNullOrEmpty(language) &&
            (language.Equals("json", StringComparison.OrdinalIgnoreCase) ||
             language.Equals("jsonc", StringComparison.OrdinalIgnoreCase)))
            return true;
        if (string.IsNullOrEmpty(filePath)) return false;
        return s_extensions.Contains(Path.GetExtension(filePath));
    }

    public async Task<DocumentStructureResult?> GetStructureAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var text = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var nodes = ConvertElement(doc.RootElement, null, 0);
            if (nodes is null) return null;

            // Root can be an object (show its properties) or a single node
            var rootNodes = doc.RootElement.ValueKind == JsonValueKind.Object
                ? ((DocumentStructureNode)nodes).Children
                : new[] { nodes };

            return new DocumentStructureResult
            {
                Nodes = rootNodes.ToList(),
                FilePath = filePath,
                Language = "json",
            };
        }
        catch (JsonException) { return null; }
        catch (OperationCanceledException) { throw; }
    }

    private static DocumentStructureNode? ConvertElement(JsonElement element, string? name, int depth)
    {
        if (depth > MaxDepth) return null;

        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element, name ?? "{}", depth),
            JsonValueKind.Array  => ConvertArray(element, name ?? "[]", depth),
            _ => name is not null
                ? new DocumentStructureNode
                {
                    Name = name,
                    Kind = "key",
                    Detail = Truncate(element.ToString()),
                }
                : null,
        };
    }

    private static DocumentStructureNode ConvertObject(JsonElement obj, string name, int depth)
    {
        var children = new List<DocumentStructureNode>();
        foreach (var prop in obj.EnumerateObject())
        {
            var child = ConvertElement(prop.Value, prop.Name, depth + 1);
            if (child is not null)
                children.Add(child);
        }

        return new DocumentStructureNode
        {
            Name = name,
            Kind = "object",
            Detail = $"{children.Count} properties",
            Children = children,
        };
    }

    private static DocumentStructureNode ConvertArray(JsonElement arr, string name, int depth)
    {
        var children = new List<DocumentStructureNode>();
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var child = ConvertElement(item, $"[{index}]", depth + 1);
            if (child is not null)
                children.Add(child);
            index++;
        }

        return new DocumentStructureNode
        {
            Name = name,
            Kind = "array",
            Detail = $"{index} items",
            Children = children,
        };
    }

    private static string? Truncate(string? value)
    {
        if (value is null) return null;
        return value.Length > MaxDetailLength ? value[..MaxDetailLength] + "..." : value;
    }
}
