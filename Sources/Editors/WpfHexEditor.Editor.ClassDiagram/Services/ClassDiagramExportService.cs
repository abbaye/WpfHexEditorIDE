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

        // Provide a large measure pass to discover natural size
        canvas.Measure(new Size(8000, 6000));
        canvas.Arrange(new Rect(canvas.DesiredSize));

        double width  = Math.Max(canvas.ActualWidth,  100);
        double height = Math.Max(canvas.ActualHeight, 100);

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
