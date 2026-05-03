// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/ClassDiagramExportService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Facade over the Core export engines. Provides async export
//     methods for C# skeleton, Mermaid, SVG, and PNG formats.
//
// Architecture Notes:
//     Pattern: Facade.
//     PNG export renders a temporary DiagramCanvas to a
//     RenderTargetBitmap, then encodes as PNG via BitmapEncoder.
//     All file I/O is async via File.WriteAllTextAsync / File.WriteAllBytesAsync.
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfHexEditor.Editor.ClassDiagram.Controls;
using WpfHexEditor.Editor.ClassDiagram.Core.Export;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>
/// Provides export operations for a <see cref="DiagramDocument"/> to C#, Mermaid, SVG, and PNG.
/// </summary>
public sealed class ClassDiagramExportService
{
    // ---------------------------------------------------------------------------
    // C# skeleton
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generates C# skeleton source for <paramref name="doc"/> and optionally writes it to disk.
    /// </summary>
    /// <param name="doc">Source document.</param>
    /// <param name="filePath">Optional output file path. When null, no file is written.</param>
    /// <returns>Generated C# source text.</returns>
    public async Task<string> ExportCSharpAsync(DiagramDocument doc, string? filePath = null)
    {
        string source = CSharpSkeletonGenerator.Generate(doc);
        if (!string.IsNullOrEmpty(filePath))
            await File.WriteAllTextAsync(filePath, source).ConfigureAwait(false);
        return source;
    }

    // ---------------------------------------------------------------------------
    // Mermaid
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generates Mermaid classDiagram syntax for <paramref name="doc"/> and optionally writes it to disk.
    /// </summary>
    public async Task<string> ExportMermaidAsync(DiagramDocument doc, string? filePath = null)
    {
        string mermaid = MermaidExporter.Export(doc);
        if (!string.IsNullOrEmpty(filePath))
            await File.WriteAllTextAsync(filePath, mermaid).ConfigureAwait(false);
        return mermaid;
    }

    // ---------------------------------------------------------------------------
    // SVG
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generates SVG markup for <paramref name="doc"/> and optionally writes it to disk.
    /// </summary>
    /// <param name="isDark">When true, uses dark-theme color palette in the SVG.</param>
    public async Task<string> ExportSvgAsync(DiagramDocument doc, string? filePath = null, bool isDark = false)
    {
        string svg = ClassDiagramSvgExporter.Export(doc, isDark);
        if (!string.IsNullOrEmpty(filePath))
            await File.WriteAllTextAsync(filePath, svg).ConfigureAwait(false);
        return svg;
    }

    // ---------------------------------------------------------------------------
    // PNG
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Renders the diagram to a PNG file by constructing a temporary
    /// <see cref="DiagramCanvas"/>, measuring/arranging it, and using
    /// <see cref="RenderTargetBitmap"/> to capture the visual.
    /// </summary>
    /// <param name="doc">Source document.</param>
    /// <param name="filePath">Output PNG file path.</param>
    /// <param name="isDark">When true, a dark background is applied.</param>
    public async Task ExportPngAsync(DiagramDocument doc, string filePath, bool isDark = false)
    {
        byte[] pngBytes = await Task.Run(() => RenderToPng(doc, isDark)).ConfigureAwait(false);
        await File.WriteAllBytesAsync(filePath, pngBytes).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------------------
    // PlantUML
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generates PlantUML @startuml class diagram syntax for <paramref name="doc"/>.
    /// </summary>
    public async Task<string> ExportPlantUmlAsync(DiagramDocument doc, string? filePath = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("@startuml");
        sb.AppendLine();

        foreach (var node in doc.Classes)
        {
            string keyword = node.Kind switch
            {
                ClassKind.Interface => "interface",
                ClassKind.Enum      => "enum",
                ClassKind.Struct    => "class",
                ClassKind.Abstract  => "abstract class",
                _                   => "class"
            };

            sb.Append(keyword).Append(' ').AppendLine(node.Name).AppendLine("{");

            foreach (var m in node.Members)
            {
                string vis = m.Visibility switch
                {
                    MemberVisibility.Public    => "+",
                    MemberVisibility.Protected => "#",
                    MemberVisibility.Private   => "-",
                    _                          => "~"
                };
                string stat = m.IsStatic ? "{static} " : "";
                string abst = m.IsAbstract ? "{abstract} " : "";
                sb.Append("  ").Append(vis).Append(stat).Append(abst)
                  .AppendLine(m.DisplayLabel);
            }
            sb.AppendLine("}").AppendLine();
        }

        foreach (var rel in doc.Relationships)
        {
            string arrow = rel.Kind switch
            {
                RelationshipKind.Inheritance => "<|--",
                RelationshipKind.Realization => "<|..",
                RelationshipKind.Dependency  => "<..",
                RelationshipKind.Aggregation => "o--",
                RelationshipKind.Composition => "*--",
                _                            => "-->"
            };

            var src = doc.FindById(rel.SourceId);
            var tgt = doc.FindById(rel.TargetId);
            if (src is null || tgt is null) continue;

            string label = string.IsNullOrWhiteSpace(rel.Label) ? "" : $" : {rel.Label}";
            sb.Append(tgt.Name).Append(' ').Append(arrow).Append(' ').Append(src.Name)
              .AppendLine(label);
        }

        sb.AppendLine("@enduml");
        string result = sb.ToString();

        if (!string.IsNullOrEmpty(filePath))
            await File.WriteAllTextAsync(filePath, result).ConfigureAwait(false);
        return result;
    }

    // ---------------------------------------------------------------------------
    // Structurizr DSL
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generates a Structurizr C4 DSL component model for <paramref name="doc"/>.
    /// </summary>
    public async Task<string> ExportStructurizrAsync(DiagramDocument doc, string? filePath = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("workspace {");
        sb.AppendLine("  model {");

        // Group by namespace as container
        var groups = doc.Classes
            .GroupBy(n => string.IsNullOrEmpty(n.Namespace) ? "Default" : n.Namespace)
            .OrderBy(g => g.Key);

        foreach (var grp in groups)
        {
            sb.Append("    ").Append(SafeId(grp.Key)).Append(" = container \"")
              .Append(grp.Key).AppendLine("\" {");

            foreach (var node in grp)
            {
                string desc = string.IsNullOrEmpty(node.XmlDocSummary)
                    ? $"{node.Kind} with {node.Members.Count} members"
                    : node.XmlDocSummary;

                sb.Append("      ").Append(SafeId(node.Name)).Append(" = component \"")
                  .Append(node.Name).Append("\" \"").Append(desc).AppendLine("\"");
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine("  }");
        sb.AppendLine("  views {");
        sb.AppendLine("    systemContext DefaultSystem {");
        sb.AppendLine("      include *");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        string result = sb.ToString();
        if (!string.IsNullOrEmpty(filePath))
            await File.WriteAllTextAsync(filePath, result).ConfigureAwait(false);
        return result;
    }

    private static string SafeId(string name) =>
        new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    // ---------------------------------------------------------------------------
    // Private rendering
    // ---------------------------------------------------------------------------

    private static byte[] RenderToPng(DiagramDocument doc, bool isDark)
    {
        // Must be called on a thread that can create WPF elements.
        // When called via Task.Run, we need STA. Wrap in Dispatcher if needed.
        byte[]? result = null;
        Exception? renderException = null;

        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                result = RenderToPngCore(doc, isDark);
            }
            catch (Exception ex)
            {
                renderException = ex;
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (renderException is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(renderException).Throw();

        return result!;
    }

    private static byte[] RenderToPngCore(DiagramDocument doc, bool isDark)
    {
        // Build a temporary canvas and populate it with the document
        var canvas = new DiagramCanvas();
        canvas.ApplyDocument(doc);

        // Use actual diagram content bounds instead of layout size —
        // Canvas.ActualWidth/Height reflects arranged size, not content extent.
        var bounds = canvas.GetDiagramBounds();
        double width  = Math.Max(bounds.Width,  100);
        double height = Math.Max(bounds.Height, 100);

        // Arrange the canvas at content size so all nodes are within bounds
        canvas.Measure(new Size(width, height));
        canvas.Arrange(new Rect(0, 0, width, height));

        var renderBitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height),
            96, 96,
            PixelFormats.Pbgra32);

        if (isDark)
        {
            // Fill background with a dark color before rendering
            var background = new System.Windows.Shapes.Rectangle
            {
                Width = width,
                Height = height,
                Fill = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };
            background.Measure(new Size(width, height));
            background.Arrange(new Rect(0, 0, width, height));
            renderBitmap.Render(background);
        }

        renderBitmap.Render(canvas);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
