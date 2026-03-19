// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlSourceLocationService.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Stateless service that maps between design-canvas UIDs and
//     source-XAML line numbers, enabling bidirectional sync between
//     the canvas selection and the code editor caret.
//
// Architecture Notes:
//     Service pattern — pure functions, no shared state.
//     FindElementStartLine: XDocument + IXmlLineInfo (SetLineInfo option).
//     FindUidAtLine:        XmlReader + IXmlLineInfo for open/close tracking,
//                           building (uid, startLine, endLine) tuples and
//                           returning the innermost element that spans the line.
//     Both methods skip XAML property elements (names containing '.') exactly
//     as DesignToXamlSyncService.InjectUids does, keeping UID numbering consistent.
// ==========================================================

using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Maps design-canvas UIDs to XAML source line numbers and vice versa,
/// enabling bidirectional canvas ↔ code editor selection sync.
/// All methods are stateless and return -1 on failure.
/// </summary>
public sealed class XamlSourceLocationService
{
    // ── Canvas → Code ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the 0-based start line of the element at pre-order position
    /// <paramref name="uid"/> in <paramref name="rawXaml"/>.
    /// Returns -1 when the element is not found or on any parse error.
    /// </summary>
    public int FindElementStartLine(string rawXaml, int uid)
    {
        if (uid < 0 || string.IsNullOrWhiteSpace(rawXaml)) return -1;
        try
        {
            var doc = XDocument.Parse(rawXaml,
                LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);

            int counter = 0;
            var el      = FindByPreOrder(doc.Root, uid, ref counter);
            if (el is null) return -1;

            var lineInfo = (IXmlLineInfo)el;
            return lineInfo.HasLineInfo() ? lineInfo.LineNumber - 1 : -1;
        }
        catch
        {
            return -1;
        }
    }

    // ── Code → Canvas ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the UID of the innermost XAML element whose source range contains
    /// the given 0-based <paramref name="line"/> in <paramref name="rawXaml"/>.
    /// Returns -1 when no element spans that line or on any parse error.
    /// </summary>
    public int FindUidAtLine(string rawXaml, int line)
    {
        if (line < 0 || string.IsNullOrWhiteSpace(rawXaml)) return -1;
        try
        {
            var ranges = BuildUidLineRanges(rawXaml);

            // Find the deepest (last inserted, largest startLine) element that covers the line.
            int bestUid       = -1;
            int bestStartLine = -1;

            foreach (var (uid, startLine, endLine) in ranges)
            {
                if (startLine <= line && line <= endLine && startLine >= bestStartLine)
                {
                    bestUid       = uid;
                    bestStartLine = startLine;
                }
            }

            return bestUid;
        }
        catch
        {
            return -1;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Pre-order traversal matching InjectUids logic: skips property elements
    /// (names containing '.') so UIDs are identical to those injected by
    /// <see cref="DesignToXamlSyncService.InjectUids"/>.
    /// </summary>
    private static XElement? FindByPreOrder(XElement? el, int uid, ref int counter)
    {
        if (el is null) return null;

        bool isPropertyElement = el.Name.LocalName.Contains('.');
        if (!isPropertyElement)
        {
            if (counter == uid) return el;
            counter++;
        }

        foreach (var child in el.Elements())
        {
            var found = FindByPreOrder(child, uid, ref counter);
            if (found is not null) return found;
        }

        return null;
    }

    /// <summary>
    /// Walks the XAML using XmlReader to collect (uid, startLine, endLine)
    /// for every non-property element in pre-order. Lines are 0-based.
    /// </summary>
    private static List<(int Uid, int StartLine, int EndLine)> BuildUidLineRanges(string rawXaml)
    {
        var result  = new List<(int, int, int)>();
        var stack   = new Stack<(int Uid, int StartLine)>();
        int counter = 0;

        var settings = new XmlReaderSettings
        {
            DtdProcessing  = DtdProcessing.Ignore,
            ConformanceLevel = ConformanceLevel.Document
        };

        using var reader   = XmlReader.Create(new StringReader(rawXaml), settings);
        var       lineInfo = (IXmlLineInfo)reader;

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                {
                    bool isPropertyElement = reader.LocalName.Contains('.');
                    if (isPropertyElement) break;

                    int startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber - 1 : 0;

                    if (reader.IsEmptyElement)
                    {
                        result.Add((counter, startLine, startLine));
                        counter++;
                    }
                    else
                    {
                        stack.Push((counter, startLine));
                        counter++;
                    }
                    break;
                }

                case XmlNodeType.EndElement:
                {
                    bool isPropertyElement = reader.LocalName.Contains('.');
                    if (isPropertyElement || stack.Count == 0) break;

                    var (uid, startLine) = stack.Pop();
                    int endLine          = lineInfo.HasLineInfo() ? lineInfo.LineNumber - 1 : startLine;
                    result.Add((uid, startLine, endLine));
                    break;
                }
            }
        }

        // Flush any unclosed elements (malformed XAML) using their start line as end.
        while (stack.Count > 0)
        {
            var (uid, startLine) = stack.Pop();
            result.Add((uid, startLine, startLine));
        }

        return result;
    }
}
