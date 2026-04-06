// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: Providers/XmlStructureProvider.cs
// Created: 2026-04-05
// Description:
//     Structure provider for XML-based files (XML, XAML, SVG, .csproj, etc.).
//     Converts the XDocument element tree to DocumentStructureNode hierarchy.
//
// Architecture Notes:
//     Priority 300. Uses System.Xml.Linq with SetLineInfo for line numbers.
//     Key attributes (x:Name, x:Class, Name, Key, Id) shown as Detail text.
// ==========================================================

using System.IO;
using System.Xml;
using System.Xml.Linq;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Core.DocumentStructure.Providers;

/// <summary>
/// XML/XAML element tree structure provider (Priority 300).
/// </summary>
public sealed class XmlStructureProvider : IDocumentStructureProvider
{
    private const int MaxDepth = 20;

    public string DisplayName => "XML Elements";
    public int Priority => 300;

    private static readonly HashSet<string> s_extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xml", ".xaml", ".csproj", ".vbproj", ".fsproj", ".props", ".targets",
        ".svg", ".config", ".resx", ".settings", ".manifest", ".xsd", ".xslt",
    };

    // Attributes that provide useful identification for the Detail column
    private static readonly string[] s_keyAttributes =
    {
        "Name", "{http://schemas.microsoft.com/winfx/2006/xaml}Name",     // x:Name
        "{http://schemas.microsoft.com/winfx/2006/xaml}Class",            // x:Class
        "{http://schemas.microsoft.com/winfx/2006/xaml}Key",              // x:Key
        "Id", "id", "Key", "key",
    };

    public bool CanProvide(string? filePath, string? documentType, string? language)
    {
        if (!string.IsNullOrEmpty(language) &&
            (language.Equals("xml", StringComparison.OrdinalIgnoreCase) ||
             language.Equals("xaml", StringComparison.OrdinalIgnoreCase)))
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
            ct.ThrowIfCancellationRequested();

            var doc = XDocument.Parse(text, LoadOptions.SetLineInfo);
            if (doc.Root is null) return null;

            var rootNode = ConvertElement(doc.Root, 0);
            if (rootNode is null) return null;

            return new DocumentStructureResult
            {
                Nodes = [rootNode],
                FilePath = filePath,
                Language = Path.GetExtension(filePath).Equals(".xaml", StringComparison.OrdinalIgnoreCase) ? "xaml" : "xml",
            };
        }
        catch (XmlException) { return null; }
        catch (OperationCanceledException) { throw; }
    }

    private static DocumentStructureNode? ConvertElement(XElement element, int depth)
    {
        if (depth > MaxDepth) return null;

        var children = new List<DocumentStructureNode>();
        foreach (var child in element.Elements())
        {
            var childNode = ConvertElement(child, depth + 1);
            if (childNode is not null)
                children.Add(childNode);
        }

        var lineInfo = (IXmlLineInfo)element;
        var detail = GetKeyAttribute(element);

        return new DocumentStructureNode
        {
            Name = element.Name.LocalName,
            Kind = "element",
            Detail = detail,
            StartLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1,
            StartColumn = lineInfo.HasLineInfo() ? lineInfo.LinePosition : -1,
            Children = children,
        };
    }

    private static string? GetKeyAttribute(XElement element)
    {
        foreach (var attrName in s_keyAttributes)
        {
            string? value;
            if (attrName.StartsWith('{'))
            {
                var xn = XName.Get(attrName.Split('}')[1], attrName[1..attrName.IndexOf('}')]);
                value = element.Attribute(xn)?.Value;
            }
            else
            {
                value = element.Attribute(attrName)?.Value;
            }

            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }
}
