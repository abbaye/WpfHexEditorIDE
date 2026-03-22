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
using System.Linq;
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

                    // Only inject Tag on FrameworkElement / FrameworkContentElement types.
                    // Freezable types (transforms, brushes, geometries, animations) and
                    // style-related types (Style, Setter, Trigger…) do NOT have a Tag property;
                    // injecting Tag on them causes XamlParseException at the next render.
                    if (ElementSupportsTagAttribute(render_el.Name.LocalName))
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
    /// Appends a child XAML element snippet to the root element's content.
    /// Returns the modified XAML string, or null on parse error.
    /// </summary>
    /// <param name="rawXaml">Current XAML source.</param>
    /// <param name="childXaml">Complete element snippet to inject (e.g. "&lt;Rectangle .../&gt;").</param>
    public string? InjectChildElement(string rawXaml, string childXaml)
    {
        if (string.IsNullOrWhiteSpace(rawXaml) || string.IsNullOrWhiteSpace(childXaml))
            return null;

        try
        {
            var doc  = System.Xml.Linq.XDocument.Parse(rawXaml, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root is null) return null;

            var childDoc   = System.Xml.Linq.XDocument.Parse($"<root xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">{childXaml}</root>");
            var childEl    = childDoc.Root?.Elements().FirstOrDefault();
            if (childEl is null) return null;

            // Reparent into root's namespace context.
            childEl.Remove();
            root.Add(childEl);

            return doc.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        }
        catch
        {
            return null;
        }
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
    /// Returns <see langword="true"/> when a WPF element with local name
    /// <paramref name="localName"/> supports the <c>Tag</c> property
    /// (i.e. it derives from <c>FrameworkElement</c> or <c>FrameworkContentElement</c>).
    ///
    /// Freezable types (transforms, brushes, geometries, animations…) and
    /// style/template infrastructure types (Style, Setter, Trigger…) do NOT
    /// have a <c>Tag</c> property — injecting one causes a
    /// <see cref="System.Windows.Markup.XamlParseException"/> at the next render.
    /// </summary>
    private static bool ElementSupportsTagAttribute(string localName)
    {
        // Transforms — all WPF transform types end with "Transform" or are "TransformGroup"
        if (localName.EndsWith("Transform", StringComparison.Ordinal)) return false;
        if (localName == "TransformGroup")                              return false;

        // Brushes — SolidColorBrush, LinearGradientBrush, RadialGradientBrush, …
        if (localName.EndsWith("Brush", StringComparison.Ordinal))     return false;

        // Animations & key-frames — DoubleAnimation, ColorAnimation, …KeyFrame, …Timeline
        if (localName.EndsWith("Animation", StringComparison.Ordinal)) return false;
        if (localName.EndsWith("KeyFrame",  StringComparison.Ordinal)) return false;
        if (localName.EndsWith("Timeline",  StringComparison.Ordinal)) return false;
        if (localName == "Storyboard")                                  return false;

        // Geometry & path primitives
        if (localName.EndsWith("Geometry", StringComparison.Ordinal))  return false;
        if (localName.EndsWith("Segment",  StringComparison.Ordinal))  return false;
        if (localName == "PathFigure")                                  return false;

        // Drawing objects (Freezable)
        if (localName.EndsWith("Drawing", StringComparison.Ordinal))   return false;

        // Visual effects (Freezable)
        if (localName.EndsWith("Effect", StringComparison.Ordinal))    return false;

        // Gradient stop (child of gradient brush)
        if (localName == "GradientStop")                                return false;

        // Style infrastructure — Style, Setter, *Trigger, *Template, Condition
        if (localName == "Style")                                       return false;
        if (localName == "Setter")                                      return false;
        if (localName.EndsWith("Trigger",   StringComparison.Ordinal)) return false;
        if (localName.EndsWith("Template",  StringComparison.Ordinal)) return false;
        if (localName.EndsWith("Condition", StringComparison.Ordinal)) return false;

        // Resources container
        if (localName == "ResourceDictionary")                          return false;

        // Input bindings & gestures — InputBinding → Freezable (no Tag)
        // Covers: KeyBinding, MouseBinding, InputBinding, CommandBinding,
        //         MultiBinding, PriorityBinding, Binding (as element), …
        if (localName.EndsWith("Binding", StringComparison.Ordinal))   return false;
        // Covers: KeyGesture, MouseGesture, InputGesture
        if (localName.EndsWith("Gesture", StringComparison.Ordinal))   return false;

        // Style setters beyond "Setter" — EventSetter, etc.
        if (localName.EndsWith("Setter", StringComparison.Ordinal))    return false;

        // Storyboard trigger-actions — BeginStoryboard, StopStoryboard, …
        // (TriggerAction → DependencyObject, no Tag)
        if (localName.EndsWith("Storyboard", StringComparison.Ordinal) &&
            localName != "Storyboard")                                  return false;

        // ListView/GridView infrastructure.
        // GridView and GridViewColumn derive from ViewBase/DependencyObject — no Tag DP.
        if (localName == "GridView")                                    return false;
        if (localName == "GridViewColumn")                              return false;

        // DataGrid column types (DataGridTextColumn, DataGridCheckBoxColumn, …)
        // All derive from DataGridColumn → DependencyObject — no Tag DP.
        if (localName.StartsWith("DataGrid", StringComparison.Ordinal) &&
            localName.EndsWith("Column",     StringComparison.Ordinal)) return false;

        // WPF value-type / struct elements that appear in resource dictionaries.
        // These are NOT FrameworkElements — they have no Tag dependency property.
        if (localName == "Color")                                       return false;
        if (localName == "Point")                                       return false;
        if (localName == "Rect")                                        return false;
        if (localName == "Size")                                        return false;
        if (localName == "Thickness")                                   return false;
        if (localName == "CornerRadius")                                return false;
        if (localName == "FontFamily")                                  return false;
        if (localName == "Duration")                                    return false;
        if (localName == "KeySpline")                                   return false;

        return true;
    }

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
        // Mirror the traversal in InjectUids: property elements (e.g. <Button.Style>,
        // <Grid.RowDefinitions>) are NOT counted, but their children still are.
        bool isPropertyElement = el.Name.LocalName.Contains('.');
        if (!isPropertyElement)
        {
            if (counter == uid) return el;
            counter++;
        }
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
        bool isPropertyElement = root.Name.LocalName.Contains('.');
        if (!isPropertyElement)
        {
            if (counter == uid) return root;
            counter++;
        }
        foreach (var child in root.Elements())
        {
            var found = FindUidForRemoval(child, uid, ref counter);
            if (found is not null) return found;
        }
        return null;
    }
}
