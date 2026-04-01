// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlReorderService.cs
// Author: Derek Tremblay
// Created: 2026-03-19
// Description:
//     Performs structural XAML reordering operations — move up/down,
//     delete, and wrap in a container — by manipulating XElement nodes
//     and serializing the result back to a XAML source string.
//
// Architecture Notes:
//     Pure service — stateless. All operations return a new XAML string
//     (functional/immutable style); the caller owns the undo entry.
//     XDocument is used with LoadOptions.PreserveWhitespace so indentation
//     and comments survive round-trips.
//     Element navigation uses an index-path notation:
//       "0"           → root element's first child
//       "0/1"         → root[0] → children[1]
//       "0/1[2]"      → not used; flat index after '/' is the child index
//     All manipulations are guarded by try/catch returning null on failure.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Performs structural reordering of XAML element nodes by manipulating
/// an <see cref="XDocument"/> and returning the updated XAML string.
/// </summary>
public sealed class XamlReorderService
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves the element identified by <paramref name="elementPath"/> one
    /// position toward the beginning of its sibling list.
    /// </summary>
    /// <returns>Updated XAML string, or <see langword="null"/> on failure.</returns>
    public string? MoveUp(string xamlSource, string elementPath)
    {
        return TryTransform(xamlSource, elementPath, (element, siblings, index) =>
        {
            if (index == 0)
                return false; // Already first.

            XNode predecessor = siblings[index - 1];
            element.Remove();
            predecessor.AddBeforeSelf(element);
            return true;
        });
    }

    /// <summary>
    /// Moves the element identified by <paramref name="elementPath"/> one
    /// position toward the end of its sibling list.
    /// </summary>
    /// <returns>Updated XAML string, or <see langword="null"/> on failure.</returns>
    public string? MoveDown(string xamlSource, string elementPath)
    {
        return TryTransform(xamlSource, elementPath, (element, siblings, index) =>
        {
            if (index == siblings.Count - 1)
                return false; // Already last.

            XNode successor = siblings[index + 1];
            element.Remove();
            successor.AddAfterSelf(element);
            return true;
        });
    }

    /// <summary>
    /// Removes the element identified by <paramref name="elementPath"/>
    /// from the document.
    /// </summary>
    /// <returns>Updated XAML string, or <see langword="null"/> on failure.</returns>
    public string? DeleteElement(string xamlSource, string elementPath)
    {
        return TryTransform(xamlSource, elementPath, (element, _, _) =>
        {
            element.Remove();
            return true;
        });
    }

    /// <summary>
    /// Wraps the element identified by <paramref name="elementPath"/> inside
    /// a new <paramref name="containerTag"/> element.
    /// </summary>
    /// <param name="containerTag">
    /// The local XML name for the wrapping container (e.g. <c>"Grid"</c>,
    /// <c>"Border"</c>).  Must be a valid XML NCName.
    /// </param>
    /// <returns>Updated XAML string, or <see langword="null"/> on failure.</returns>
    public string? WrapIn(string xamlSource, string elementPath, string containerTag)
    {
        if (string.IsNullOrWhiteSpace(containerTag))
            return null;

        return TryTransform(xamlSource, elementPath, (element, _, _) =>
        {
            // Inherit the default namespace from the element being wrapped so the
            // output remains well-formed without spurious xmlns declarations.
            XNamespace ns = element.Name.Namespace;
            var container = new XElement(ns + containerTag, element);

            element.ReplaceWith(container);
            return true;
        });
    }

    // ── Private: transform orchestration ─────────────────────────────────────

    /// <summary>
    /// Parses the source, locates the target element, invokes the
    /// <paramref name="mutation"/> delegate, and serializes the result.
    /// Returns <see langword="null"/> at any failure point.
    /// </summary>
    private static string? TryTransform(
        string xamlSource,
        string elementPath,
        Func<XElement, IReadOnlyList<XElement>, int, bool> mutation)
    {
        if (string.IsNullOrWhiteSpace(xamlSource) || string.IsNullOrWhiteSpace(elementPath))
            return null;

        try
        {
            XDocument document = XDocument.Parse(xamlSource, LoadOptions.PreserveWhitespace);

            if (!TryNavigate(document, elementPath, out XElement? element) || element is null)
                return null;

            // Build the sibling list (element children only) for index operations.
            XElement? parent = element.Parent;
            if (parent is null)
                return null; // Cannot reorder the root.

            List<XElement> siblings = parent.Elements().ToList();
            int index = siblings.IndexOf(element);
            if (index < 0)
                return null;

            bool mutated = mutation(element, siblings, index);
            return mutated ? document.ToString(SaveOptions.None) : null;
        }
        catch
        {
            return null;
        }
    }

    // ── Private: path navigation ──────────────────────────────────────────────

    /// <summary>
    /// Navigates an <see cref="XDocument"/> using a slash-separated path of
    /// zero-based child element indices.
    /// </summary>
    /// <example>
    /// Path <c>"0"</c>        → root element's first child element.<br/>
    /// Path <c>"0/2"</c>      → root[0], then its third child element.<br/>
    /// Path <c>"0/1/0"</c>    → three levels deep.
    /// </example>
    private static bool TryNavigate(
        XDocument document,
        string path,
        out XElement? result)
    {
        result = null;

        if (document.Root is null)
            return false;

        // The path is relative to the document root's children.
        XElement current = document.Root;

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            if (!TryParseIndex(segment, out int index))
                return false;

            List<XElement> children = current.Elements().ToList();
            if (index < 0 || index >= children.Count)
                return false;

            current = children[index];
        }

        // If the path had no segments, we'd be pointing at the root — which has
        // no parent and cannot be reordered.  Require at least one segment.
        if (segments.Length == 0)
            return false;

        result = current;
        return true;
    }

    /// <summary>
    /// Parses a path segment like <c>"2"</c> or <c>"[2]"</c> into an integer index.
    /// </summary>
    private static bool TryParseIndex(string segment, out int index)
    {
        index = -1;

        ReadOnlySpan<char> span = segment.AsSpan().Trim();

        // Strip optional brackets: "[2]" → "2"
        if (span.StartsWith("[") && span.EndsWith("]"))
            span = span[1..^1];

        return int.TryParse(span, out index) && index >= 0;
    }
}
