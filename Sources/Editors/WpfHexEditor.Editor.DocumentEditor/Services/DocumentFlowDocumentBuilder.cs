// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Services/DocumentFlowDocumentBuilder.cs
// Description:
//     Converts a DocumentBlock tree to a WPF FlowDocument so the
//     standard PrintDialog can render to any installed printer
//     including "Microsoft Print to PDF". Used by T1.3 Export PDF.
// Architecture notes:
//     Mirrors DocumentClipboardService's HTML mapping but emits
//     WPF Block/Inline objects rather than markup strings. Style
//     attributes (font/size/bold/italic/underline/color) honored
//     via cascade resolution already baked into the model.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Services;

/// <summary>
/// Builds a printable <see cref="FlowDocument"/> from a <see cref="DocumentModel"/>.
/// Style attributes are honored block-by-block; structural blocks (image, table,
/// list-item) get appropriate WPF wrappers.
/// </summary>
public static class DocumentFlowDocumentBuilder
{
    /// <summary>
    /// Builds a FlowDocument with sensible page metrics (US Letter at 96 dpi)
    /// suitable for both screen preview and PDF/print output.
    /// </summary>
    public static FlowDocument Build(DocumentModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        var doc = new FlowDocument
        {
            PagePadding         = new Thickness(72),  // 1 inch margins
            ColumnGap           = 0,
            ColumnWidth         = double.PositiveInfinity,
            FontFamily          = new FontFamily("Calibri"),
            FontSize            = 11,
            TextAlignment       = TextAlignment.Left,
        };

        if (model.PageSettings is { } ps && ps.EffectivePageWidth > 0 && ps.EffectivePageHeight > 0)
        {
            doc.PageWidth  = ps.EffectivePageWidth;
            doc.PageHeight = ps.EffectivePageHeight;
        }

        foreach (var block in model.Blocks)
        {
            var wpfBlock = MapBlock(block);
            if (wpfBlock is not null) doc.Blocks.Add(wpfBlock);
        }
        return doc;
    }

    private static System.Windows.Documents.Block? MapBlock(DocumentBlock b)
    {
        return b.Kind switch
        {
            DocumentBlockKinds.Heading    => BuildHeading(b),
            DocumentBlockKinds.ListItem   => BuildListItemParagraph(b),
            DocumentBlockKinds.Table      => BuildTable(b),
            DocumentBlockKinds.Image      => BuildImagePlaceholder(b),
            _                             => BuildParagraph(b)
        };
    }

    private static System.Windows.Documents.Block BuildHeading(DocumentBlock b)
    {
        int level = b.Attributes.TryGetValue(DocumentBlockAttributes.Level, out var l) && l is int li
            ? Math.Clamp(li, 1, 6) : 1;
        var p = BuildParagraph(b);
        p.FontWeight = FontWeights.SemiBold;
        p.FontSize   = level switch { 1 => 22, 2 => 18, 3 => 16, 4 => 14, 5 => 12, _ => 11 };
        p.Margin     = new Thickness(0, 12, 0, 6);
        return p;
    }

    private static Paragraph BuildParagraph(DocumentBlock b)
    {
        var p = new Paragraph();
        ApplyStyle(p, b);
        if (b.Children.Count == 0)
            p.Inlines.Add(new Run(b.Text ?? string.Empty));
        else
            foreach (var c in b.Children)
                p.Inlines.Add(MapInline(c));
        return p;
    }

    private static Inline MapInline(DocumentBlock b)
    {
        if (b.Kind == DocumentBlockKinds.Run)
        {
            var run = new Run(b.Text ?? string.Empty);
            ApplyStyle(run, b);
            return run;
        }
        if (b.Kind == DocumentBlockKinds.Hyperlink)
        {
            var link = new Hyperlink(new Run(b.Text ?? string.Empty));
            ApplyStyle(link, b);
            return link;
        }
        if (b.Kind == DocumentBlockKinds.Image)
            return new Run("[image]");
        // Fallback: treat unknown inline kinds as plain runs.
        return new Run(b.Text ?? string.Empty);
    }

    private static Paragraph BuildListItemParagraph(DocumentBlock b)
    {
        var p = BuildParagraph(b);
        p.Margin = new Thickness(24, 0, 0, 4);
        p.TextIndent = -12;
        // Always prepend the bullet as a separate Run so the original inlines
        // keep their styling intact (mutating firstRun.Text would mangle them).
        var bullet = new Run("• ");
        if (p.Inlines.FirstInline is null) p.Inlines.Add(bullet);
        else                                p.Inlines.InsertBefore(p.Inlines.FirstInline, bullet);
        return p;
    }

    private static System.Windows.Documents.Block BuildTable(DocumentBlock b)
    {
        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) };
        var rg = new TableRowGroup();
        table.RowGroups.Add(rg);
        int maxCols = 1;
        foreach (var rowBlock in b.Children)
        {
            if (rowBlock.Kind != DocumentBlockKinds.TableRow) continue;
            var row = new TableRow();
            int colCount = 0;
            foreach (var cellBlock in rowBlock.Children)
            {
                if (cellBlock.Kind != DocumentBlockKinds.TableCell) continue;
                var cell = new TableCell
                {
                    BorderBrush     = Brushes.Gray,
                    BorderThickness = new Thickness(0.5),
                    Padding         = new Thickness(4)
                };
                foreach (var child in cellBlock.Children)
                {
                    var wb = MapBlock(child);
                    if (wb is not null) cell.Blocks.Add(wb);
                }
                if (cell.Blocks.Count == 0 && !string.IsNullOrEmpty(cellBlock.Text))
                    cell.Blocks.Add(new Paragraph(new Run(cellBlock.Text)));
                row.Cells.Add(cell);
                colCount++;
            }
            if (colCount > maxCols) maxCols = colCount;
            rg.Rows.Add(row);
        }
        for (int i = 0; i < maxCols; i++)
            table.Columns.Add(new TableColumn());
        return table;
    }

    private static System.Windows.Documents.Block BuildImagePlaceholder(DocumentBlock b)
    {
        // Without resolving binary bytes to a BitmapSource we'd block the printer
        // pipeline; emit a placeholder paragraph. Future revision can hydrate
        // BitmapImage from entry.InlineData when available.
        var p = new Paragraph(new Run("[image]"))
        {
            FontStyle = FontStyles.Italic,
            Foreground = Brushes.Gray,
            Margin    = new Thickness(0, 4, 0, 4)
        };
        return p;
    }

    private static void ApplyStyle(TextElement target, DocumentBlock b)
    {
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.FontFamily, out var ff) && ff is string s && !string.IsNullOrEmpty(s))
            target.FontFamily = new FontFamily(s);
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.FontSize, out var fs))
        {
            double pt = fs switch { double d => d, int i => i, _ => 0 };
            if (pt > 0) target.FontSize = pt * 96.0 / 72.0; // pt → device-independent px
        }
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.Bold,   out var bo) && bo is true) target.FontWeight = FontWeights.Bold;
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.Italic, out var it) && it is true) target.FontStyle  = FontStyles.Italic;
        if (b.Attributes.TryGetValue(DocumentBlockAttributes.Color, out var c) && c is string cs && TryParseHexColor(cs, out var col))
            target.Foreground = new SolidColorBrush(col);

        if (target is Inline inline &&
            b.Attributes.TryGetValue(DocumentBlockAttributes.Underline, out var un) && un is true)
            inline.TextDecorations = TextDecorations.Underline;
    }

    private static bool TryParseHexColor(string s, out Color color)
    {
        color = default;
        int start = s.Length > 0 && s[0] == '#' ? 1 : 0;
        int digits = s.Length - start;
        if (digits is not (6 or 8)) return false;
        try
        {
            byte a = 0xFF, r, g, bl;
            if (digits == 8)
            {
                a  = byte.Parse(s.AsSpan(start,     2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                r  = byte.Parse(s.AsSpan(start + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                g  = byte.Parse(s.AsSpan(start + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                bl = byte.Parse(s.AsSpan(start + 6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else
            {
                r  = byte.Parse(s.AsSpan(start,     2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                g  = byte.Parse(s.AsSpan(start + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                bl = byte.Parse(s.AsSpan(start + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            color = Color.FromArgb(a, r, g, bl);
            return true;
        }
        catch { return false; }
    }
}
