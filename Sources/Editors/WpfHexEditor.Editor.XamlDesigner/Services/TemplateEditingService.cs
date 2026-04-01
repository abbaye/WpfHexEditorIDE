// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: TemplateEditingService.cs
// Description:
//     Extracts ControlTemplate / DataTemplate XAML fragments from a parent
//     XAML document and patches them back atomically as a undo-capable
//     design operation.
//     Provides the template scope context for TemplateBreadcrumbBar.
//
// Architecture Notes:
//     Pure XLinq service — no WPF rendering dependency.
//     Template scope is modelled as a stack of (elementName, templateType)
//     pairs matching the breadcrumb bar display.
//     Extract: finds the ControlTemplate or DataTemplate child element and
//       returns its inner content as a standalone XAML snippet.
//     Commit: serializes the edited fragment back into the parent document.
// ==========================================================

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>Describes one level of template scope nesting.</summary>
/// <param name="ElementName">The element whose template is being edited (e.g. "Button").</param>
/// <param name="TemplateType">Display string, e.g. "ControlTemplate" or "DataTemplate".</param>
public sealed record TemplateScopeEntry(string ElementName, string TemplateType);

/// <summary>
/// Extracts and commits template XAML fragments for template-edit mode.
/// </summary>
public sealed class TemplateEditingService
{
    private static readonly XNamespace WpfNs  = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    // ── Stack ─────────────────────────────────────────────────────────────────

    private readonly Stack<TemplateScopeEntry> _scopeStack = new();

    /// <summary>Current template-edit scope breadcrumb path (outermost first).</summary>
    public IReadOnlyList<TemplateScopeEntry> ScopeStack
        => _scopeStack.Reverse().ToList();

    /// <summary>Whether template-edit mode is active.</summary>
    public bool IsInTemplateScope => _scopeStack.Count > 0;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the ControlTemplate or DataTemplate XAML from
    /// <paramref name="rawXaml"/> for the element with the given UID.
    /// Returns (templateXaml, updatedRawWithPlaceholder) on success, or null on failure.
    /// </summary>
    public (string TemplateXaml, TemplateScopeEntry Scope)? ExtractTemplate(
        string rawXaml, int elementUid)
    {
        try
        {
            var doc  = XDocument.Parse(rawXaml);
            var el   = FindElementByUid(doc.Root, elementUid);
            if (el is null) return null;

            // Look for a ControlTemplate or DataTemplate child element.
            var templateEl = el.Descendants()
                .FirstOrDefault(d =>
                    d.Name.LocalName is "ControlTemplate" or "DataTemplate" or "ItemContainerStyle");

            if (templateEl is null) return null;

            string elementName = el.Name.LocalName;
            string templateType = templateEl.Name.LocalName;

            _scopeStack.Push(new TemplateScopeEntry(elementName, templateType));

            return (templateEl.ToString(), new TemplateScopeEntry(elementName, templateType));
        }
        catch { return null; }
    }

    /// <summary>
    /// Patches <paramref name="rawXaml"/> by replacing the template fragment
    /// inside the element with the given UID with <paramref name="editedTemplateXaml"/>.
    /// Returns the updated XAML string, or null on failure.
    /// </summary>
    public string? CommitTemplate(string rawXaml, int elementUid, string editedTemplateXaml)
    {
        try
        {
            var doc  = XDocument.Parse(rawXaml);
            var el   = FindElementByUid(doc.Root, elementUid);
            if (el is null) return null;

            var templateEl = el.Descendants()
                .FirstOrDefault(d =>
                    d.Name.LocalName is "ControlTemplate" or "DataTemplate" or "ItemContainerStyle");

            if (templateEl is null) return null;

            var newTemplate = XElement.Parse(editedTemplateXaml);
            templateEl.ReplaceWith(newTemplate);

            return doc.ToString(SaveOptions.DisableFormatting);
        }
        catch { return null; }
    }

    /// <summary>Pops the innermost scope level.</summary>
    public void PopScope()
    {
        if (_scopeStack.Count > 0)
            _scopeStack.Pop();
    }

    /// <summary>Clears the entire scope stack (Exit template editing).</summary>
    public void ClearScope() => _scopeStack.Clear();

    // ── Private helpers ───────────────────────────────────────────────────────

    private static XElement? FindElementByUid(XElement? root, int uid)
    {
        if (root is null) return null;
        string tag = $"xd_{uid}";
        return root.DescendantsAndSelf()
            .FirstOrDefault(e => (string?)e.Attribute("Tag") == tag);
    }
}
