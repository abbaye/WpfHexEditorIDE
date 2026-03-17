// ==========================================================
// Project: WpfHexEditor.Core
// File: SynalysisGrammar/SynalysisGrammarParser.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Deserialises a Synalysis / Hexinator UFWB grammar XML file into the
//     UfwbRoot object graph. Handles all element types defined in UFWB 1.x
//     (structure, number, binary, string, structref, fixedvalues).
//
// Architecture Notes:
//     Pattern: Parser (no state between calls — all methods are static or
//     instance-stateless after construction).
//     Uses System.Xml.Linq (inbox in net8.0, zero NuGet).
//     Malformed or unknown XML elements are skipped with a warning rather
//     than throwing, ensuring forward-compatibility with future UFWB versions.
// ==========================================================

using System;
using System.IO;
using System.Xml.Linq;

namespace WpfHexEditor.Core.SynalysisGrammar;

/// <summary>
/// Parses a UFWB grammar XML document into a <see cref="UfwbRoot"/> object graph.
/// </summary>
public sealed class SynalysisGrammarParser
{
    // -- Public entry points ---------------------------------------------------

    /// <summary>Parses a grammar from a file on disk.</summary>
    /// <param name="path">Absolute path to the .grammar file.</param>
    /// <returns>Parsed grammar root.</returns>
    public UfwbRoot ParseFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var stream = File.OpenRead(path);
        return ParseFromStream(stream);
    }

    /// <summary>Parses a grammar from an open stream (does not close the stream).</summary>
    public UfwbRoot ParseFromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var doc = XDocument.Load(stream, LoadOptions.None);
        return ParseDocument(doc);
    }

    /// <summary>Parses a grammar from an XML string.</summary>
    public UfwbRoot ParseFromString(string xml)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml);
        var doc = XDocument.Parse(xml, LoadOptions.None);
        return ParseDocument(doc);
    }

    // -- Document -----------------------------------------------------------

    private static UfwbRoot ParseDocument(XDocument doc)
    {
        var ufwb = doc.Root;
        if (ufwb is null || ufwb.Name.LocalName != "ufwb")
            throw new InvalidOperationException("Not a valid UFWB grammar: root element must be <ufwb>.");

        var root = new UfwbRoot
        {
            Version = Attr(ufwb, "version"),
        };

        var grammarEl = ufwb.Element("grammar");
        if (grammarEl is null)
            throw new InvalidOperationException("Not a valid UFWB grammar: missing <grammar> element.");

        root.Grammar = ParseGrammar(grammarEl);
        return root;
    }

    // -- Grammar ------------------------------------------------------------

    private static UfwbGrammar ParseGrammar(XElement el)
    {
        var grammar = new UfwbGrammar
        {
            Name          = Attr(el, "name"),
            Start         = Attr(el, "start"),
            Author        = Attr(el, "author"),
            FileExtension = Attr(el, "fileextension"),
            Uti           = Attr(el, "uti"),
            Description   = DescriptionText(el),
        };

        foreach (var child in el.Elements("structure"))
            grammar.Structures.Add(ParseStructure(child));

        return grammar;
    }

    // -- Structure ----------------------------------------------------------

    private static UfwbStructure ParseStructure(XElement el)
    {
        var structure = new UfwbStructure
        {
            Id            = Attr(el, "id"),
            Name          = Attr(el, "name"),
            Extends       = Attr(el, "extends"),
            Encoding      = Attr(el, "encoding"),
            Endian        = Attr(el, "endian"),
            Signed        = Attr(el, "signed"),
            Length        = Attr(el, "length"),
            FillColor     = Attr(el, "fillcolor"),
            VariableOrder = Attr(el, "order") == "variable",
            Floating      = Attr(el, "floating") == "yes",
            Description   = DescriptionText(el),
            RepeatMin     = ParseRepeat(Attr(el, "repeatmin"), defaultValue: 1),
            RepeatMax     = ParseRepeat(Attr(el, "repeatmax"), defaultValue: 1),
        };

        foreach (var child in el.Elements())
        {
            var element = ParseElement(child);
            if (element is not null)
                structure.Elements.Add(element);
        }

        return structure;
    }

    // -- Element dispatch ---------------------------------------------------

    private static UfwbElement? ParseElement(XElement el) =>
        el.Name.LocalName switch
        {
            "number"      => ParseNumber(el),
            "binary"      => ParseBinary(el),
            "string"      => ParseString(el),
            "structref"   => ParseStructRef(el),
            "structure"   => ParseStructure(el),   // inline / nested
            "description" => null,                  // handled by DescriptionText — skip here
            _             => null,                  // unknown / future elements ignored
        };

    // -- Number -------------------------------------------------------------

    private static UfwbNumber ParseNumber(XElement el) => new()
    {
        Id          = Attr(el, "id"),
        Name        = Attr(el, "name"),
        Type        = AttrOrDefault(el, "type", "integer"),
        Length      = Attr(el, "length"),
        Display     = Attr(el, "display"),
        FillColor   = Attr(el, "fillcolor"),
        Signed      = Attr(el, "signed"),
        MustMatch   = Attr(el, "mustmatch") == "yes",
        Description = DescriptionText(el),
        FixedValues = ParseFixedValues(el),
    };

    // -- Binary -------------------------------------------------------------

    private static UfwbBinary ParseBinary(XElement el) => new()
    {
        Id          = Attr(el, "id"),
        Name        = Attr(el, "name"),
        Length      = Attr(el, "length"),
        FillColor   = Attr(el, "fillcolor"),
        MustMatch   = Attr(el, "mustmatch") == "yes",
        Description = DescriptionText(el),
        FixedValues = ParseFixedValues(el),
    };

    // -- String -------------------------------------------------------------

    private static UfwbString ParseString(XElement el) => new()
    {
        Id          = Attr(el, "id"),
        Name        = Attr(el, "name"),
        Type        = AttrOrDefault(el, "type", "zero-terminated"),
        Length      = Attr(el, "length"),
        Encoding    = Attr(el, "encoding"),
        Description = DescriptionText(el),
    };

    // -- StructRef ----------------------------------------------------------

    private static UfwbStructRef ParseStructRef(XElement el) => new()
    {
        Id           = Attr(el, "id"),
        Name         = Attr(el, "name"),
        StructureRef = Attr(el, "structure"),
        Description  = DescriptionText(el),
        RepeatMin    = ParseRepeat(Attr(el, "repeatmin"), defaultValue: 1),
        RepeatMax    = ParseRepeat(Attr(el, "repeatmax"), defaultValue: 1),
    };

    // -- FixedValues --------------------------------------------------------

    private static UfwbFixedValues? ParseFixedValues(XElement parent)
    {
        var fvEl = parent.Element("fixedvalues");
        if (fvEl is null) return null;

        var fv = new UfwbFixedValues();
        foreach (var vEl in fvEl.Elements("fixedvalue"))
        {
            fv.Values.Add(new UfwbFixedValue
            {
                Name        = Attr(vEl, "name"),
                Value       = Attr(vEl, "value"),
                Description = DescriptionText(vEl),
            });
        }

        return fv;
    }

    // -- Helpers ------------------------------------------------------------

    /// <summary>Returns attribute value or empty string when absent.</summary>
    private static string Attr(XElement el, string name)
        => el.Attribute(name)?.Value ?? string.Empty;

    /// <summary>Returns attribute value or <paramref name="defaultValue"/> when absent.</summary>
    private static string AttrOrDefault(XElement el, string name, string defaultValue)
        => el.Attribute(name)?.Value ?? defaultValue;

    /// <summary>
    /// Extracts the trimmed text of the first &lt;description&gt; child element.
    /// Returns empty string when absent.
    /// </summary>
    private static string DescriptionText(XElement el)
        => el.Element("description")?.Value.Trim() ?? string.Empty;

    /// <summary>
    /// Parses a repeat attribute value: returns -1 for "unlimited", the integer value,
    /// or <paramref name="defaultValue"/> when empty / absent.
    /// </summary>
    private static int ParseRepeat(string value, int defaultValue)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        if (value == "unlimited") return -1;
        return int.TryParse(value, out var n) ? n : defaultValue;
    }
}
