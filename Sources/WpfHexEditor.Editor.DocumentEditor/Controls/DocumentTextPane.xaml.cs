// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentTextPane.xaml.cs
// Description:
//     WPF RichTextBox pane that renders DocumentModel.Blocks as a
//     FlowDocument and provides bidirectional text↔block mapping
//     via TextPointerMap for binary-map synchronisation.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Forensic;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Renders a <see cref="DocumentModel"/> as an editable <see cref="FlowDocument"/>
/// and exposes <see cref="SelectedBlock"/> for binary-map synchronisation.
/// </summary>
public partial class DocumentTextPane : UserControl
{
    // ── Fields ──────────────────────────────────────────────────────────────

    private DocumentModel?        _model;
    private TextPointerMap?       _pointerMap;
    private bool                  _isInternalUpdate;
    private bool                  _forensicMode;

    /// <summary>Raised when the user changes text selection.</summary>
    public event EventHandler<DocumentBlock?>? SelectedBlockChanged;

    // ── Constructor ─────────────────────────────────────────────────────────

    public DocumentTextPane()
    {
        InitializeComponent();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Currently selected block (null when nothing selected).</summary>
    public DocumentBlock? SelectedBlock { get; private set; }

    /// <summary>Binds this pane to the given model and rebuilds the FlowDocument.</summary>
    public void BindModel(DocumentModel model)
    {
        if (_model is not null)
            _model.BlocksChanged -= OnModelBlocksChanged;

        _model = model;
        _model.BlocksChanged += OnModelBlocksChanged;
        RebuildFlowDocument();
    }

    /// <summary>Scrolls to and highlights the block at the given offset.</summary>
    public void ScrollToOffset(long offset)
    {
        if (_pointerMap is null || _model is null) return;

        var block = _model.BinaryMap.BlockAt(offset);
        if (block is null) return;

        HighlightBlock(block, fromHex: true);
    }

    /// <summary>Enables or disables forensic gutter rendering.</summary>
    public void SetForensicMode(bool enabled)
    {
        _forensicMode = enabled;
        PART_GutterCol.Width  = enabled ? new GridLength(16) : new GridLength(0);
        PART_ForensicGutter.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (enabled) RenderForensicGutter();
    }

    // ── FlowDocument building ────────────────────────────────────────────────

    private void RebuildFlowDocument()
    {
        if (_model is null) return;

        _isInternalUpdate = true;
        try
        {
            var doc       = new FlowDocument();
            var ptrMap    = new TextPointerMap();
            var docBlocks = _model.Blocks.ToList();

            foreach (var block in docBlocks)
                AppendBlock(doc, block, ptrMap);

            PART_RichTextBox.Document = doc;
            _pointerMap = ptrMap;
        }
        finally
        {
            _isInternalUpdate = false;
        }

        if (_forensicMode) RenderForensicGutter();
    }

    private static void AppendBlock(
        FlowDocument  doc,
        DocumentBlock block,
        TextPointerMap ptrMap)
    {
        switch (block.Kind)
        {
            case "paragraph":
            case "heading":
            {
                var para = new Paragraph();
                if (block.Kind == "heading")
                {
                    int level = int.TryParse(
                        block.Attributes.GetValueOrDefault("level") as string, out int l) ? l : 1;
                    para.FontSize = level == 1 ? 22 : level == 2 ? 18 : 15;
                    para.FontWeight = FontWeights.Bold;
                }

                if (block.Children.Count > 0)
                {
                    foreach (var runBlock in block.Children.Where(c => c.Kind == "run"))
                    {
                        var run = BuildRun(runBlock);
                        para.Inlines.Add(run);
                        // Store start pointer (cannot get exact pointer until document rendered)
                    }
                }
                else
                {
                    para.Inlines.Add(new Run(block.Text));
                }

                doc.Blocks.Add(para);
                ptrMap.RegisterParagraph(block, para);
                break;
            }

            case "table":
            {
                var table = new Table();
                var rowGroup = new TableRowGroup();
                table.RowGroups.Add(rowGroup);

                foreach (var rowBlock in block.Children.Where(c => c.Kind == "table-row"))
                {
                    var row = new TableRow();
                    foreach (var cellPara in rowBlock.Children)
                    {
                        var cell = new TableCell(new Paragraph(new Run(cellPara.Text)));
                        row.Cells.Add(cell);
                    }
                    rowGroup.Rows.Add(row);
                }

                doc.Blocks.Add(table);
                break;
            }

            case "image":
            {
                var para = new Paragraph(new Run($"[image @ 0x{block.RawOffset:X}]"))
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176))
                };
                doc.Blocks.Add(para);
                ptrMap.RegisterParagraph(block, para);
                break;
            }

            default:
            {
                if (!string.IsNullOrEmpty(block.Text))
                {
                    var para = new Paragraph(new Run(block.Text));
                    doc.Blocks.Add(para);
                    ptrMap.RegisterParagraph(block, para);
                }
                break;
            }
        }
    }

    private static Run BuildRun(DocumentBlock runBlock)
    {
        var run = new Run(runBlock.Text);

        if (runBlock.Attributes.TryGetValue("bold",      out var b) && b is true)
            run.FontWeight = FontWeights.Bold;
        if (runBlock.Attributes.TryGetValue("italic",    out var i) && i is true)
            run.FontStyle = FontStyles.Italic;
        if (runBlock.Attributes.TryGetValue("underline", out var u) && u is true)
            run.TextDecorations = TextDecorations.Underline;
        if (runBlock.Attributes.TryGetValue("fontSize",  out var fs) && fs is int size)
            run.FontSize = size;

        return run;
    }

    // ── Selection & highlight ────────────────────────────────────────────────

    private void OnRichTextBoxSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isInternalUpdate || _pointerMap is null) return;

        var sel = PART_RichTextBox.Selection;
        if (sel.IsEmpty) return;

        var block = _pointerMap.BlockAtPointer(sel.Start);
        if (block is null || block == SelectedBlock) return;

        SelectedBlock = block;
        SelectedBlockChanged?.Invoke(this, block);
    }

    private void OnRichTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInternalUpdate || _model is null || SelectedBlock is null) return;
        // TODO: push TextEditUndoEntry to UndoEngine
    }

    private void HighlightBlock(DocumentBlock block, bool fromHex)
    {
        if (_pointerMap is null) return;

        var para = _pointerMap.ParagraphOf(block);
        if (para is null) return;

        _isInternalUpdate = true;
        try
        {
            var start = para.ContentStart;
            var end   = para.ContentEnd;
            PART_RichTextBox.Selection.Select(start, end);
            start.Paragraph?.BringIntoView();
        }
        finally
        {
            _isInternalUpdate = false;
        }

        SelectedBlock = block;
        if (!fromHex) SelectedBlockChanged?.Invoke(this, block);
    }

    // ── Forensic gutter ──────────────────────────────────────────────────────

    private void RenderForensicGutter()
    {
        if (_model is null) return;
        PART_ForensicGutter.Children.Clear();

        var alerts = _model.ForensicAlerts;
        if (alerts.Count == 0) return;

        // Build a quick lookup: offset → highest severity alert
        var alertByOffset = new Dictionary<long, ForensicSeverity>();
        foreach (var alert in alerts)
        {
            if (!alertByOffset.TryGetValue(alert.Offset, out var existing) ||
                alert.Severity > existing)
                alertByOffset[alert.Offset] = alert.Severity;
        }

        // Draw badges in gutter (simplified: one badge per alert, positioned by row index)
        // Full positioning requires AdornerLayer — see ForensicOverlayControl (GO 18)
        int row = 0;
        foreach (var alert in alerts.Take(30))
        {
            var brush = alert.Severity switch
            {
                ForensicSeverity.Error   => TryFindResource("DE_ForensicErrorBrush") as Brush
                                            ?? Brushes.Red,
                ForensicSeverity.Warning => TryFindResource("DE_ForensicWarnBrush") as Brush
                                            ?? Brushes.Yellow,
                _ => TryFindResource("DE_ForensicOkBrush") as Brush ?? Brushes.Green
            };

            var badge = new System.Windows.Shapes.Rectangle
            {
                Width  = 10,
                Height = 10,
                Fill   = brush,
                ToolTip = alert.Message
            };
            Canvas.SetLeft(badge, 3);
            Canvas.SetTop(badge, row * 16 + 3);
            PART_ForensicGutter.Children.Add(badge);
            row++;
        }
    }

    // ── Model events ────────────────────────────────────────────────────────

    private void OnModelBlocksChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(RebuildFlowDocument);
    }
}

// ── TextPointerMap ────────────────────────────────────────────────────────────

/// <summary>
/// Maps <see cref="DocumentBlock"/> ↔ <see cref="Paragraph"/> for selection bridging.
/// </summary>
internal sealed class TextPointerMap
{
    private readonly Dictionary<DocumentBlock, Paragraph> _blockToPara = [];
    private readonly Dictionary<Paragraph, DocumentBlock> _paraToBlock = [];

    public void RegisterParagraph(DocumentBlock block, Paragraph para)
    {
        _blockToPara[block] = para;
        _paraToBlock[para]  = block;
    }

    public Paragraph? ParagraphOf(DocumentBlock block) =>
        _blockToPara.TryGetValue(block, out var p) ? p : null;

    public DocumentBlock? BlockAtPointer(TextPointer ptr)
    {
        // Walk up to find the containing paragraph
        var para = ptr.Paragraph;
        if (para is null) return null;
        return _paraToBlock.TryGetValue(para, out var block) ? block : null;
    }
}
