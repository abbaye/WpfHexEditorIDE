// ==========================================================
// Project: WpfHexEditor.Core.DocumentStructure
// File: Providers/XamlDesignerStructureProvider.cs
// Created: 2026-04-06
// Description:
//     Document structure provider for .xaml files open in the XAML Designer.
//     Builds the node tree from the live rendered element tree exposed by
//     IXamlDesignerService, enabling VS-like Document Outline experience.
//
// Architecture Notes:
//     Priority 800 — above XmlStructureProvider (300) but below LSP (1000).
//     CanProvide returns false when the designer is inactive, allowing the
//     XmlStructureProvider fallback to handle code-only .xaml views.
//     DocumentStructureNode.Tag carries the element UID (int) for selection sync.
// ==========================================================

using System.IO;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;
using WpfHexEditor.SDK.ExtensionPoints.XamlDesigner;

namespace WpfHexEditor.Core.DocumentStructure.Providers;

/// <summary>
/// Document structure provider backed by the live XAML Designer element tree.
/// Priority 800 — takes over from <see cref="XmlStructureProvider"/> when the
/// active document is rendered in the XAML Designer.
/// </summary>
public sealed class XamlDesignerStructureProvider : IDocumentStructureProvider
{
    private readonly IXamlDesignerService _service;

    public string DisplayName => "XAML Designer";
    public int    Priority    => 800;

    public XamlDesignerStructureProvider(IXamlDesignerService service)
        => _service = service;

    public bool CanProvide(string? filePath, string? documentType, string? language)
        => _service.IsDesignerActive
        && string.Equals(
               Path.GetExtension(filePath),
               ".xaml",
               StringComparison.OrdinalIgnoreCase);

    public Task<DocumentStructureResult?> GetStructureAsync(
        string filePath, CancellationToken ct = default)
    {
        var roots = _service.GetElementTree();
        if (roots.Count == 0)
            return Task.FromResult<DocumentStructureResult?>(null);

        var nodes = roots
            .Select(r => ConvertNode(r))
            .ToList();

        return Task.FromResult<DocumentStructureResult?>(new DocumentStructureResult
        {
            FilePath  = filePath,
            Language  = "xaml",
            Nodes     = nodes,
            Timestamp = DateTime.UtcNow,
        });
    }

    // ── Conversion ──────────────────────────────────────────────────────────

    private static DocumentStructureNode ConvertNode(XamlDesignerNode n)
    {
        var children = n.Children
            .Select(c => ConvertNode(c))
            .ToList();

        return new DocumentStructureNode
        {
            Name        = n.TypeName,
            Kind        = "element",
            Detail      = n.Name,           // x:Name shown as detail
            StartLine   = n.StartLine,
            StartColumn = n.StartColumn,
            Tag         = n.Uid,            // carries UID for selection sync
            Children    = children,
        };
    }
}
