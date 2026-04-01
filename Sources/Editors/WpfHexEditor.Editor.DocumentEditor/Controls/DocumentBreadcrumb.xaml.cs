// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentBreadcrumb.xaml.cs
// Description:
//     Breadcrumb navigation bar showing the hierarchical path
//     (Format > Section > Paragraph > Run) of the currently
//     selected DocumentBlock. Clicking a segment navigates to it.
// Architecture: Pure UI — driven by Segments DP from DocumentEditorHost.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Breadcrumb bar that displays the path from the document root
/// to the currently selected <see cref="DocumentBlock"/>.
/// </summary>
public partial class DocumentBreadcrumb : UserControl
{
    // ── Dependency Properties ────────────────────────────────────────────────

    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.Register(
            nameof(Segments),
            typeof(ObservableCollection<BreadcrumbSegment>),
            typeof(DocumentBreadcrumb),
            new PropertyMetadata(null, OnSegmentsChanged));

    public static readonly DependencyProperty HasPathProperty =
        DependencyProperty.Register(
            nameof(HasPath), typeof(bool), typeof(DocumentBreadcrumb),
            new PropertyMetadata(false));

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Raised when the user clicks a breadcrumb segment.</summary>
    public event EventHandler<DocumentBlock>? BlockSelected;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DocumentBreadcrumb()
    {
        Segments = [];
        InitializeComponent();
    }

    // ── Properties ──────────────────────────────────────────────────────────

    public ObservableCollection<BreadcrumbSegment> Segments
    {
        get => (ObservableCollection<BreadcrumbSegment>)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public bool HasPath
    {
        get => (bool)GetValue(HasPathProperty);
        private set => SetValue(HasPathProperty, value);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the breadcrumb path for the given block by walking
    /// its ancestry chain within the document model.
    /// </summary>
    public void SetPath(DocumentBlock? block, DocumentModel? model)
    {
        Segments.Clear();
        if (block is null || model is null)
        {
            HasPath = false;
            return;
        }

        var path = BuildAncestorPath(block, model);
        for (int i = 0; i < path.Count; i++)
            Segments.Add(new BreadcrumbSegment(path[i], i == path.Count - 1));

        HasPath = Segments.Count > 0;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<DocumentBlock> BuildAncestorPath(DocumentBlock target, DocumentModel model)
    {
        var path = new List<DocumentBlock>();

        bool Walk(IEnumerable<DocumentBlock> blocks)
        {
            foreach (var b in blocks)
            {
                path.Add(b);
                if (b == target) return true;
                if (b.Children.Count > 0 && Walk(b.Children)) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        Walk(model.Blocks);
        return path;
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DocumentBreadcrumb bc)
            bc.HasPath = bc.Segments?.Count > 0;
    }

    private void OnSegmentClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is BreadcrumbSegment seg)
            BlockSelected?.Invoke(this, seg.Block);
    }
}

// ── BreadcrumbSegment ─────────────────────────────────────────────────────────

/// <summary>A single node in the breadcrumb path.</summary>
public sealed class BreadcrumbSegment
{
    public BreadcrumbSegment(DocumentBlock block, bool isLast)
    {
        Block    = block;
        IsLast   = isLast;
        KindLabel = block.Kind.ToUpperInvariant()[..Math.Min(3, block.Kind.Length)];
        Label     = block.Text.Length > 24 ? block.Text[..24] + "…" : block.Text;
        if (string.IsNullOrWhiteSpace(Label)) Label = block.Kind;
    }

    public DocumentBlock Block    { get; }
    public bool          IsLast   { get; }
    public string        KindLabel { get; }
    public string        Label    { get; }
}
