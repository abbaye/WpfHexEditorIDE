// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: Rendering/LineVisualPool.cs
// Description:
//     Fixed-capacity pool of DrawingVisual objects — one per visible hex line.
//     Enables selective per-line redraws driven by DirtyReason flags, eliminating
//     the need to repaint all lines on every cursor move or selection change.
//
// Architecture Notes:
//     Owner (HexViewport) overrides VisualChildrenCount / GetVisualChild so WPF
//     includes the pool visuals in the visual tree.  Resize() calls Add/RemoveVisualChild
//     on the owner to keep the tree in sync.  OnRender replaces the background rect
//     only; RefreshDirtyLines() drives selective per-line paints.
// ==========================================================

using System;
using System.Windows.Media;

namespace WpfHexEditor.HexEditor.Rendering;

/// <summary>
/// Manages a pool of <see cref="DrawingVisual"/> objects for per-line rendering.
/// </summary>
internal sealed class LineVisualPool
{
    // AddVisualChild/RemoveVisualChild are protected on Visual; delegate to the owner via callbacks.
    private readonly Action<Visual> _addChild;
    private readonly Action<Visual> _removeChild;
    private DrawingVisual[] _pool = Array.Empty<DrawingVisual>();
    private bool[] _dirty = Array.Empty<bool>();

    /// <summary>Number of line visuals currently in the pool.</summary>
    public int Capacity => _pool.Length;

    /// <param name="addChild">Callback that calls <c>AddVisualChild(v)</c> on the owner.</param>
    /// <param name="removeChild">Callback that calls <c>RemoveVisualChild(v)</c> on the owner.</param>
    public LineVisualPool(Action<Visual> addChild, Action<Visual> removeChild)
    {
        _addChild    = addChild    ?? throw new ArgumentNullException(nameof(addChild));
        _removeChild = removeChild ?? throw new ArgumentNullException(nameof(removeChild));
    }

    /// <summary>
    /// Resizes the pool to <paramref name="newCapacity"/>.
    /// Calls add/remove visual child callbacks to keep the WPF visual tree in sync.
    /// </summary>
    public void Resize(int newCapacity)
    {
        if (newCapacity == _pool.Length)
            return;

        if (newCapacity > _pool.Length)
        {
            var newPool  = new DrawingVisual[newCapacity];
            var newDirty = new bool[newCapacity];
            Array.Copy(_pool,  newPool,  _pool.Length);
            Array.Copy(_dirty, newDirty, _dirty.Length);

            for (int i = _pool.Length; i < newCapacity; i++)
            {
                var v = new DrawingVisual();
                newPool[i] = v;
                newDirty[i] = true;
                _addChild(v);
            }

            _pool  = newPool;
            _dirty = newDirty;
        }
        else
        {
            // Shrink: remove excess visuals from the tree
            for (int i = newCapacity; i < _pool.Length; i++)
                _removeChild(_pool[i]);

            var newPool  = new DrawingVisual[newCapacity];
            var newDirty = new bool[newCapacity];
            Array.Copy(_pool,  newPool,  newCapacity);
            Array.Copy(_dirty, newDirty, newCapacity);
            _pool  = newPool;
            _dirty = newDirty;
        }
    }

    /// <summary>Returns the <see cref="DrawingVisual"/> at <paramref name="index"/>.</summary>
    public DrawingVisual Acquire(int index) => _pool[index];

    /// <summary>Marks the line at <paramref name="lineIndex"/> as needing a redraw.</summary>
    public void MarkDirty(int lineIndex)
    {
        if ((uint)lineIndex < (uint)_dirty.Length)
            _dirty[lineIndex] = true;
    }

    /// <summary>Marks all lines dirty (used for FullInvalidate / LinesChanged).</summary>
    public void MarkAllDirty()
    {
        Array.Fill(_dirty, true);
    }

    /// <summary>Returns true if the line at <paramref name="index"/> is dirty.</summary>
    public bool IsDirty(int index) => (uint)index < (uint)_dirty.Length && _dirty[index];

    /// <summary>Clears the dirty flag for <paramref name="index"/>.</summary>
    public void ClearDirty(int index)
    {
        if ((uint)index < (uint)_dirty.Length)
            _dirty[index] = false;
    }
}
