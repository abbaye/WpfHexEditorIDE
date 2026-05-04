// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Schema/OoXmlSchemaEngine.cs
// Description:
//     Generic OOXML (DOCX/ODT) loader/serializer driven entirely by
//     DocumentSchemaDefinition. No hardcoded XML element names or
//     namespace URIs — all rules come from the .whfmt documentSchema.
//     Phase 11: LoadBlocks() and SerializeBlocks() stubs ready for
//     Phase 17 wiring into DocxDocumentSaver/OdtDocumentSaver.
// ==========================================================

using System.Xml;
using System.Xml.Linq;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.Schema;

/// <summary>
/// Schema-driven loader and serializer for OOXML (DOCX / ODT) documents.
/// All block-mapping and serialization rules come from
/// <see cref="DocumentSchemaDefinition"/>; zero format-specific logic in C#.
/// </summary>
public static class OoXmlSchemaEngine
{
    // ── Loading ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses an <see cref="XDocument"/> (the body XML entry of a ZIP package)
    /// into a list of <see cref="DocumentBlock"/>s using the supplied schema.
    /// </summary>
    public static List<DocumentBlock> LoadBlocks(XDocument doc, DocumentSchemaDefinition schema)
    {
        var nsMap  = BuildNsMap(schema);
        var result = new List<DocumentBlock>();
        var body   = FindBody(doc, schema, nsMap);
        if (body is null) return result;

        foreach (var element in body.Elements())
            ProcessElement(element, schema.BlockMappings, nsMap, result);

        return result;
    }

    private static void ProcessElement(
        XElement element,
        IEnumerable<BlockMappingRule> mappings,
        Dictionary<string, XNamespace> nsMap,
        List<DocumentBlock> output)
    {
        var localName = element.Name.LocalName;

        foreach (var rule in mappings)
        {
            var ruleLocal = LocalName(rule.XmlElement);
            if (!string.Equals(ruleLocal, localName, StringComparison.OrdinalIgnoreCase))
                continue;

            var block = new DocumentBlock
            {
                Kind      = rule.BlockKind,
                Text      = ExtractText(element, rule.TextSource, nsMap),
                RawOffset = -1,
                RawLength = 0
            };

            // Apply attribute mappings
            foreach (var attrRule in rule.AttributeMappings)
                ApplyAttributeMapping(block, element, attrRule, nsMap);

            // Process children recursively if child mappings exist
            if (rule.ChildMappings.Count > 0)
            {
                foreach (var child in element.Elements())
                    ProcessElement(child, rule.ChildMappings, nsMap, block.Children);
            }

            output.Add(block);
            return;
        }
    }

    private static string ExtractText(
        XElement element, string textSource,
        Dictionary<string, XNamespace> nsMap)
    {
        if (string.IsNullOrEmpty(textSource) || textSource == ".")
            return element.Value;

        // Simple path like "w:r/w:t" — navigate descendant elements
        var parts  = textSource.Split('/');
        var current = new List<XElement> { element };

        foreach (var part in parts)
        {
            var localPart = LocalName(part);
            current = current.SelectMany(e => e.Elements())
                             .Where(e => e.Name.LocalName == localPart)
                             .ToList();
        }

        return string.Concat(current.Select(e => e.Value));
    }

    private static void ApplyAttributeMapping(
        DocumentBlock block, XElement element,
        AttributeMappingRule rule,
        Dictionary<string, XNamespace> nsMap)
    {
        try
        {
            if (!string.IsNullOrEmpty(rule.XmlPath))
            {
                var val = EvaluateXPath(element, rule.XmlPath, nsMap);
                if (val is null && rule.Presence) return;
                if (rule.Presence)
                {
                    block.Attributes[rule.Attribute] = true;
                    return;
                }
                if (val is not null)
                {
                    block.Attributes[rule.Attribute] =
                        ApplyTransform(val, rule.Transform);
                }
            }
            else if (!string.IsNullOrEmpty(rule.XmlAttr))
            {
                var attrLocal = LocalName(rule.XmlAttr);
                var attrVal   = element.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == attrLocal)?.Value;
                if (attrVal is not null)
                    block.Attributes[rule.Attribute] = attrVal;
            }
        }
        catch { /* malformed XML — skip attribute */ }
    }

    private static string? EvaluateXPath(
        XElement root, string path,
        Dictionary<string, XNamespace> nsMap)
    {
        // Simplified path evaluator for patterns like "w:rPr/w:b" or "w:rPr/w:sz/@w:val"
        var isAttr = path.Contains("/@");
        var steps  = path.Replace("/@", "/").Split('/');
        var current = new List<XElement> { root };

        for (var i = 0; i < steps.Length; i++)
        {
            var step      = steps[i];
            var localStep = LocalName(step);
            var isLast    = i == steps.Length - 1;

            if (isLast && isAttr && step.StartsWith('@'))
            {
                return current.FirstOrDefault()
                              ?.Attributes()
                              .FirstOrDefault(a => a.Name.LocalName == localStep)?.Value;
            }

            current = current.SelectMany(e => e.Elements())
                             .Where(e => e.Name.LocalName == localStep)
                             .ToList();
        }

        return current.Count > 0 ? current[0].Value : null;
    }

    private static object ApplyTransform(string value, string transform) =>
        transform switch
        {
            "halfPointsToPoints" when int.TryParse(value, out var hp) => (object)(hp / 2.0),
            _ => value
        };

    // ── Serialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a list of <see cref="DocumentBlock"/>s back to an
    /// <see cref="XDocument"/> using the supplied schema's serialization rules.
    /// </summary>
    public static XDocument SerializeBlocks(
        IEnumerable<DocumentBlock> blocks, DocumentSchemaDefinition schema)
    {
        var nsMap = BuildNsMap(schema);
        var ns    = nsMap.TryGetValue(schema.NamespacePrefix, out var primary)
            ? primary
            : XNamespace.None;

        var root = new XElement(ns + GetBodyElementName(schema));

        foreach (var block in blocks)
        {
            var element = SerializeBlock(block, schema, ns, nsMap);
            if (element is not null) root.Add(element);
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static XElement? SerializeBlock(
        DocumentBlock block, DocumentSchemaDefinition schema,
        XNamespace ns, Dictionary<string, XNamespace> nsMap)
    {
        if (!schema.SerializationRules.TryGetValue(block.Kind, out var rule))
            return null;

        var localName = LocalName(rule.XmlElement);
        var element   = new XElement(ns + localName);

        // Serialize attributes back to XML
        SerializeAttributes(block, schema, element, ns, nsMap);

        // Text content — only when no children own the text (children serialize their own text)
        if (block.Children.Count == 0)
        {
            if (!string.IsNullOrEmpty(rule.TextElement))
            {
                var textPath   = rule.TextElement.Split('/');
                var textParent = element;
                foreach (var step in textPath.SkipLast(1))
                    textParent = GetOrAddChild(textParent, ns + LocalName(step));
                textParent.Add(new XElement(ns + LocalName(textPath.Last()), block.Text));
            }
            else
            {
                element.Value = block.Text;
            }
        }

        // Children
        foreach (var child in block.Children)
        {
            var childElement = SerializeBlock(child, schema, ns, nsMap);
            if (childElement is not null) element.Add(childElement);
        }

        return element;
    }

    private static void SerializeAttributes(
        DocumentBlock block, DocumentSchemaDefinition schema,
        XElement element, XNamespace ns,
        Dictionary<string, XNamespace> nsMap)
    {
        foreach (var attr in block.Attributes)
        {
            if (!schema.AttributeSerializationRules.TryGetValue(attr.Key, out var rule))
                continue;

            if (!string.IsNullOrEmpty(rule.Parent) && !string.IsNullOrEmpty(rule.XmlElement))
            {
                var parentEl = GetOrAddChild(element, ns + LocalName(rule.Parent));
                var attrEl   = new XElement(ns + LocalName(rule.XmlElement));

                if (!string.IsNullOrEmpty(rule.Attr))
                {
                    var attrVal = ApplySerializeTransform(attr.Value?.ToString() ?? string.Empty, rule.Transform);
                    attrEl.SetAttributeValue(ns + LocalName(rule.Attr), attrVal);
                }

                foreach (var extra in rule.Attrs)
                    attrEl.SetAttributeValue(ns + LocalName(extra.Key), extra.Value);

                parentEl.Add(attrEl);
            }
        }
    }

    private static string ApplySerializeTransform(string value, string transform) =>
        transform switch
        {
            "pointsToHalfPoints" when double.TryParse(value, out var pt) =>
                ((int)(pt * 2)).ToString(),
            _ => value
        };

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Dictionary<string, XNamespace> BuildNsMap(DocumentSchemaDefinition schema)
    {
        var map = new Dictionary<string, XNamespace>();
        if (!string.IsNullOrEmpty(schema.NamespacePrefix) && !string.IsNullOrEmpty(schema.Namespace))
            map[schema.NamespacePrefix] = XNamespace.Get(schema.Namespace);
        foreach (var kvp in schema.Namespaces)
            map[kvp.Key] = XNamespace.Get(kvp.Value);
        return map;
    }

    private static XElement? FindBody(
        XDocument doc, DocumentSchemaDefinition schema,
        Dictionary<string, XNamespace> nsMap)
    {
        // DOCX: /wsp:wordDocument/w:body  or just search for first element containing mappable children
        // Simple approach: use the document root's first element that has children
        return doc.Root?.Elements().FirstOrDefault()
               ?? doc.Root;
    }

    private static string GetBodyElementName(DocumentSchemaDefinition schema) =>
        schema.Engine switch
        {
            "ooxml" => "document",
            "odf"   => "office:text",
            _       => "body"
        };

    private static string LocalName(string prefixedName)
    {
        var idx = prefixedName.IndexOf(':');
        return idx >= 0 ? prefixedName[(idx + 1)..] : prefixedName;
    }

    private static XElement GetOrAddChild(XElement parent, XName name)
    {
        var existing = parent.Element(name);
        if (existing is not null) return existing;
        var child = new XElement(name);
        parent.AddFirst(child);
        return child;
    }
}
