// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/CellEditorAdorner.cs
// Description:
//     TextBox-based adorner for inline table cell editing (Phase 16).
//     Double-click on a table cell → CellEditorAdorner overlays a TextBox
//     at the cell's canvas rect. On commit (LostFocus / Enter) calls
//     DocumentMutator.SetText(); on cancel (Escape) discards changes.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.DocumentEditor.Core.Editing;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Adorner that renders an editable <see cref="TextBox"/> over a table cell.
/// </summary>
internal sealed class CellEditorAdorner : Adorner
{
    private readonly VisualCollection _children;
    private readonly TextBox          _textBox;
    private readonly DocumentBlock    _cellBlock;
    private readonly DocumentMutator  _mutator;
    private readonly Rect             _cellRect;
    private          bool             _committed;

    public event EventHandler? EditCommitted;
    public event EventHandler? EditCancelled;

    public CellEditorAdorner(
        UIElement         adornedElement,
        Rect              cellRect,
        DocumentBlock     cellBlock,
        DocumentMutator   mutator)
        : base(adornedElement)
    {
        _cellRect  = cellRect;
        _cellBlock = cellBlock;
        _mutator   = mutator;
        _children  = new VisualCollection(this);

        _textBox = new TextBox
        {
            Text           = cellBlock.Text,
            AcceptsReturn  = false,
            BorderThickness = new Thickness(1),
            Padding        = new Thickness(2),
            FontSize       = 13,
        };

        _textBox.LostFocus += (_, _) => Commit();
        _textBox.KeyDown   += OnKeyDown;

        _children.Add(_textBox);
    }

    // ── Adorner overrides ─────────────────────────────────────────────────────

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        _textBox.Arrange(_cellRect);
        return finalSize;
    }

    public void Focus() => _textBox.Focus();

    // ── Commit / Cancel ───────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)  { Commit();  e.Handled = true; }
        if (e.Key == Key.Escape)  { Cancel();  e.Handled = true; }
    }

    private void Commit()
    {
        if (_committed) return;
        _committed = true;
        _mutator.SetText(_cellBlock, _textBox.Text);
        EditCommitted?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        if (_committed) return;
        _committed = true;
        EditCancelled?.Invoke(this, EventArgs.Empty);
    }
}
