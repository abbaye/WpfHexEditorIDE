// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Services/CodeGen/XamlCodeBehindScanner.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     Scans a XAML source string and extracts the semantic information
//     needed for code-behind generation: x:Class, x:Name elements,
//     event handler sinks, and Style/EventSetter sinks.
//     Returns an immutable XamlCodeModel record.
//
// Architecture Notes:
//     Stateless pure service — no dependencies, no side effects.
//     Uses XLinq (XDocument.Parse) for structural navigation, NOT XamlReader,
//     so no runtime WPF types need to be loaded for scanning.
//     Skips d:* design-time attributes and mc:Ignorable namespace content.
//     C#-identifier sanitization prevents collision with reserved keywords.
// ==========================================================

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services.CodeGen;

/// <summary>
/// Parses XAML source text and returns an immutable <see cref="XamlCodeModel"/>
/// describing all code-generation-relevant declarations.
/// </summary>
public sealed class XamlCodeBehindScanner
{
    // ── Well-known XML namespaces ─────────────────────────────────────────────

    private static readonly XNamespace XNs      = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace XmlNs    = "http://www.w3.org/XML/1998/namespace";
    private static readonly XNamespace DesignNs = "http://schemas.microsoft.com/expression/blend/2008";
    private static readonly XNamespace McNs     = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    // ── C# reserved keyword set ───────────────────────────────────────────────

    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
    {
        "abstract","as","base","bool","break","byte","case","catch","char","checked",
        "class","const","continue","decimal","default","delegate","do","double","else",
        "enum","event","explicit","extern","false","finally","fixed","float","for",
        "foreach","goto","if","implicit","in","int","interface","internal","is","lock",
        "long","namespace","new","null","object","operator","out","override","params",
        "private","protected","public","readonly","ref","return","sbyte","sealed",
        "short","sizeof","stackalloc","static","string","struct","switch","this",
        "throw","true","try","typeof","uint","ulong","unchecked","unsafe","ushort",
        "using","virtual","void","volatile","while"
    };

    // ── Routed-event attribute detection ─────────────────────────────────────

    // Attributes ending in known event name patterns that are not properties.
    // A simple heuristic: PascalCase attribute whose value starts with an uppercase letter
    // and looks like a method name (no spaces, dots, braces).
    private static readonly Regex EventHandlerValuePattern =
        new(@"^[A-Z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    // Attributes that are definitely NOT events (common WPF properties that happen
    // to start uppercase and take string values resembling method names).
    private static readonly HashSet<string> KnownNonEventAttributes = new(StringComparer.Ordinal)
    {
        "Style","Template","Content","Header","Title","Text","Tag","Name","Key",
        "TargetType","BasedOn","DataType","Source","NavigateUri","ToolTip",
        "Width","Height","Margin","Padding","Background","Foreground","BorderBrush",
        "BorderThickness","FontFamily","FontSize","FontWeight","FontStyle","Opacity",
        "Visibility","IsEnabled","IsTabStop","TabIndex","HorizontalAlignment",
        "VerticalAlignment","HorizontalContentAlignment","VerticalContentAlignment",
        "Stretch","StrokeThickness","Fill","Stroke","CornerRadius","ClipToBounds",
        "RenderTransform","RenderTransformOrigin","LayoutTransform",
        "FlowDirection","Language","InputScope","Cursor","SnapsToDevicePixels",
        "UseLayoutRounding","AllowDrop","Focusable","IsHitTestVisible",
        "TextWrapping","TextAlignment","TextTrimming","LineHeight",
        "MinWidth","MinHeight","MaxWidth","MaxHeight","ActualWidth","ActualHeight",
        "Row","Column","RowSpan","ColumnSpan","ZIndex","DockPanel.Dock",
        "Grid.Row","Grid.Column","Grid.RowSpan","Grid.ColumnSpan",
        "Canvas.Left","Canvas.Top","Canvas.Right","Canvas.Bottom",
        "ItemsSource","ItemTemplate","DisplayMemberPath","SelectedItem",
        "SelectedIndex","SelectedValue","SelectedValuePath",
        "Command","CommandParameter","CommandTarget",
        "IsChecked","IsSelected","IsExpanded","IsReadOnly","IsMultiline",
        "Orientation","ScrollViewer.HorizontalScrollBarVisibility",
        "ScrollViewer.VerticalScrollBarVisibility",
        "Image.Source","BitmapImage.UriSource","MediaElement.Source",
        "PasswordChar","MaxLength","AcceptsReturn","AcceptsTab",
        "SpellCheck.IsEnabled","InputMethod.IsInputMethodEnabled",
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <paramref name="xamlSource"/> and returns an immutable
    /// <see cref="XamlCodeModel"/> representing its code-generation surface.
    /// Returns <see cref="XamlCodeModel.Empty"/> on parse failure.
    /// </summary>
    public XamlCodeModel Scan(string xamlSource)
    {
        if (string.IsNullOrWhiteSpace(xamlSource))
            return XamlCodeModel.Empty;

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xamlSource, LoadOptions.SetLineInfo);
        }
        catch
        {
            return XamlCodeModel.Empty;
        }

        var root = doc.Root;
        if (root is null)
            return XamlCodeModel.Empty;

        // Determine which namespaces are declared mc:Ignorable (skip those prefixes).
        var ignorableNs = GetIgnorableNamespaces(root);

        // x:Class → namespace + class name.
        string? fullClass = root.Attribute(XNs + "Class")?.Value;
        string? ns        = null;
        string? className = null;
        if (fullClass is { Length: > 0 })
        {
            int lastDot = fullClass.LastIndexOf('.');
            if (lastDot > 0)
            {
                ns        = fullClass[..lastDot];
                className = fullClass[(lastDot + 1)..];
            }
            else
            {
                className = fullClass;
            }
        }

        string rootTypeName = LocalName(root);

        // Walk all elements.
        var namedElements   = ImmutableArray.CreateBuilder<XamlNamedElement>();
        var eventSinks      = ImmutableArray.CreateBuilder<XamlEventSink>();
        var styleEventSinks = ImmutableArray.CreateBuilder<XamlStyleEventSink>();

        WalkElement(root, ignorableNs, namedElements, eventSinks, styleEventSinks);

        return new XamlCodeModel(
            ns,
            className,
            rootTypeName,
            namedElements.ToImmutable(),
            eventSinks.ToImmutable(),
            styleEventSinks.ToImmutable());
    }

    /// <summary>
    /// Computes the delta between <paramref name="oldModel"/> and <paramref name="newModel"/>.
    /// </summary>
    public CodeBehindPatch Diff(XamlCodeModel oldModel, XamlCodeModel newModel)
    {
        var oldNames = oldModel.NamedElements.ToDictionary(e => e.Name, StringComparer.Ordinal);
        var newNames = newModel.NamedElements.ToDictionary(e => e.Name, StringComparer.Ordinal);

        var added   = newModel.NamedElements.Where(e => !oldNames.ContainsKey(e.Name)).ToImmutableArray();
        var removed = oldModel.NamedElements.Where(e => !newNames.ContainsKey(e.Name)).ToImmutableArray();

        // Rename heuristic: if one element was removed and one added with the same type,
        // treat as a rename rather than add+remove.
        var renamed = ImmutableArray.CreateBuilder<(XamlNamedElement Old, XamlNamedElement New)>();
        var addedFinal   = added.ToBuilder();
        var removedFinal = removed.ToBuilder();

        foreach (var rem in removed)
        {
            var match = addedFinal.FirstOrDefault(a => a.WpfTypeName == rem.WpfTypeName);
            if (match is not null)
            {
                renamed.Add((rem, match));
                addedFinal.Remove(match);
                removedFinal.Remove(rem);
            }
        }

        var oldSinkKeys = oldModel.EventSinks.Select(SinkKey).ToHashSet(StringComparer.Ordinal);
        var newSinkKeys = newModel.EventSinks.Select(SinkKey).ToHashSet(StringComparer.Ordinal);

        var addedSinks   = newModel.EventSinks.Where(s => !oldSinkKeys.Contains(SinkKey(s))).ToImmutableArray();
        var removedSinks = oldModel.EventSinks.Where(s => !newSinkKeys.Contains(SinkKey(s))).ToImmutableArray();

        bool classChanged =
            oldModel.Namespace  != newModel.Namespace ||
            oldModel.ClassName  != newModel.ClassName;

        return new CodeBehindPatch(
            addedFinal.ToImmutableArray(),
            removedFinal.ToImmutableArray(),
            renamed.ToImmutable(),
            addedSinks,
            removedSinks,
            classChanged);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string SinkKey(XamlEventSink s)
        => $"{s.ElementName}::{s.EventAttributeName}::{s.HandlerName}";

    private static HashSet<XNamespace> GetIgnorableNamespaces(XElement root)
    {
        var ignorable = root.Attribute(McNs + "Ignorable")?.Value ?? string.Empty;
        var prefixes  = ignorable.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result    = new HashSet<XNamespace>();

        foreach (var prefix in prefixes)
        {
            // Look up namespace declared for this prefix.
            var nsAttr = root.Attributes()
                .FirstOrDefault(a => a.IsNamespaceDeclaration && a.Name.LocalName == prefix);
            if (nsAttr is not null)
                result.Add(nsAttr.Value);
        }

        // Design-time namespace is always ignored for codegen purposes.
        result.Add(DesignNs);
        return result;
    }

    private static void WalkElement(
        XElement                                     element,
        HashSet<XNamespace>                          ignorableNs,
        ImmutableArray<XamlNamedElement>.Builder     namedElements,
        ImmutableArray<XamlEventSink>.Builder        eventSinks,
        ImmutableArray<XamlStyleEventSink>.Builder   styleEventSinks)
    {
        // Skip elements in ignorable / design-time namespaces.
        if (ignorableNs.Contains(element.Name.Namespace))
            return;

        // Skip pure property-element nodes (e.g. <Button.Style>) — they contain
        // no x:Name or events themselves, but their children do.
        bool isPropertyElement = element.Name.LocalName.Contains('.');

        if (!isPropertyElement)
        {
            string typeName   = LocalName(element);
            int    sourceLine = ((System.Xml.IXmlLineInfo)element).LineNumber;

            // x:Name attribute.
            string? xName = element.Attribute(XNs + "Name")?.Value
                         ?? element.Attribute("Name")?.Value;

            if (xName is { Length: > 0 })
            {
                string safeName = SanitizeIdentifier(xName);
                namedElements.Add(new XamlNamedElement(safeName, typeName, sourceLine));
            }

            // Style/EventSetter detection.
            if (typeName is "Style" or "ControlTemplate" or "DataTemplate")
            {
                ScanForEventSetters(element, ignorableNs, styleEventSinks);
            }

            // Event attribute sinks (non-design-time attributes whose value looks like a handler).
            foreach (var attr in element.Attributes())
            {
                if (attr.IsNamespaceDeclaration) continue;
                if (ignorableNs.Contains(attr.Name.Namespace)) continue;
                if (attr.Name.Namespace == XNs) continue;      // x:Name, x:Key, etc.
                if (attr.Name.Namespace == XmlNs) continue;

                string attrLocal = attr.Name.LocalName;
                if (KnownNonEventAttributes.Contains(attrLocal)) continue;
                if (!EventHandlerValuePattern.IsMatch(attr.Value)) continue;

                // Heuristic: event attributes start with a known verb or follow WPF naming.
                if (!LooksLikeEventAttribute(attrLocal)) continue;

                eventSinks.Add(new XamlEventSink(
                    xName,
                    typeName,
                    attrLocal,
                    attr.Value,
                    sourceLine));
            }
        }

        // Recurse into children.
        foreach (var child in element.Elements())
            WalkElement(child, ignorableNs, namedElements, eventSinks, styleEventSinks);
    }

    private static void ScanForEventSetters(
        XElement                                    styleElement,
        HashSet<XNamespace>                         ignorableNs,
        ImmutableArray<XamlStyleEventSink>.Builder  styleEventSinks)
    {
        foreach (var child in styleElement.Descendants())
        {
            if (ignorableNs.Contains(child.Name.Namespace)) continue;
            if (LocalName(child) is not "EventSetter") continue;

            string? eventName   = child.Attribute("Event")?.Value;
            string? handlerName = child.Attribute("Handler")?.Value;

            if (eventName is { Length: > 0 } && handlerName is { Length: > 0 })
            {
                int line = ((System.Xml.IXmlLineInfo)child).LineNumber;
                styleEventSinks.Add(new XamlStyleEventSink(eventName, handlerName, line));
            }
        }
    }

    private static readonly string[] EventSuffixes =
    [
        "Click","Changed","Selected","Entered","Left","Down","Up","Moved",
        "Activated","Deactivated","Loaded","Unloaded","Closed","Closing",
        "Opened","Opening","Completed","Started","Stopped","Executed",
        "CanExecute","DragEnter","DragLeave","Drop","KeyDown","KeyUp",
        "MouseDown","MouseUp","MouseMove","GotFocus","LostFocus",
        "Checked","Unchecked","Expanded","Collapsed","Scroll","ValueChanged",
        "TextChanged","SelectionChanged","SizeChanged","LayoutUpdated",
    ];

    private static bool LooksLikeEventAttribute(string attrName)
    {
        foreach (var suffix in EventSuffixes)
            if (attrName.EndsWith(suffix, StringComparison.Ordinal))
                return true;

        // Also accept Preview* variants.
        if (attrName.StartsWith("Preview", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static string LocalName(XElement e)
    {
        string local = e.Name.LocalName;
        // Strip property-element suffix if present (should not reach here normally).
        int dot = local.IndexOf('.');
        return dot > 0 ? local[..dot] : local;
    }

    private static string SanitizeIdentifier(string name)
    {
        // Replace characters that are not valid in C# identifiers.
        var sanitized = Regex.Replace(name, @"[^A-Za-z0-9_]", "_");

        // Ensure it doesn't start with a digit.
        if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        // Escape C# reserved keywords.
        if (ReservedKeywords.Contains(sanitized))
            sanitized = "@" + sanitized;

        return sanitized.Length > 0 ? sanitized : "_element";
    }
}
