// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Schema/DocumentSchemaDefinition.cs
// Description:
//     C# model for the "documentSchema" top-level section of a .whfmt file.
//     Drives OoXmlSchemaEngine and RtfSchemaEngine — no hardcoded format
//     rules in C#. All block mapping, serialization, and attribute rules
//     come from the .whfmt file's documentSchema section.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Schema;

/// <summary>
/// Parsed representation of the <c>documentSchema</c> JSON section in a .whfmt file.
/// </summary>
public sealed class DocumentSchemaDefinition
{
    /// <summary>Engine type: "ooxml", "odf", or "rtf".</summary>
    public string Engine { get; set; } = string.Empty;

    /// <summary>ZIP entry path for the body XML (DOCX: "word/document.xml", ODT: "content.xml").</summary>
    public string ContentEntry { get; set; } = string.Empty;

    /// <summary>Primary XML namespace URI (OOXML/ODF formats).</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Primary namespace prefix (e.g. "w" for DOCX, "text" for ODT).</summary>
    public string NamespacePrefix { get; set; } = string.Empty;

    /// <summary>Additional namespace prefix→URI mappings (ODF formats).</summary>
    public Dictionary<string, string> Namespaces { get; set; } = [];

    /// <summary>How XML elements / RTF groups map to DocumentBlock kinds.</summary>
    public List<BlockMappingRule> BlockMappings { get; set; } = [];

    /// <summary>How DocumentBlock kinds serialize back to XML/RTF tokens.</summary>
    public Dictionary<string, SerializationRule> SerializationRules { get; set; } = [];

    /// <summary>How DocumentBlock attributes serialize to XML elements / RTF tokens.</summary>
    public Dictionary<string, AttributeSerializationRule> AttributeSerializationRules { get; set; } = [];
}

/// <summary>Maps an XML element or RTF group to a <see cref="DocumentBlock.Kind"/>.</summary>
public sealed class BlockMappingRule
{
    public string XmlElement   { get; set; } = string.Empty;
    public string RtfGroup     { get; set; } = string.Empty;
    public string BlockKind    { get; set; } = string.Empty;
    public string TextSource   { get; set; } = string.Empty;

    public List<AttributeMappingRule>  AttributeMappings { get; set; } = [];
    public List<BlockMappingRule>      ChildMappings     { get; set; } = [];

    /// <summary>For image blocks: ZIP entry path template with {r:embed} substitution.</summary>
    public string ImageEntryPath { get; set; } = string.Empty;
}

/// <summary>Maps an XML attribute path to a <see cref="DocumentBlock.Attributes"/> key.</summary>
public sealed class AttributeMappingRule
{
    public string XmlPath          { get; set; } = string.Empty;
    public string XmlAttr          { get; set; } = string.Empty;
    public string Attribute        { get; set; } = string.Empty;
    public string Transform        { get; set; } = string.Empty;
    public bool   Presence         { get; set; }
    public bool   StyleResolution  { get; set; }
}

/// <summary>How a <see cref="DocumentBlock.Kind"/> maps to output XML/RTF.</summary>
public sealed class SerializationRule
{
    public string XmlElement   { get; set; } = string.Empty;
    public string TextElement  { get; set; } = string.Empty;
    public bool   WrapChildren { get; set; }
    public string PStyle       { get; set; } = string.Empty;
    public string Attr         { get; set; } = string.Empty;
    public string AttrSource   { get; set; } = string.Empty;
    public string Prefix       { get; set; } = string.Empty;
    public string Suffix       { get; set; } = string.Empty;
}

/// <summary>How a <see cref="DocumentBlock.Attributes"/> key maps to output XML/RTF markup.</summary>
public sealed class AttributeSerializationRule
{
    // OOXML
    public string Parent      { get; set; } = string.Empty;
    public string XmlElement  { get; set; } = string.Empty;
    public string Attr        { get; set; } = string.Empty;
    public Dictionary<string, string> Attrs { get; set; } = [];
    public string Transform   { get; set; } = string.Empty;

    // ODF
    public string StyleProperty { get; set; } = string.Empty;
    public string Value         { get; set; } = string.Empty;
    public string Unit          { get; set; } = string.Empty;

    // RTF
    public string Wrap    { get; set; } = string.Empty;
    public string Left    { get; set; } = string.Empty;
    public string Center  { get; set; } = string.Empty;
    public string Right   { get; set; } = string.Empty;
    public string Justify { get; set; } = string.Empty;
}
