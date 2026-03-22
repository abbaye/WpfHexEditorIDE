// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Export/ClassDiagramSvgExporter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Generates a standalone SVG document from a DiagramDocument.
//     Renders class boxes as <rect>+<text> elements and relationships
//     as <path> arrow connectors.  Supports light and dark colour
//     themes via the isDark flag.
//
// Architecture Notes:
//     Pure BCL string generation — no System.Drawing, no WPF, no XML DOM.
//     Coordinates read directly from ClassNode.X/Y/Width/Height so
//     AutoLayoutEngine must be run before calling Export when automatic
//     positioning is desired.
//
//     Arrow routing: straight line from bottom-centre of source to
//     top-centre of target.  Arrowhead rendered as an SVG <marker>.
//     Label centred at midpoint of connector line.
//
//     SVG viewBox is computed from the bounding box of all nodes plus
//     a fixed 40px canvas padding.
// ==========================================================

using System.Globalization;
using System.Text;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Export;

/// <summary>
/// Exports a <see cref="DiagramDocument"/> to a standalone SVG string.
/// </summary>
public static class ClassDiagramSvgExporter
{
    // Formatting helper — always use invariant culture for SVG numbers
    private static string F(double v) => v.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>
    /// Renders <paramref name="doc"/> as a standalone SVG document.
    /// </summary>
    /// <param name="doc">The diagram to render.</param>
    /// <param name="isDark">
    /// When <see langword="true"/> uses a dark background palette;
    /// otherwise uses a light background palette.
    /// </param>
    public static string Export(DiagramDocument doc, bool isDark = false)
    {
        var palette = isDark ? DarkPalette : LightPalette;
        var sb = new StringBuilder();

        // Compute canvas bounding box
        var (viewW, viewH) = ComputeViewBox(doc);

        WriteSvgHeader(sb, viewW, viewH, palette);
        WriteArrowMarkerDefs(sb, palette);

        // Draw relationships first (behind boxes)
        foreach (var rel in doc.Relationships)
            WriteRelationshipPath(sb, doc, rel, palette);

        // Draw class boxes
        foreach (var node in doc.Classes)
            WriteClassBox(sb, node, palette);

        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    // -------------------------------------------------------
    // Rendering helpers
    // -------------------------------------------------------

    private static void WriteSvgHeader(
        StringBuilder sb, double w, double h, SvgPalette p)
    {
        sb.AppendLine(
            $"""
            <svg xmlns="http://www.w3.org/2000/svg"
                 width="{F(w)}" height="{F(h)}"
                 viewBox="0 0 {F(w)} {F(h)}"
                 font-family="Segoe UI, Arial, sans-serif" font-size="12">
              <rect width="100%" height="100%" fill="{p.Background}"/>
            """);
    }

    private static void WriteArrowMarkerDefs(StringBuilder sb, SvgPalette p)
    {
        // Open arrowhead marker (used for most relationship types)
        sb.AppendLine(
            $"""
              <defs>
                <marker id="arrow" markerWidth="10" markerHeight="7"
                        refX="9" refY="3.5" orient="auto">
                  <polygon points="0 0, 10 3.5, 0 7"
                           fill="{p.ArrowFill}" stroke="none"/>
                </marker>
                <marker id="arrow-open" markerWidth="10" markerHeight="7"
                        refX="9" refY="3.5" orient="auto">
                  <polyline points="0 0, 10 3.5, 0 7"
                            fill="none" stroke="{p.ArrowFill}" stroke-width="1.5"/>
                </marker>
                <marker id="diamond-open" markerWidth="12" markerHeight="8"
                        refX="11" refY="4" orient="auto">
                  <polygon points="0 4, 6 0, 12 4, 6 8"
                           fill="none" stroke="{p.ArrowFill}" stroke-width="1.2"/>
                </marker>
              </defs>
            """);
    }

    private static void WriteClassBox(StringBuilder sb, ClassNode node, SvgPalette p)
    {
        var x = node.X;
        var y = node.Y;
        var w = node.Width;
        var headerH = 36.0;
        var memberH = 18.0;
        var totalH = node.Height;

        // Outer box
        sb.AppendLine(
            $"""
              <rect x="{F(x)}" y="{F(y)}" width="{F(w)}" height="{F(totalH)}"
                    rx="4" ry="4"
                    fill="{p.BoxFill}" stroke="{p.BoxStroke}" stroke-width="1.5"/>
            """);

        // Header divider
        sb.AppendLine(
            $"""
              <line x1="{F(x)}" y1="{F(y + headerH)}"
                    x2="{F(x + w)}" y2="{F(y + headerH)}"
                    stroke="{p.BoxStroke}" stroke-width="1"/>
            """);

        // Type stereotype label (small, above name)
        var stereotypeLabel = node.Kind switch
        {
            ClassKind.Interface => "«interface»",
            ClassKind.Enum => "«enumeration»",
            ClassKind.Struct => "«struct»",
            ClassKind.Abstract => "«abstract»",
            _ when node.IsAbstract => "«abstract»",
            _ => null
        };

        var nameY = stereotypeLabel != null ? y + 14 : y + headerH / 2 + 5;

        if (stereotypeLabel != null)
        {
            sb.AppendLine(
                $"""
                  <text x="{F(x + w / 2)}" y="{F(y + 13)}"
                        text-anchor="middle" font-size="9"
                        fill="{p.StereotypeText}" font-style="italic">{EscapeXml(stereotypeLabel)}</text>
                """);
        }

        // Class name
        sb.AppendLine(
            $"""
              <text x="{F(x + w / 2)}" y="{F(nameY)}"
                    text-anchor="middle" font-weight="bold"
                    fill="{p.HeaderText}">{EscapeXml(node.Name)}</text>
            """);

        // Member rows
        var memberStartY = y + headerH + memberH;
        foreach (var member in node.Members)
        {
            var visChar = member.Visibility switch
            {
                MemberVisibility.Private => "-",
                MemberVisibility.Protected => "#",
                MemberVisibility.Internal => "~",
                _ => "+"
            };

            var label = $"{visChar} {member.DisplayLabel}";
            if (member.Kind == MemberKind.Method)
            {
                var paramsText = string.Join(", ", member.Parameters);
                label = $"{visChar} {member.Name}({paramsText})";
                if (!string.IsNullOrEmpty(member.TypeName))
                    label += $" : {member.TypeName}";
            }

            sb.AppendLine(
                $"""
                  <text x="{F(x + 8)}" y="{F(memberStartY)}"
                        fill="{p.MemberText}" font-size="11">{EscapeXml(label)}</text>
                """);

            memberStartY += memberH;
        }
    }

    private static void WriteRelationshipPath(
        StringBuilder sb, DiagramDocument doc,
        ClassRelationship rel, SvgPalette p)
    {
        var src = doc.Classes.FirstOrDefault(
            n => string.Equals(n.Id, rel.SourceId, StringComparison.Ordinal));
        var tgt = doc.Classes.FirstOrDefault(
            n => string.Equals(n.Id, rel.TargetId, StringComparison.Ordinal));

        if (src is null || tgt is null)
            return;

        // Connect bottom-centre of source to top-centre of target
        var x1 = src.X + src.Width / 2;
        var y1 = src.Y + src.Height;
        var x2 = tgt.X + tgt.Width / 2;
        var y2 = tgt.Y;

        var (strokeDash, markerId) = rel.Kind switch
        {
            RelationshipKind.Dependency => ("stroke-dasharray=\"5,3\"", "arrow"),
            RelationshipKind.Inheritance => (string.Empty, "arrow-open"),
            RelationshipKind.Aggregation => (string.Empty, "diamond-open"),
            RelationshipKind.Composition => (string.Empty, "arrow"),
            _ => (string.Empty, "arrow")
        };

        sb.AppendLine(
            $"""
              <path d="M {F(x1)} {F(y1)} C {F(x1)} {F((y1 + y2) / 2)}, {F(x2)} {F((y1 + y2) / 2)}, {F(x2)} {F(y2)}"
                    fill="none" stroke="{p.ArrowFill}" stroke-width="1.5"
                    {strokeDash} marker-end="url(#{markerId})"/>
            """);

        // Optional mid-edge label
        if (!string.IsNullOrEmpty(rel.Label))
        {
            var midX = (x1 + x2) / 2;
            var midY = (y1 + y2) / 2;
            sb.AppendLine(
                $"""
                  <text x="{F(midX)}" y="{F(midY - 4)}"
                        text-anchor="middle" font-size="10"
                        fill="{p.MemberText}">{EscapeXml(rel.Label)}</text>
                """);
        }
    }

    private static (double W, double H) ComputeViewBox(DiagramDocument doc)
    {
        if (doc.Classes.Count == 0)
            return (400, 300);

        var maxX = doc.Classes.Max(n => n.X + n.Width) + 40;
        var maxY = doc.Classes.Max(n => n.Y + n.Height) + 40;

        return (maxX, maxY);
    }

    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");

    // -------------------------------------------------------
    // Palettes
    // -------------------------------------------------------

    private sealed record SvgPalette(
        string Background,
        string BoxFill,
        string BoxStroke,
        string HeaderText,
        string MemberText,
        string StereotypeText,
        string ArrowFill);

    private static readonly SvgPalette LightPalette = new(
        Background: "#FFFFFF",
        BoxFill: "#F0F4FF",
        BoxStroke: "#4A7CC7",
        HeaderText: "#1A2B6B",
        MemberText: "#2D2D2D",
        StereotypeText: "#6A6A9A",
        ArrowFill: "#4A7CC7");

    private static readonly SvgPalette DarkPalette = new(
        Background: "#1E1E2E",
        BoxFill: "#2A2A3E",
        BoxStroke: "#7AA2E5",
        HeaderText: "#CDD6F4",
        MemberText: "#BAC2DE",
        StereotypeText: "#9399B2",
        ArrowFill: "#7AA2E5");
}
