// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ToolboxDropService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Handles drag-and-drop from the Toolbox panel onto the DesignCanvas.
//     Receives the XAML snippet from the dragged ToolboxItem, injects
//     it into the document at the drop position, and notifies the host
//     so the code editor and canvas are updated in sync.
//
// Architecture Notes:
//     Service pattern — stateless drop handling.
//     Drop position is converted from canvas coordinates into XAML
//     attribute values (Canvas.Left/Top for Canvas parents, Margin otherwise).
// ==========================================================

using System.Xml.Linq;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Handles inserting a <see cref="ToolboxItem"/> into a XAML document at a given position.
/// </summary>
public sealed class ToolboxDropService
{
    // ── Format ────────────────────────────────────────────────────────────────

    /// <summary>
    /// WPF drag-drop format string used to transfer <see cref="ToolboxItem"/> data.
    /// </summary>
    public const string DragDropFormat = "XD_ToolboxItem";

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts <paramref name="item"/>'s default XAML into <paramref name="rawXaml"/>
    /// at the document root or inside the first Panel child of the root.
    /// Optionally injects Canvas.Left/Top or Margin from <paramref name="dropPosition"/>.
    /// Returns the updated XAML string, or <paramref name="rawXaml"/> on error.
    /// </summary>
    public string InsertItem(
        string rawXaml,
        ToolboxItem item,
        System.Windows.Point dropPosition,
        bool canvasParent = false)
    {
        if (string.IsNullOrWhiteSpace(rawXaml)) return rawXaml;

        try
        {
            // Parse the item XAML fragment with default WPF namespaces.
            var ns   = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var xns  = "http://schemas.microsoft.com/winfx/2006/xaml";
            var snippet = XElement.Parse(
                $"<root xmlns=\"{ns}\" xmlns:x=\"{xns}\">{item.DefaultXaml}</root>");
            var newEl = snippet.Elements().FirstOrDefault();
            if (newEl is null) return rawXaml;

            // Detach from the wrapper root.
            newEl.Remove();

            // Inject position.
            if (canvasParent)
            {
                newEl.SetAttributeValue("Canvas.Left", dropPosition.X.ToString("F0", System.Globalization.CultureInfo.InvariantCulture));
                newEl.SetAttributeValue("Canvas.Top",  dropPosition.Y.ToString("F0", System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (dropPosition.X > 4 || dropPosition.Y > 4)
            {
                newEl.SetAttributeValue("Margin",
                    $"{dropPosition.X:F0},{dropPosition.Y:F0},0,0");
            }

            // Insert into the document.
            var doc = XDocument.Parse(rawXaml, LoadOptions.PreserveWhitespace);
            var insertTarget = FindFirstPanelOrRoot(doc.Root);
            insertTarget?.Add(newEl);

            var sb = new System.Text.StringBuilder();
            using var writer = System.Xml.XmlWriter.Create(sb,
                new System.Xml.XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Indent             = true,
                    IndentChars        = "    ",
                    NewLineHandling    = System.Xml.NewLineHandling.Replace
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

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the root element if it is a Panel or other container;
    /// otherwise returns the root directly.
    /// </summary>
    private static XElement? FindFirstPanelOrRoot(XElement? root)
    {
        if (root is null) return null;

        string[] panelNames = { "Grid", "StackPanel", "DockPanel", "WrapPanel",
                                 "Canvas", "UniformGrid", "Border" };

        if (Array.Exists(panelNames, n => n.Equals(root.Name.LocalName, StringComparison.Ordinal)))
            return root;

        // Check direct children.
        foreach (var child in root.Elements())
        {
            if (Array.Exists(panelNames, n => n.Equals(child.Name.LocalName, StringComparison.Ordinal)))
                return child;
        }

        return root;
    }
}
