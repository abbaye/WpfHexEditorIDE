// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Services/CodeGen/XamlClassInsertService.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     Inserts an x:Class attribute into a XAML root element that does not
//     currently have one, enabling code-behind generation for that file.
//     Used by the CodeGen panel's "Enable Code Generation" action.
//
// Architecture Notes:
//     Pure service — stateless, returns the mutated XAML string.
//     Uses XLinq to locate the root element and inject the attribute.
//     Ensures the x: namespace prefix is declared on the root if absent.
// ==========================================================

using System.Xml.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.Services.CodeGen;

/// <summary>
/// Inserts <c>x:Class="Namespace.ClassName"</c> into a XAML root element.
/// </summary>
public sealed class XamlClassInsertService
{
    private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// Returns the XAML source with <c>x:Class="{ns}.{className}"</c> inserted
    /// on the root element. If x:Class already exists, the existing value is preserved.
    /// Returns null when the XAML cannot be parsed.
    /// </summary>
    public string? InsertXClass(string xamlSource, string ns, string className)
    {
        if (string.IsNullOrWhiteSpace(xamlSource) ||
            string.IsNullOrWhiteSpace(ns)         ||
            string.IsNullOrWhiteSpace(className))
            return null;

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xamlSource, LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return null;
        }

        var root = doc.Root;
        if (root is null)
            return null;

        // Already has x:Class — leave it alone.
        if (root.Attribute(XNs + "Class") is not null)
            return xamlSource;

        // Ensure the x: namespace is declared.
        var xDecl = root.Attributes()
                         .FirstOrDefault(a => a.IsNamespaceDeclaration &&
                                              a.Name.LocalName == "x" &&
                                              a.Value == XNs.NamespaceName);
        if (xDecl is null)
        {
            root.Add(new XAttribute(XNamespace.Xmlns + "x", XNs.NamespaceName));
        }

        // Insert x:Class as the first attribute after namespace declarations.
        root.AddFirst(new XAttribute(XNs + "Class", $"{ns}.{className}"));

        var sb = new System.Text.StringBuilder();
        using var writer = System.Xml.XmlWriter.Create(sb, new System.Xml.XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent             = false,
            NewLineHandling    = System.Xml.NewLineHandling.None
        });
        doc.WriteTo(writer);
        writer.Flush();
        return sb.ToString();
    }

    /// <summary>
    /// Derives a reasonable class name from the XAML file name.
    /// E.g. "MyView.xaml" → "MyView".
    /// </summary>
    public static string ClassNameFromFilePath(string xamlFilePath)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(xamlFilePath);
        // Remove ".xaml" suffix if present (handles "Foo.xaml" → "Foo").
        if (name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];
        return name.Length > 0 ? name : "MyView";
    }
}
