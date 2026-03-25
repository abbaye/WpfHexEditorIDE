// Project      : WpfHexEditorControl
// File         : Algorithms/SemanticDiffAlgorithm.cs
// Description  : Format-aware diff that parses JSON/XML/C# structure before diffing.
//                Falls back to MyersDiffAlgorithm on any parse failure.
// Architecture : Implements IDiffAlgorithm; uses System.Text.Json + System.Xml.Linq.

using System.Text.Json;
using System.Xml.Linq;
using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Algorithms;

/// <summary>
/// Format-aware diff algorithm.
/// Linearizes the parsed structure (JSON property paths, XML element paths, source lines)
/// into a sequence of strings, then runs Myers on those sequences.
/// Falls back to Myers line-diff on any parse error.
/// </summary>
public sealed class SemanticDiffAlgorithm : IDiffAlgorithm
{
    private static readonly MyersDiffAlgorithm _myers = new();

    // -----------------------------------------------------------------------
    // IDiffAlgorithm
    // -----------------------------------------------------------------------

    public BinaryDiffResult ComputeBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        => _myers.ComputeBytes(left, right);

    public TextDiffResult ComputeLines(string[] leftLines, string[] rightLines)
    {
        // Re-join lines to detect format from content
        var leftText  = string.Join("\n", leftLines);
        var rightText = string.Join("\n", rightLines);

        try
        {
            if (IsJson(leftText) && IsJson(rightText))
                return DiffJson(leftText, rightText);

            if (IsXml(leftText) && IsXml(rightText))
                return DiffXml(leftText, rightText);
        }
        catch
        {
            // Fall through to Myers
        }

        return new TextDiffResult
        {
            Lines          = _myers.ComputeLines(leftLines, rightLines).Lines,
            Stats          = _myers.ComputeLines(leftLines, rightLines).Stats,
            FallbackReason = "Semantic parse failed — using Myers line diff"
        };
    }

    // -----------------------------------------------------------------------
    // JSON diff: linearize property paths and values, then Myers
    // -----------------------------------------------------------------------

    private static TextDiffResult DiffJson(string leftText, string rightText)
    {
        var leftPaths  = LinearizeJson(leftText);
        var rightPaths = LinearizeJson(rightText);
        return _myers.ComputeLines(leftPaths, rightPaths);
    }

    private static string[] LinearizeJson(string json)
    {
        var lines = new List<string>();
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        WalkJson(doc.RootElement, "", lines);
        return [.. lines];
    }

    private static void WalkJson(JsonElement element, string path, List<string> lines)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                lines.Add($"{path}={{");
                foreach (var prop in element.EnumerateObject())
                    WalkJson(prop.Value, $"{path}.{prop.Name}", lines);
                lines.Add($"{path}}}");
                break;

            case JsonValueKind.Array:
                lines.Add($"{path}=[");
                int i = 0;
                foreach (var item in element.EnumerateArray())
                    WalkJson(item, $"{path}[{i++}]", lines);
                lines.Add($"{path}]");
                break;

            default:
                lines.Add($"{path}={element.GetRawText()}");
                break;
        }
    }

    // -----------------------------------------------------------------------
    // XML diff: linearize element paths, then Myers
    // -----------------------------------------------------------------------

    private static TextDiffResult DiffXml(string leftText, string rightText)
    {
        var leftPaths  = LinearizeXml(leftText);
        var rightPaths = LinearizeXml(rightText);
        return _myers.ComputeLines(leftPaths, rightPaths);
    }

    private static string[] LinearizeXml(string xml)
    {
        var lines = new List<string>();
        var doc   = XDocument.Parse(xml);
        WalkXml(doc.Root, "", lines);
        return [.. lines];
    }

    private static void WalkXml(XElement? element, string path, List<string> lines)
    {
        if (element is null) return;

        var localPath = $"{path}/{element.Name.LocalName}";

        foreach (var attr in element.Attributes())
            lines.Add($"{localPath}[@{attr.Name.LocalName}]={attr.Value}");

        if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
            lines.Add($"{localPath}={element.Value.Trim()}");
        else
            lines.Add(localPath);

        int idx = 0;
        var grouped = element.Elements().GroupBy(e => e.Name.LocalName);
        foreach (var g in grouped)
        {
            idx = 0;
            foreach (var child in g)
                WalkXml(child, $"{localPath}/{child.Name.LocalName}[{idx++}]", lines);
        }
    }

    // -----------------------------------------------------------------------
    // Sniffers
    // -----------------------------------------------------------------------

    private static bool IsJson(string text)
    {
        var t = text.TrimStart();
        return t.StartsWith('{') || t.StartsWith('[');
    }

    private static bool IsXml(string text)
    {
        var t = text.TrimStart();
        return t.StartsWith('<');
    }
}
