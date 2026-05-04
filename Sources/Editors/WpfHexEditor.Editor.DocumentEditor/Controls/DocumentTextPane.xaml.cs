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
    }

    // ── Public Properties ────────────────────────────────────────────────────

    public DocumentBlock?          SelectedBlock    => PART_Renderer.SelectedBlock;
    public int                     BlockCount       => PART_Renderer.BlockCount;
    public int                     CaretBlockIndex  => PART_Renderer.CaretBlockIndex;

    /// <summary>Direct access to the renderer for PageSettings wiring.</summary>
    public DocumentCanvasRenderer  Renderer         => PART_Renderer;

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
    public void IncreaseIndent()                     => PART_Renderer.IncreaseIndent();
    public void DecreaseIndent()                     => PART_Renderer.DecreaseIndent();
    public void NavigateToBlockIndex(int blockIndex) => PART_Renderer.NavigateToBlockIndex(blockIndex);

    public void SetForensicMode(bool enabled)
    {
        PART_GutterCol.Width           = enabled ? new GridLength(16) : new GridLength(0);
        PART_ForensicGutter.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        PART_Renderer.SetForensicMode(enabled);
        if (enabled && _model is not null) RenderForensicGutter();
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
        PART_Renderer.ApplyFormatToSelection(format, true);
    }

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

    // ── Forensic gutter (legacy badge rendering) ─────────────────────────────

    private void RenderForensicGutter()
    {
        if (_model is null) return;
        PART_ForensicGutter.Children.Clear();

        int row = 0;
        foreach (var alert in _model.ForensicAlerts.Take(30))
        {
            var brush = alert.Severity switch
            {
                Core.Forensic.ForensicSeverity.Error   => TryFindResource("DE_ForensicErrorBrush") as Brush ?? Brushes.Red,
                Core.Forensic.ForensicSeverity.Warning => TryFindResource("DE_ForensicWarnBrush") as Brush  ?? Brushes.Yellow,
                _ => TryFindResource("DE_ForensicOkBrush") as Brush ?? Brushes.Green
            };

            var badge = new System.Windows.Shapes.Rectangle
            {
                Width   = 10,
                Height  = 10,
                Fill    = brush,
                ToolTip = alert.Description
            };
            System.Windows.Controls.Canvas.SetLeft(badge, 3);
            System.Windows.Controls.Canvas.SetTop(badge,  row * 16 + 3);
            PART_ForensicGutter.Children.Add(badge);
            row++;
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
