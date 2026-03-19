// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignToXamlSyncService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Converts design-surface interactions into XAML text mutations.
//     Given a raw XAML string, an element UID, and a set of attribute
//     changes, returns the updated XAML string with those changes applied.
//
// Architecture Notes:
//     Service pattern — stateless, pure function surface.
//     Uses XLinq (System.Xml.Linq) for structural patching.
//     Element lookup uses pre-order UID traversal matching the UID
//     injection performed by DesignCanvas during render.
//     Preserves all whitespace and unrelated attributes.
// ==========================================================

using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Applies XAML attribute patches derived from design-surface interactions.
/// All methods are stateless — they receive the full XAML text and return
/// an updated copy without mutating any shared state.
/// </summary>
public sealed class DesignToXamlSyncService
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Locates the element at pre-order position <paramref name="elementUid"/> in
    /// <paramref name="rawXaml"/>, applies <paramref name="changes"/>, and returns
    /// the updated XAML string. Returns <paramref name="rawXaml"/> unchanged on error.
    /// </summary>
    /// <param name="rawXaml">Full XAML document text.</param>
    /// <param name="elementUid">Zero-based pre-order index of the target element.</param>
    /// <param name="changes">
    /// Attribute changes: key = attribute local name (no namespace prefix),
    /// value = new string value, or null to remove the attribute.
    /// </param>
    public string PatchElement(
        string rawXaml,
        int elementUid,
        IReadOnlyDictionary<string, string?> changes)
    {
        if (string.IsNullOrWhiteSpace(rawXaml) || changes.Count == 0)
            return rawXaml;

        try
        {
            var doc    = XDocument.Parse(rawXaml, LoadOptions.PreserveWhitespace);
            var target = FindElementByUid(doc.Root, elementUid);

            if (target is null)
                return rawXaml;

            ApplyAttributeChanges(target, changes);

            // Serialize preserving as much whitespace / namespace declaration order as possible.
            var sb = new StringBuilder();
            using var writer = System.Xml.XmlWriter.Create(
                sb,
                new System.Xml.XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Indent             = false,
                    NewLineHandling    = System.Xml.NewLineHandling.None
                });
            doc.WriteTo(writer);
            writer.Flush();

            return sb.ToString();
        }
        catch
        {
            return rawXaml;
        }
    }

    /// <summary>
    /// Injects <c>Tag="xd_N"</c> on every element in <paramref name="xaml"/>
    /// (pre-order traversal, starting at 0) and returns the modified XAML string.
    /// Also populates <paramref name="uidMap"/> with uid → XElement mappings
    /// from the *original* (pre-injection) XDocument so callers can later
    /// apply patches via <see cref="PatchElement"/>.
    /// Returns <paramref name="xaml"/> unchanged on parse error.
    /// </summary>
    public string InjectUids(string xaml, out Dictionary<int, XElement> uidMap)
    {
        uidMap = new Dictionary<int, XElement>();

        if (string.IsNullOrWhiteSpace(xaml))
            return xaml;

        try
        {
            var original  = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            var forRender = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);

            int counter = 0;
            // Copy out-param reference to a local so the local function can capture it.
            var map = uidMap;

            void Walk(XElement original_el, XElement render_el)
            {
                // XAML property elements (e.g. <Button.Style>, <Grid.RowDefinitions>)
                // are XML elements but NOT WPF UIElements — Tag attribute is invalid there
                // and would cause XamlParseException (NonemptyPropertyElement rule).
                bool isPropertyElement = render_el.Name.LocalName.Contains('.');
                if (!isPropertyElement)
                {
                    map[counter] = original_el;
                    render_el.SetAttributeValue("Tag", $"xd_{counter}");
                    counter++;
                }

                var origChildren   = original_el.Elements().ToList();
                var renderChildren = render_el.Elements().ToList();

                int count = Math.Min(origChildren.Count, renderChildren.Count);
                for (int i = 0; i < count; i++)
                    Walk(origChildren[i], renderChildren[i]);
            }

            if (original.Root is not null && forRender.Root is not null)
                Walk(original.Root, forRender.Root);

            var sb = new StringBuilder();
            using var writer = System.Xml.XmlWriter.Create(
                sb,
                new System.Xml.XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Indent             = false,
                    NewLineHandling    = System.Xml.NewLineHandling.None
                });
            forRender.WriteTo(writer);
            writer.Flush();

            return sb.ToString();
        }
        catch
        {
            return xaml;
        }
    }

    /// <summary>
    /// Applies the forward (after) state of a design operation to the given XAML string.
    /// Routes structural operations (e.g. Rotate) to dedicated patch methods instead of
    /// the generic attribute-only <see cref="PatchElement"/>.
    /// </summary>
    public string ApplyOperation(string rawXaml, Models.DesignOperation op)
    {
        if (op.Type == Models.DesignOperationType.Rotate)
        {
            op.After.TryGetValue("Angle", out var angle);
            return PatchRotation(rawXaml, op.ElementUid, angle ?? "0");
        }
        return PatchElement(rawXaml, op.ElementUid, op.After);
    }

    /// <summary>
    /// Applies the inverse of <paramref name="op"/> to produce an undo XAML string.
    /// Uses the <c>Before</c> dictionary of the operation.
    /// </summary>
    public string ApplyUndo(string rawXaml, Models.DesignOperation op)
    {
        if (op.Type == Models.DesignOperationType.Rotate)
        {
            op.Before.TryGetValue("Angle", out var angle);
            return PatchRotation(rawXaml, op.ElementUid, angle ?? "0");
        }
        return PatchElement(rawXaml, op.ElementUid, op.Before);
    }

    /// <summary>
    /// Applies the forward state of <paramref name="op"/> to produce a redo XAML string.
    /// Uses the <c>After</c> dictionary of the operation.
    /// </summary>
    public string ApplyRedo(string rawXaml, Models.DesignOperation op)
    {
        if (op.Type == Models.DesignOperationType.Rotate)
        {
            op.After.TryGetValue("Angle", out var angle);
            return PatchRotation(rawXaml, op.ElementUid, angle ?? "0");
        }
        return PatchElement(rawXaml, op.ElementUid, op.After);
    }

    /// <summary>
    /// Applies the <c>Before</c> state of each operation in REVERSE order,
    /// restoring XAML to the state before the batch was applied.
    /// Reversal ensures each PatchElement call operates on a consistent UID mapping.
    /// </summary>
    public string ApplyBatchUndo(string rawXaml, IReadOnlyList<Models.DesignOperation> ops)
    {
        if (string.IsNullOrWhiteSpace(rawXaml) || ops.Count == 0) return rawXaml;
        for (int i = ops.Count - 1; i >= 0; i--)
            rawXaml = PatchElement(rawXaml, ops[i].ElementUid, ops[i].Before);
        return rawXaml;
    }

    /// <summary>
    /// Applies the <c>After</c> state of each operation in FORWARD order,
    /// re-applying the batch when redoing.
    /// </summary>
    public string ApplyBatchRedo(string rawXaml, IReadOnlyList<Models.DesignOperation> ops)
    {
        if (string.IsNullOrWhiteSpace(rawXaml) || ops.Count == 0) return rawXaml;
        foreach (var op in ops)
            rawXaml = PatchElement(rawXaml, op.ElementUid, op.After);
        return rawXaml;
    }

    /// <summary>
    /// Removes the element at pre-order position <paramref name="elementUid"/> from
    /// <paramref name="rawXaml"/> and returns the updated XAML string.
    /// Returns <paramref name="rawXaml"/> unchanged when the element is not found or on error.
    /// Used to implement the Delete key in Design/Split mode.
    /// </summary>
    public string RemoveElement(string rawXaml, int elementUid)
    {
        if (string.IsNullOrWhiteSpace(rawXaml)) return rawXaml;
        try
        {
            var doc     = XDocument.Parse(rawXaml, LoadOptions.PreserveWhitespace);
            int counter = 0;
            var target  = FindUidForRemoval(doc.Root, elementUid, ref counter);

            if (target is null) return rawXaml;

            target.Remove();

            var sb = new StringBuilder();
            using var writer = System.Xml.XmlWriter.Create(
                sb,
                new System.Xml.XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Indent             = false,
                    NewLineHandling    = System.Xml.NewLineHandling.None
                });
            doc.WriteTo(writer);
            writer.Flush();
            return sb.ToString();
        }
        catch
        {
            return rawXaml;
        }
    }

    /// <summary>
    /// Wraps the element at <paramref name="elementUid"/> inside a new container element.
    /// Example: wraps a Button in a Border → &lt;Border&gt;&lt;Button .../&gt;&lt;/Border&gt;.
    /// Returns <paramref name="rawXaml"/> unchanged on error or when element is not found.
    /// </summary>
    public string WrapInContainer(string rawXaml, int elementUid, string containerTag)
    {
        if (string.IsNullOrWhiteSpace(rawXaml) || string.IsNullOrWhiteSpace(containerTag))
            return rawXaml;
        try
        {
            var doc    = XDocument.Parse(rawXaml, LoadOptions.PreserveWhitespace);
            var target = FindElementByUid(doc.Root, elementUid);
            if (target?.Parent is null) return rawXaml;

            // Use the same namespace as the document root so XAML parsers resolve it correctly.
            var ns        = doc.Root!.Name.Namespace;
            var container = new XElement(ns + containerTag, new XElement(target));
            target.ReplaceWith(container);

            return Serialize(doc);
        }
        catch
        {
            return rawXaml;
        }
    }

    /// <summary>
    /// Removes the container element at <paramref name="elementUid"/> and promotes its
    /// children directly to the container's parent.
    /// Property elements (names containing '.') are excluded from promotion.
    /// Returns <paramref name="rawXaml"/> unchanged on error or when element is not found.
    /// </summary>
    public string UnwrapContainer(string rawXaml, int elementUid)
    {
        if (string.IsNullOrWhiteSpace(rawXaml)) return rawXaml;
        try
        {
            var doc    = XDocument.Parse(rawXaml, LoadOptions.PreserveWhitespace);
            var target = FindElementByUid(doc.Root, elementUid);
            if (target?.Parent is null) return rawXaml;

            // Collect UIElement children only (skip XAML property elements such as <Grid.RowDefinitions>).
            var children = target.Elements()
                .Where(e => !e.Name.LocalName.Contains('.'))
                .Select(e => new XElement(e))   // clone before removal
                .ToList();

            // Insert promoted children at the target's position, then remove the container.
            foreach (var child in children)
                target.AddBeforeSelf(child);
            target.Remove();

            return Serialize(doc);
        }
        catch
        {
            return rawXaml;
        }
    }

    /// <summary>
    /// Returns the current string value of <paramref name="attrName"/> on the element
    /// at pre-order position <paramref name="elementUid"/> in <paramref name="rawXaml"/>,
    /// or <c>null</c> when the attribute is absent or the element is not found.
    /// Used by callers to capture the "before" value for lightweight undo entries.
    /// </summary>
    public string? ReadAttributeValue(string rawXaml, int elementUid, string attrName)
    {
        if (string.IsNullOrWhiteSpace(rawXaml)) return null;
        try
        {
            var doc    = XDocument.Parse(rawXaml, LoadOptions.PreserveWhitespace);
            var target = FindElementByUid(doc.Root, elementUid);
            return target?.Attribute(attrName)?.Value;
        }
        catch
        {
            return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Patches the element at <paramref name="uid"/> to add or remove a
    /// <c>&lt;Element.RenderTransform&gt;&lt;RotateTransform Angle="X"/&gt;&lt;/Element.RenderTransform&gt;</c>
    /// child and the corresponding <c>RenderTransformOrigin="0.5,0.5"</c> attribute.
    /// When <paramref name="angleDeg"/> rounds to zero, both are removed.
    /// </summary>
    private static string PatchRotation(string rawXaml, int uid, string angleDeg)
    {
        // uid < 0 means the rotation started without a valid element (DragStarted didn't fire).
        if (string.IsNullOrWhiteSpace(rawXaml) || uid < 0) return rawXaml;
        try
        {
            var doc    = XDocument.Parse(rawXaml, LoadOptions.PreserveWhitespace);
            var target = FindElementByUid(doc.Root, uid);
            if (target is null) return rawXaml;

            // Use the document root namespace for all generated elements.
            // This avoids XLinq emitting redundant xmlns= attributes inside property elements.
            var ns = doc.Root!.Name.Namespace;

            bool removeRotation = !double.TryParse(angleDeg, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double angle)
                || Math.Abs(angle) < 0.01;

            // Remove ALL existing RenderTransform property elements (may be more than one on malformed XAML).
            target.Elements()
                  .Where(e => e.Name.LocalName.EndsWith(".RenderTransform"))
                  .ToList()
                  .ForEach(e => e.Remove());

            if (removeRotation)
            {
                target.SetAttributeValue("RenderTransformOrigin", null);
            }
            else
            {
                target.SetAttributeValue("RenderTransformOrigin", "0.5,0.5");

                // <TypeName.RenderTransform><RotateTransform Angle="X"/></TypeName.RenderTransform>
                var rt = new XElement(
                    ns + (target.Name.LocalName + ".RenderTransform"),
                    new XElement(ns + "RotateTransform",
                        new XAttribute("Angle", angleDeg)));

                target.AddFirst(rt);
            }

            return Serialize(doc);
        }
        catch
        {
            return rawXaml;
        }
    }

    /// <summary>Serializes an XDocument to a XAML-compatible string without an XML declaration.</summary>
    private static string Serialize(XDocument doc)
    {
        var sb = new StringBuilder();
        using var writer = System.Xml.XmlWriter.Create(
            sb,
            new System.Xml.XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent             = false,
                NewLineHandling    = System.Xml.NewLineHandling.None
            });
        doc.WriteTo(writer);
        writer.Flush();
        return sb.ToString();
    }

    private static XElement? FindElementByUid(XElement? root, int uid)
    {
        if (root is null) return null;
        int counter = 0;
        return FindUid(root, uid, ref counter);
    }

    private static XElement? FindUid(XElement el, int uid, ref int counter)
    {
        if (counter == uid) return el;
        counter++;
        foreach (var child in el.Elements())
        {
            var found = FindUid(child, uid, ref counter);
            if (found is not null) return found;
        }
        return null;
    }

    private static void ApplyAttributeChanges(XElement target, IReadOnlyDictionary<string, string?> changes)
    {
        foreach (var (key, val) in changes)
        {
            if (val is null)
                target.Attribute(key)?.Remove();
            else
                target.SetAttributeValue(key, val);
        }
    }

    /// <summary>
    /// Inline pre-order UID traversal used by <see cref="RemoveElement"/>.
    /// Mirrors the traversal in <see cref="FindUid"/> but is kept separate
    /// to avoid exposing private implementation details.
    /// </summary>
    private static XElement? FindUidForRemoval(XElement? root, int uid, ref int counter)
    {
        if (root is null) return null;
        if (counter == uid) return root;
        counter++;
        foreach (var child in root.Elements())
        {
            var found = FindUidForRemoval(child, uid, ref counter);
            if (found is not null) return found;
        }
        return null;
    }
}
