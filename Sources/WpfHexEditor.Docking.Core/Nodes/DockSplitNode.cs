//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Docking.Core.Nodes;

/// <summary>
/// A node that splits its area between two or more children, either horizontally or vertically.
/// Each child gets a proportional share defined by <see cref="Ratios"/>.
/// </summary>
public class DockSplitNode : DockNode
{
    private readonly List<DockNode> _children = [];
    private readonly List<double> _ratios = [];
    private readonly List<double?> _pixelSizes = [];

    public SplitOrientation Orientation { get; set; }

    public IReadOnlyList<DockNode> Children => _children;

    public IReadOnlyList<double> Ratios => _ratios;

    /// <summary>
    /// Absolute pixel sizes for each child. Non-null values indicate fixed (Pixel) sizing;
    /// null values indicate flexible (Star) sizing (typically the document host).
    /// Used by <see cref="DockSplitPanel"/> to restore exact panel dimensions regardless of window size.
    /// </summary>
    public IReadOnlyList<double?> PixelSizes => _pixelSizes;

    /// <summary>
    /// Replaces all pixel sizes. Must match the child count.
    /// </summary>
    public void SetPixelSizes(double?[] sizes)
    {
        if (sizes.Length != _children.Count)
            throw new ArgumentException("PixelSizes count must match children count.", nameof(sizes));

        _pixelSizes.Clear();
        _pixelSizes.AddRange(sizes);
    }

    /// <summary>
    /// Adds a child with the given ratio. Existing ratios are not automatically re-normalized.
    /// </summary>
    public void AddChild(DockNode child, double ratio = 0.5)
    {
        ArgumentNullException.ThrowIfNull(child);

        child.Parent = this;
        _children.Add(child);
        _ratios.Add(ratio);
        _pixelSizes.Add(null);
    }

    /// <summary>
    /// Inserts a child at the specified index.
    /// </summary>
    public void InsertChild(int index, DockNode child, double ratio = 0.5)
    {
        ArgumentNullException.ThrowIfNull(child);

        child.Parent = this;
        _children.Insert(index, child);
        _ratios.Insert(index, ratio);
        _pixelSizes.Insert(index, null);
    }

    /// <summary>
    /// Removes a child node.
    /// </summary>
    internal void RemoveChild(DockNode child)
    {
        var index = _children.IndexOf(child);
        if (index < 0) return;

        _children.RemoveAt(index);
        _ratios.RemoveAt(index);
        if (_pixelSizes.Count > index) _pixelSizes.RemoveAt(index);
        child.Parent = null;
    }

    /// <summary>
    /// Replaces a child node with another.
    /// </summary>
    public void ReplaceChild(DockNode oldChild, DockNode newChild)
    {
        var index = _children.IndexOf(oldChild);
        if (index < 0)
            throw new ArgumentException("Child not found in this split node.", nameof(oldChild));

        _children[index] = newChild;
        newChild.Parent = this;
        oldChild.Parent = null;
    }

    /// <summary>
    /// Normalizes ratios so they sum to 1.0.
    /// </summary>
    public void NormalizeRatios()
    {
        if (_ratios.Count == 0) return;

        var sum = _ratios.Sum();
        if (sum <= 0)
        {
            var equal = 1.0 / _ratios.Count;
            for (var i = 0; i < _ratios.Count; i++)
                _ratios[i] = equal;
            return;
        }

        for (var i = 0; i < _ratios.Count; i++)
            _ratios[i] /= sum;
    }

    /// <summary>
    /// Replaces all ratios with the given values. Must match the child count.
    /// </summary>
    public void SetRatios(double[] ratios)
    {
        if (ratios.Length != _ratios.Count)
            throw new ArgumentException("Ratios count must match children count.", nameof(ratios));

        for (var i = 0; i < _ratios.Count; i++)
            _ratios[i] = ratios[i];
    }

    /// <summary>
    /// Sets ratios to equal distribution.
    /// </summary>
    public void EqualizeRatios()
    {
        var equal = 1.0 / Math.Max(1, _ratios.Count);
        for (var i = 0; i < _ratios.Count; i++)
            _ratios[i] = equal;
    }
}
