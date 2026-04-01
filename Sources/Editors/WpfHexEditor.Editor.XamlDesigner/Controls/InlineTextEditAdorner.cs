// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: InlineTextEditAdorner.cs
// Description:
//     Adorner that overlays a transparent TextBox on the adorned element
//     for in-place text editing. Activated by double-clicking a TextBlock,
//     Label, Button, or any FrameworkElement that exposes a text content
//     property (Text, Content, Header).
//     Enter or losing focus commits the edit; Escape discards.
//
// Architecture Notes:
//     Single-use adorner — removed from AdornerLayer after commit/discard.
//     VisualCollection holds one TextBox child.
//     PropertyName is resolved at construction; write-back is delegated via
//     the TextCommitted event so DesignCanvas can push the change through
//     DesignToXamlSyncService without introducing a circular dependency.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Transparent TextBox overlay for in-place text editing of design elements.
/// </summary>
public sealed class InlineTextEditAdorner : Adorner
{
    private readonly VisualCollection _visuals;
    private readonly TextBox          _textBox;
    private readonly string           _originalText;
    private          bool             _committed;

    /// <summary>
    /// Raised when the user commits an edit (Enter or focus loss).
    /// Value is the new text.
    /// </summary>
    public event EventHandler<string>? TextCommitted;

    public InlineTextEditAdorner(FrameworkElement adornedElement, string currentText)
        : base(adornedElement)
    {
        _originalText = currentText;
        _visuals      = new VisualCollection(this);

        var editBg = Application.Current?.TryFindResource("XD_InlineEditBackground") as Brush
                     ?? new SolidColorBrush(Color.FromArgb(220, 30, 40, 60));

        _textBox = new TextBox
        {
            Text            = currentText,
            Background      = editBg,
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(1.5),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(0x4F, 0xC1, 0xFF)),
            FontFamily      = System.Windows.Documents.TextElement.GetFontFamily(adornedElement),
            FontSize        = System.Windows.Documents.TextElement.GetFontSize(adornedElement) is var fs && fs > 0 ? fs : 12,
            Padding         = new Thickness(2),
            AcceptsReturn   = false,
            TextWrapping    = TextWrapping.NoWrap,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        _textBox.LostFocus   += (_, _) => Commit();
        _textBox.KeyDown     += OnKeyDown;

        _visuals.Add(_textBox);
    }

    // ── Adorner overrides ─────────────────────────────────────────────────────

    protected override int    VisualChildrenCount       => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override Size MeasureOverride(Size constraint)
    {
        _textBox.Measure(AdornedElement.RenderSize);
        return AdornedElement.RenderSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _textBox.Arrange(new Rect(AdornedElement.RenderSize));
        return finalSize;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Focuses the TextBox and selects all text.</summary>
    public void Activate()
    {
        _textBox.Focus();
        _textBox.SelectAll();
    }

    // ── Keyboard handling ─────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            Commit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Discard();
            e.Handled = true;
        }
    }

    private void Commit()
    {
        if (_committed) return;
        _committed = true;
        TextCommitted?.Invoke(this, _textBox.Text);
        RemoveSelf();
    }

    private void Discard()
    {
        if (_committed) return;
        _committed = true;
        RemoveSelf();
    }

    private void RemoveSelf()
    {
        if (AdornedElement is UIElement el)
        {
            var layer = AdornerLayer.GetAdornerLayer(el);
            layer?.Remove(this);
        }
    }
}
