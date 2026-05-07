// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentTextPane.xaml.cs
// Description:
//     Thin host for DocumentCanvasRenderer. Bridges the existing
//     DocumentEditorHost API (BindModel, ScrollToOffset, SetZoom,
//     ApplyFormat, SetForensicMode) to the overkill canvas renderer.
//     The ScaleTransform on the ScrollViewer provides zoom; the renderer
//     handles all drawing and IScrollInfo interaction.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Host for <see cref="DocumentCanvasRenderer"/>. Exposes the same
/// public surface as the previous RichTextBox-based implementation.
/// </summary>
public partial class DocumentTextPane : UserControl
{
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user selects a block.</summary>
    public event EventHandler<DocumentBlock?>? SelectedBlockChanged;

    /// <summary>Raised when a selection occurs — host should show pop-toolbar.</summary>
    public event EventHandler<PopToolbarRequestedArgs>? PopToolbarRequested;

    // ── Fields ────────────────────────────────────────────────────────────────

    private DocumentModel? _model;

    private static readonly Brush FallbackKindBg     = Frozen(Color.FromRgb( 55,  65,  85));
    private static readonly Brush FallbackKindHoverBg = Frozen(Color.FromRgb( 80, 100, 140));
    private static readonly Brush FallbackErrorHover  = Frozen(Color.FromRgb(255, 120, 120));
    private static readonly Brush FallbackWarnHover   = Frozen(Color.FromRgb(255, 210,  80));
    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    // ── Constructor ──────────────────────────────────────────────────────────

    public DocumentTextPane()
    {
        InitializeComponent();

        // Wire ScrollViewer to renderer's IScrollInfo
        PART_ScrollViewer.Loaded += (_, _) =>
        {
            if (PART_Renderer is System.Windows.Controls.Primitives.IScrollInfo si)
                si.ScrollOwner = PART_ScrollViewer;
        };

        // Wire rulers
        Loaded += (_, _) =>
        {
            PART_HRuler.Attach(PART_Renderer, _mutator);
            PART_VRuler.Attach(PART_Renderer);
        };

        PART_ScrollViewer.ScrollChanged += (_, _) => UpdateForensicGutterPositions();
        PART_Renderer.SizeChanged       += (_, _) => UpdateForensicGutterPositions();
    }

    private WpfHexEditor.Editor.DocumentEditor.Core.Editing.DocumentMutator? _mutator;

    /// <summary>Allows the host to inject the mutator that the rulers use to commit indent edits.</summary>
    public void SetMutator(WpfHexEditor.Editor.DocumentEditor.Core.Editing.DocumentMutator mutator)
    {
        _mutator = mutator;
        PART_HRuler.Attach(PART_Renderer, _mutator);
    }

    /// <summary>Hides both rulers (for Draft / Outline modes which have no page concept).</summary>
    public void SetRulersVisible(bool visible)
    {
        PART_RulerRow.Height  = visible ? new System.Windows.GridLength(22) : new System.Windows.GridLength(0);
        PART_VRulerCol.Width  = visible ? new System.Windows.GridLength(18) : new System.Windows.GridLength(0);
    }

    // ── Public Properties ────────────────────────────────────────────────────

    public DocumentBlock?          SelectedBlock    => PART_Renderer.SelectedBlock;
    public int                     BlockCount       => PART_Renderer.BlockCount;
    public int                     CaretBlockIndex  => PART_Renderer.CaretBlockIndex;

    /// <summary>Direct access to the renderer for PageSettings wiring.</summary>
    public DocumentCanvasRenderer  Renderer         => PART_Renderer;

    /// <summary>
    /// Post-zoom scroll values from the ScrollViewer (screen pixels, zoom-adjusted).
    /// Use these for the minimap viewport rect so its size is correct at any zoom level.
    /// </summary>
    public double ScrollViewerVerticalOffset  => PART_ScrollViewer.VerticalOffset;
    public double ScrollViewerExtentHeight    => PART_ScrollViewer.ExtentHeight;
    public double ScrollViewerViewportHeight  => PART_ScrollViewer.ViewportHeight;

    public DocumentPageSettings PageSettings
    {
        get => PART_Renderer.PageSettings;
        set => PART_Renderer.PageSettings = value;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void BindModel(DocumentModel model)
    {
        _model = model;
        PART_Renderer.BindModel(model);
    }

    public void ScrollToOffset(long offset)          => PART_Renderer.ScrollToOffset(offset);
    public void ScrollToBlock(DocumentBlock block)   => PART_Renderer.ScrollToBlock(block);
    public void IncreaseIndent()                                          => PART_Renderer.IncreaseIndent();
    public void DecreaseIndent()                                          => PART_Renderer.DecreaseIndent();
    public void NavigateToBlockIndex(int blockIndex)                      => PART_Renderer.NavigateToBlockIndex(blockIndex);
    public void InsertPageBreak()                                         => PART_Renderer.InsertPageBreak();
    public void InsertHyperlink(string displayText, string url)           => PART_Renderer.InsertHyperlink(displayText, url);
    public void InsertTable(int rows, int columns)                        => PART_Renderer.InsertTable(rows, columns);
    public string GetSelectedText()                                       => PART_Renderer.GetSelectedText();

    public void SetForensicMode(bool enabled)
    {
        PART_GutterCol.Width           = enabled ? new GridLength(52) : new GridLength(0);
        PART_ForensicGutter.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        PART_Renderer.SetForensicMode(enabled);
        if (enabled && _model is not null) RenderForensicGutter();
        else PART_ForensicGutter.Children.Clear();
    }

    public void SetZoom(double level)
    {
        PART_ZoomTransform.ScaleX = level;
        PART_ZoomTransform.ScaleY = level;
        PART_Renderer.SetZoom(level);
    }

    /// <summary>
    /// Applies a toggle formatting attribute to the current selection.
    /// Delegates to <see cref="DocumentCanvasRenderer.ApplyFormatToSelection"/>;
    /// undo is handled by <c>DocumentMutator.ApplyRunAttribute</c> via <c>RunAttributeUndoEntry</c>.
    /// </summary>
    public void ApplyFormat(string format)
    {
        if (_model is null) return;
        switch (format)
        {
            case "bullet-list":   PART_Renderer.ToggleBulletList();   break;
            case "numbered-list": PART_Renderer.ToggleNumberedList(); break;
            default:              PART_Renderer.ApplyFormatToSelection(format, true); break;
        }
    }

    /// <summary>Toggles the caret block as a bullet list item.</summary>
    public void ToggleBulletList()   => PART_Renderer.ToggleBulletList();

    /// <summary>Toggles the caret block as a numbered list item.</summary>
    public void ToggleNumberedList() => PART_Renderer.ToggleNumberedList();

    public void ShowLoading(string message = "Loading…") => PART_Renderer.ShowLoading(message);
    public void ShowError(string message)                 => PART_Renderer.ShowError(message);

    public void ShowQuickSearch(WpfHexEditor.Editor.Core.ISearchTarget target)
    {
        if (PART_QuickSearchBar.Visibility == Visibility.Visible)
        {
            PART_QuickSearchBar.FocusSearchInput();
            return;
        }
        PART_QuickSearchBar.BindToTarget(target);
        PART_QuickSearchBar.OnCloseRequested -= OnSearchBarClose;
        PART_QuickSearchBar.OnCloseRequested += OnSearchBarClose;
        PART_QuickSearchBar.Visibility = Visibility.Visible;
        PART_QuickSearchBar.EnsureDefaultPosition(PART_SearchCanvas);
        PART_QuickSearchBar.FocusSearchInput();
    }

    public void HideQuickSearch()
    {
        PART_QuickSearchBar.Visibility = Visibility.Collapsed;
    }

    private void OnSearchBarClose(object? sender, EventArgs e) => HideQuickSearch();

    // ── Renderer event forwarding ────────────────────────────────────────────

    private void OnRendererSelectedBlockChanged(object? sender, DocumentBlock? block) =>
        SelectedBlockChanged?.Invoke(this, block);

    private void OnRendererPopToolbarRequested(object? sender, PopToolbarRequestedArgs args) =>
        PopToolbarRequested?.Invoke(this, args);

    // ── Forensic gutter — scroll-aware, interactive badges ───────────────────

    // Canvas padding constant that matches DocumentCanvasRenderer.PageCanvasPad
    private const double RendererPagePad = 32.0;

    private void RenderForensicGutter()
    {
        if (_model is null) return;
        PART_ForensicGutter.Children.Clear();

        var blocks = PART_Renderer.LayoutBlocks;
        if (blocks.Count == 0) return;

        double scrollY = PART_Renderer.VerticalOffset;
        var alertByBlock = _model.ForensicAlerts.ToDictionary(a => a.Block);

        for (int i = 0; i < blocks.Count; i++)
        {
            var rb = blocks[i];
            if (rb.Block.Kind is "header" or "footer" or "page-break") continue;

            double screenY = RendererPagePad + rb.Y - scrollY;

            PART_ForensicGutter.Children.Add(MakeKindChip(rb, i, screenY));

            if (rb.ForensicSeverity.HasValue)
            {
                alertByBlock.TryGetValue(rb.Block, out var alert);
                PART_ForensicGutter.Children.Add(MakeForensicDot(rb, i, screenY, alert?.Description ?? string.Empty));
            }
        }
    }

    private UIElement MakeKindChip(RenderBlock rb, int blockIdx, double screenY)
    {
        string label = rb.Block.Kind.ToUpperInvariant() switch
        {
            "PARAGRAPH" => "PAR",
            "LIST-ITEM" => "LIS",
            "HEADING"   => "HEA",
            "TABLE"     => "TBL",
            "CODE"      => "COD",
            "IMAGE"     => "IMG",
            string k    => k[..Math.Min(3, k.Length)]
        };

        var kindKey    = $"Forensic_Kind_{label}";
        var tooltipKey = $"Forensic_Kind_{label}_Tip";

        var normalBg = TryFindResource("DE_ForensicKindBg")     as Brush ?? FallbackKindBg;
        var hoverBg  = TryFindResource("DE_ForensicKindHoverBg") as Brush ?? FallbackKindHoverBg;

        var chip = new Border
        {
            Width           = 36,
            Height          = 16,
            CornerRadius    = new CornerRadius(2),
            Background      = normalBg,
            Cursor          = Cursors.Hand,
            ToolTip         = TryFindResource(tooltipKey) as string
                              ?? BuildKindTooltip(rb.Block.Kind, rb.Block),
            Child           = new TextBlock
            {
                Text              = label,
                FontSize          = 9,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = TryFindResource("DE_ForensicKindFg") as Brush ?? Brushes.LightSteelBlue,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            },
            Tag = blockIdx
        };

        chip.ContextMenu = BuildBlockContextMenu(rb, blockIdx);

        chip.MouseEnter += (_, _) => chip.Background = hoverBg;
        chip.MouseLeave += (_, _) => chip.Background = normalBg;
        chip.MouseLeftButtonUp += (_, _) =>
        {
            PART_Renderer.NavigateToBlockIndex(blockIdx);
        };

        Canvas.SetLeft(chip, 2);
        Canvas.SetTop (chip, screenY + 1);
        return chip;
    }

    private UIElement MakeForensicDot(RenderBlock rb, int blockIdx, double screenY, string alertDesc)
    {
        var isError     = rb.ForensicSeverity == Core.Forensic.ForensicSeverity.Error;
        var normalBrush = isError
            ? (TryFindResource("DE_ForensicErrorBrush") as Brush ?? Brushes.Red)
            : (TryFindResource("DE_ForensicWarnBrush")  as Brush ?? Brushes.Orange);
        var hoverBrush  = isError ? FallbackErrorHover : FallbackWarnHover;

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width   = 10,
            Height  = 10,
            Fill    = normalBrush,
            Cursor  = Cursors.Hand,
            ToolTip = alertDesc,
            Tag     = blockIdx
        };

        dot.ContextMenu = BuildForensicDotContextMenu(rb, blockIdx, alertDesc);

        dot.MouseEnter += (_, _) => dot.Fill = hoverBrush;
        dot.MouseLeave += (_, _) => dot.Fill = normalBrush;
        dot.MouseLeftButtonUp += (_, _) => PART_Renderer.NavigateToBlockIndex(blockIdx);

        Canvas.SetLeft(dot, 40);
        Canvas.SetTop (dot, screenY + 3);
        return dot;
    }

    private static string BuildKindTooltip(string kind, DocumentBlock block)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(kind switch
        {
            "paragraph" => "Paragraph",
            "list-item" => "List item",
            "heading"   => $"Heading level {block.Attributes.GetValueOrDefault("level") ?? "1"}",
            "table"     => "Table",
            "code"      => "Code block",
            "image"     => "Image",
            _           => kind
        });
        string preview = block.Children.Count > 0
            ? string.Concat(block.Children.Select(c => c.Text))
            : block.Text;
        if (!string.IsNullOrEmpty(preview))
            sb.Append('\n').Append(preview.Length > 60 ? preview[..60] + "…" : preview);
        return sb.ToString();
    }

    private ContextMenu BuildBlockContextMenu(RenderBlock rb, int blockIdx)
    {
        var cm = new ContextMenu();

        AddMenuItem(cm, TryFindResource("Forensic_Ctx_NavigateTo") as string ?? "Navigate to block",
            "&#xE8A5;", () => PART_Renderer.NavigateToBlockIndex(blockIdx));

        AddMenuItem(cm, TryFindResource("Forensic_Ctx_CopyText") as string ?? "Copy text",
            "&#xE8C8;", () =>
            {
                string text = rb.Block.Children.Count > 0
                    ? string.Concat(rb.Block.Children.Select(c => c.Text))
                    : rb.Block.Text;
                if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
            });

        cm.Items.Add(new Separator());

        AddMenuItem(cm, TryFindResource("Forensic_Ctx_StyleNormal")   as string ?? "Style: Normal",   null, () => PART_Renderer.SetBlockStyleFromMenu("paragraph", 0));
        AddMenuItem(cm, TryFindResource("Forensic_Ctx_StyleHeading1") as string ?? "Style: Heading 1", null, () => PART_Renderer.SetBlockStyleFromMenu("heading", 1));
        AddMenuItem(cm, TryFindResource("Forensic_Ctx_StyleHeading2") as string ?? "Style: Heading 2", null, () => PART_Renderer.SetBlockStyleFromMenu("heading", 2));
        AddMenuItem(cm, TryFindResource("Forensic_Ctx_StyleHeading3") as string ?? "Style: Heading 3", null, () => PART_Renderer.SetBlockStyleFromMenu("heading", 3));

        cm.Items.Add(new Separator());

        AddMenuItem(cm, TryFindResource("Forensic_Ctx_InspectBinary") as string ?? "Inspect in Hex Editor",
            "&#xE7C4;", () => { /* raise event for host to open hex */ });

        return cm;
    }

    private ContextMenu BuildForensicDotContextMenu(RenderBlock rb, int blockIdx, string alertDesc)
    {
        var cm = new ContextMenu();

        if (!string.IsNullOrEmpty(alertDesc))
        {
            var hdr = new MenuItem { Header = alertDesc, IsEnabled = false };
            cm.Items.Add(hdr);
            cm.Items.Add(new Separator());
        }

        AddMenuItem(cm, TryFindResource("Forensic_Ctx_NavigateTo") as string ?? "Navigate to block",
            "&#xE8A5;", () => PART_Renderer.NavigateToBlockIndex(blockIdx));

        AddMenuItem(cm, TryFindResource("Forensic_Ctx_CopyAlert") as string ?? "Copy alert description",
            "&#xE8C8;", () => { if (!string.IsNullOrEmpty(alertDesc)) Clipboard.SetText(alertDesc); });

        cm.Items.Add(new Separator());

        AddMenuItem(cm, TryFindResource("Forensic_Ctx_MarkFalsePositive") as string ?? "Mark as false positive",
            "&#xE73E;", () => { /* TODO: mark alert suppressed */ });

        AddMenuItem(cm, TryFindResource("Forensic_Ctx_InspectBinary") as string ?? "Inspect in Hex Editor",
            "&#xE7C4;", () => { /* raise event for host */ });

        return cm;
    }

    private static void AddMenuItem(ContextMenu cm, string header, string? iconGlyph, Action action)
    {
        var item = new MenuItem { Header = header };
        if (iconGlyph is not null)
        {
            item.Icon = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Text       = System.Net.WebUtility.HtmlDecode(iconGlyph),
                FontSize   = 12
            };
        }
        item.Click += (_, _) => action();
        cm.Items.Add(item);
    }

    private void UpdateForensicGutterPositions()
    {
        if (PART_ForensicGutter.Visibility != Visibility.Visible) return;
        if (_model is null) return;

        var blocks = PART_Renderer.LayoutBlocks;
        if (blocks.Count == 0) return;

        double scrollY = PART_Renderer.VerticalOffset;

        // Re-position existing children — faster than full rebuild on every scroll tick
        // Each block contributes up to 2 children (chip + optional dot); walk in sync
        int childIdx = 0;
        for (int i = 0; i < blocks.Count && childIdx < PART_ForensicGutter.Children.Count; i++)
        {
            var rb = blocks[i];
            if (rb.Block.Kind is "header" or "footer" or "page-break") continue;

            double screenY = RendererPagePad + rb.Y - scrollY;

            if (childIdx < PART_ForensicGutter.Children.Count)
            {
                Canvas.SetTop(PART_ForensicGutter.Children[childIdx], screenY + 1);
                childIdx++;
            }
            if (rb.ForensicSeverity.HasValue && childIdx < PART_ForensicGutter.Children.Count)
            {
                Canvas.SetTop(PART_ForensicGutter.Children[childIdx], screenY + 3);
                childIdx++;
            }
        }
    }
}

// ── PopToolbarRequestedArgs ───────────────────────────────────────────────────

/// <summary>Arguments for the pop-toolbar positioning event.</summary>
public sealed class PopToolbarRequestedArgs(Rect selectionRect, DocumentBlock? block) : EventArgs
{
    public Rect           SelectionRect { get; } = selectionRect;
    public DocumentBlock? Block         { get; } = block;
}
